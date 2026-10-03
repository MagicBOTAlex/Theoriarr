using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Transcoding.HardwareAccel;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public interface IGpuCapabilityService
    {
        MediaCompressionCapabilities GetCapabilities(bool force = false);
        List<TranscodeDevice> GetDevices();
        List<TranscodeDevice> UpdateDevices(List<TranscodeDevice> devices);
        string GetFingerprint();
    }

    // Owns device detection: asks every backend to enumerate its devices, validates encoders with
    // tiny probe encodes, merges the result with the user's stored control settings (matched by the
    // stable device id) and caches the snapshot so a poll does not re-probe.
    public class GpuCapabilityService : IGpuCapabilityService
    {
        private static readonly object Locker = new object();

        // How long a probed snapshot is trusted before the (subprocess-spawning) fingerprint is
        // recomputed. An explicit reprobe (force) always recomputes.
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

        private readonly IEnumerable<IHardwareAccelBackend> _backends;
        private readonly IFFmpegInfo _ffmpegInfo;
        private readonly IFFmpegProvider _ffmpegProvider;
        private readonly IConfigService _configService;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        private MediaCompressionCapabilities _cache;

        public GpuCapabilityService(IEnumerable<IHardwareAccelBackend> backends,
                                    IFFmpegInfo ffmpegInfo,
                                    IFFmpegProvider ffmpegProvider,
                                    IConfigService configService,
                                    IAppFolderInfo appFolderInfo,
                                    IDiskProvider diskProvider,
                                    Logger logger)
        {
            _backends = backends;
            _ffmpegInfo = ffmpegInfo;
            _ffmpegProvider = ffmpegProvider;
            _configService = configService;
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public MediaCompressionCapabilities GetCapabilities(bool force = false)
        {
            lock (Locker)
            {
                // Callers (the scheduler, health checks) read the snapshot outside the lock, so they
                // must never receive the mutable cache itself; hand out a detached copy.
                return Snapshot(GetCapabilitiesInternal(force));
            }
        }

        // The cache is authoritative for its lifetime: a poll must not spawn ffmpeg/nvidia-smi/vainfo
        // every tick. The fingerprint (and a full re-detect) only runs when the cache is absent/expired
        // or an explicit reprobe was requested. Always called under Locker and returns the live cache.
        private MediaCompressionCapabilities GetCapabilitiesInternal(bool force = false)
        {
            if (!force && IsFresh(_cache))
            {
                return _cache;
            }

            var fingerprint = GetFingerprint();

            if (!force && _cache != null && _cache.Fingerprint == fingerprint)
            {
                Renew(_cache);
                return _cache;
            }

            var stored = LoadStored();

            if (!force && stored != null && stored.Fingerprint == fingerprint && HasCodecSettings(stored))
            {
                Renew(stored);
                _cache = stored;
                return _cache;
            }

            var capabilities = new MediaCompressionCapabilities
            {
                Fingerprint = fingerprint,
                LastProbed = DateTime.UtcNow,
                FFmpegPath = _ffmpegProvider.GetFFmpegPath(),
                FFmpegVersion = _ffmpegInfo.GetVersion(),
                Devices = Merge(Detect(), stored?.Devices)
            };

            Persist(capabilities);
            _cache = capabilities;

            _logger.Info("Probed {0} transcode device(s): {1}", capabilities.Devices.Count, capabilities.Devices.Select(d => d.Id).Join(", "));

            return _cache;
        }

        public List<TranscodeDevice> GetDevices()
        {
            lock (Locker)
            {
                return Snapshot(GetCapabilitiesInternal())?.Devices;
            }
        }

        public List<TranscodeDevice> UpdateDevices(List<TranscodeDevice> devices)
        {
            lock (Locker)
            {
                var capabilities = GetCapabilitiesInternal();

                foreach (var device in capabilities.Devices)
                {
                    var update = devices?.FirstOrDefault(candidate => candidate.Id == device.Id);

                    if (update == null)
                    {
                        continue;
                    }

                    var hardCap = Math.Max(1, device.Capabilities?.MaxSessions ?? 1);

                    device.Enabled = update.Enabled;
                    device.MaxParallel = Math.Max(1, Math.Min(update.MaxParallel, hardCap));
                    device.Priority = update.Priority;
                    device.Weight = Math.Max(0, update.Weight);

                    if (update.Options != null)
                    {
                        // Deep-copy: the caller's instance must never be aliased into the canonical
                        // cache, or a later mutation would silently change persisted settings.
                        device.Options = Snapshot(update.Options);
                    }

                    if (update.Codecs != null)
                    {
                        foreach (var codec in device.Codecs ?? new List<TranscodeCodecSetting>())
                        {
                            var updated = update.Codecs.FirstOrDefault(candidate => string.Equals(candidate.Codec, codec.Codec, StringComparison.OrdinalIgnoreCase));

                            if (updated != null)
                            {
                                codec.Enabled = updated.Enabled;
                            }
                        }
                    }
                }

                Persist(capabilities);
                _cache = capabilities;

                return Snapshot(capabilities.Devices);
            }
        }

        public string GetFingerprint()
        {
            var parts = new List<string>
            {
                _ffmpegProvider.GetFFmpegPath() ?? "no-ffmpeg",
                _ffmpegInfo.GetVersion() ?? "no-version"
            };

            foreach (var backend in _backends.OrderBy(backend => backend.Kind))
            {
                parts.Add($"{backend.Kind}:{backend.GetFingerprint()}");
            }

            return Hash(parts.Join("|"));
        }

        // A deep, detached copy of the cached snapshot so callers can never observe a device
        // mid-update; the cache and the scheduler then share no mutable state.
        public static MediaCompressionCapabilities Snapshot(MediaCompressionCapabilities capabilities)
        {
            if (capabilities == null)
            {
                return null;
            }

            return new MediaCompressionCapabilities
            {
                Fingerprint = capabilities.Fingerprint,
                LastProbed = capabilities.LastProbed,
                FFmpegPath = capabilities.FFmpegPath,
                FFmpegVersion = capabilities.FFmpegVersion,
                Devices = Snapshot(capabilities.Devices)
            };
        }

        public static List<TranscodeDevice> Snapshot(List<TranscodeDevice> devices)
        {
            return devices?.Select(Snapshot).ToList() ?? new List<TranscodeDevice>();
        }

        private static TranscodeDevice Snapshot(TranscodeDevice device)
        {
            if (device == null)
            {
                return null;
            }

            return new TranscodeDevice
            {
                Id = device.Id,
                Kind = device.Kind,
                Name = device.Name,
                Supported = device.Supported,
                Enabled = device.Enabled,
                MaxParallel = device.MaxParallel,
                Priority = device.Priority,
                Weight = device.Weight,
                Unavailable = device.Unavailable,
                Options = Snapshot(device.Options),
                Codecs = device.Codecs?.Select(codec => new TranscodeCodecSetting
                {
                    Codec = codec.Codec,
                    Encoder = codec.Encoder,
                    Supported = codec.Supported,
                    Enabled = codec.Enabled
                }).ToList() ?? new List<TranscodeCodecSetting>(),
                Capabilities = Snapshot(device.Capabilities)
            };
        }

        private static TranscodeDeviceOptions Snapshot(TranscodeDeviceOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return new TranscodeDeviceOptions
            {
                Cpu = options.Cpu == null ? null : new CpuTranscodeOptions { Threads = options.Cpu.Threads, Nice = options.Cpu.Nice },
                Nvidia = options.Nvidia == null ? null : new NvidiaTranscodeOptions { DecodeAccel = options.Nvidia.DecodeAccel, ExtraArgs = options.Nvidia.ExtraArgs },
                Vaapi = options.Vaapi == null ? null : new VaapiTranscodeOptions { DecodeAccel = options.Vaapi.DecodeAccel, ExtraArgs = options.Vaapi.ExtraArgs }
            };
        }

        private static DeviceCapability Snapshot(DeviceCapability capability)
        {
            if (capability == null)
            {
                return null;
            }

            return new DeviceCapability
            {
                Id = capability.Id,
                Kind = capability.Kind,
                Name = capability.Name,
                Supported = capability.Supported,
                Encoders = capability.Encoders?.ToList() ?? new List<string>(),
                Decoders = capability.Decoders?.ToList() ?? new List<string>(),
                PixelFormats = capability.PixelFormats?.ToList() ?? new List<string>(),
                RateControls = capability.RateControls?.ToList() ?? new List<string>(),
                Presets = capability.Presets?.ToList() ?? new List<string>(),
                Profiles = capability.Profiles?.ToList() ?? new List<string>(),
                Entrypoints = capability.Entrypoints?.ToList() ?? new List<string>(),
                Filters = capability.Filters?.ToList() ?? new List<string>(),
                Tonemap = capability.Tonemap,
                MaxSessions = capability.MaxSessions,
                VramMB = capability.VramMB,
                Driver = capability.Driver
            };
        }

        // Detection and merge are static/self-contained so the merge semantics can be unit-tested
        // without touching real hardware.
        public static List<TranscodeDevice> Merge(List<DeviceCapability> detected, List<TranscodeDevice> stored)
        {
            detected ??= new List<DeviceCapability>();
            stored ??= new List<TranscodeDevice>();

            var devices = new List<TranscodeDevice>();

            foreach (var capability in detected)
            {
                var existing = stored.FirstOrDefault(device => device.Id == capability.Id);
                var device = new TranscodeDevice
                {
                    Id = capability.Id,
                    Kind = capability.Kind,
                    Name = capability.Name,
                    Supported = capability.Supported,
                    Capabilities = capability,
                    Unavailable = false,
                    Options = existing?.Options ?? new TranscodeDeviceOptions()
                };

                if (existing != null)
                {
                    device.Enabled = existing.Enabled;
                    device.MaxParallel = existing.MaxParallel > 0 ? existing.MaxParallel : DefaultMaxParallel(capability);
                    device.Priority = existing.Priority > 0 ? existing.Priority : DefaultPriority(capability);
                }
                else
                {
                    device.Enabled = capability.Supported;
                    device.MaxParallel = DefaultMaxParallel(capability);
                    device.Priority = DefaultPriority(capability);
                }

                device.Weight = existing != null && existing.Weight > 0 ? existing.Weight : 100;
                device.Codecs = BuildCodecs(device, existing);

                devices.Add(device);
            }

            foreach (var missing in stored.Where(device => detected.All(capability => capability.Id != device.Id)))
            {
                missing.Unavailable = true;
                missing.Codecs = BuildCodecs(missing, missing);
                devices.Add(missing);
            }

            return devices.OrderBy(device => device.Priority).ThenBy(device => device.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // True while the cached snapshot is within its lifetime; a null or undated cache is never fresh,
        // so the first call after a restart still validates it against the current fingerprint.
        private static bool IsFresh(MediaCompressionCapabilities capabilities)
        {
            return capabilities?.LastProbed != null &&
                   DateTime.UtcNow - capabilities.LastProbed.Value < CacheLifetime;
        }

        // A match against the current fingerprint restarts the lifetime in memory only; the persisted
        // snapshot keeps its original probe time so a restart re-validates it once.
        private static void Renew(MediaCompressionCapabilities capabilities)
        {
            capabilities.LastProbed = DateTime.UtcNow;
        }

        // A snapshot stored before per-codec settings existed (or one whose devices have no codec list)
        // is re-detected so the codec toggles appear without a manual reprobe.
        private static bool HasCodecSettings(MediaCompressionCapabilities capabilities)
        {
            return capabilities.Devices != null &&
                   capabilities.Devices.All(device => device.Codecs != null && device.Codecs.Count > 0);
        }

        private static List<TranscodeCodecSetting> BuildCodecs(TranscodeDevice device, TranscodeDevice existing)
        {
            var settings = new List<TranscodeCodecSetting>();

            foreach (var codec in TranscodeCodecs.All)
            {
                var name = TranscodeCodecs.Name(codec);
                var encoder = TranscodeArgumentBuilder.GetEncoderFor(device, codec);
                var supported = encoder != null;
                var enabled = supported;

                var stored = existing?.Codecs?.FirstOrDefault(setting => string.Equals(setting.Codec, name, StringComparison.OrdinalIgnoreCase));

                if (stored != null)
                {
                    // A stored enable flag that differs from the support state it was stored with is an
                    // explicit user override (including forcing an unsupported codec on); otherwise the
                    // choice was just the default and should follow fresh detection.
                    enabled = stored.Enabled == stored.Supported ? supported : stored.Enabled;
                }

                settings.Add(new TranscodeCodecSetting
                {
                    Codec = name,
                    Encoder = encoder ?? TranscodeArgumentBuilder.GetEncoderFor(device, codec, allowUnsupported: true),
                    Supported = supported,
                    Enabled = enabled
                });
            }

            return settings;
        }

        private static int DefaultMaxParallel(DeviceCapability capability)
        {
            return capability.Kind switch
            {
                TranscodeDeviceKind.Nvidia => Math.Max(1, Math.Min(2, capability.MaxSessions / 2)),
                _ => 1
            };
        }

        private static int DefaultPriority(DeviceCapability capability)
        {
            return capability.Kind switch
            {
                TranscodeDeviceKind.Nvidia => 10,
                TranscodeDeviceKind.Vaapi => 20,
                _ => 100
            };
        }

        private static string Hash(string value)
        {
            using var sha = SHA1.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }

        private List<DeviceCapability> Detect()
        {
            var detected = new List<DeviceCapability>();

            foreach (var backend in _backends)
            {
                try
                {
                    detected.AddRange(backend.Detect() ?? new List<DeviceCapability>());
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Capability detection failed for the {0} backend", backend.Kind);
                }
            }

            if (detected.All(device => device.Kind != TranscodeDeviceKind.Software))
            {
                detected.Add(new DeviceCapability
                {
                    Id = SoftwareBackend.DeviceId,
                    Kind = TranscodeDeviceKind.Software,
                    Name = "CPU",
                    Supported = false
                });
            }

            return detected;
        }

        private MediaCompressionCapabilities LoadStored()
        {
            var json = _configService.TranscodeDevicesConfig;

            if (json.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                return Json.Deserialize<MediaCompressionCapabilities>(json);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to deserialize the stored transcode device registry");
                return null;
            }
        }

        private void Persist(MediaCompressionCapabilities capabilities)
        {
            _configService.TranscodeDevicesConfig = capabilities.ToJson();

            try
            {
                var folder = Path.Combine(_appFolderInfo.AppDataFolder, "media-compression");

                if (!_diskProvider.FolderExists(folder))
                {
                    _diskProvider.CreateFolder(folder);
                }

                _diskProvider.WriteAllText(Path.Combine(folder, "devices.json"), capabilities.ToJson());
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to write the transcode capability snapshot");
            }
        }
    }
}
