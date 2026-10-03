using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Localization;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public interface ITranscodeProfileService
    {
        List<TranscodeProfile> GetAll();
        TranscodeProfile Get(int id);

        // Non-throwing lookup for callers that only need the profile as an optional input (e.g. the
        // create-jobs path, which must not 404 a request that has nothing to queue).
        TranscodeProfile Find(int id);
        TranscodeProfile Add(TranscodeProfile profile);
        TranscodeProfile Update(TranscodeProfile profile);
        void Delete(int id);
    }

    public class TranscodeProfileService : ITranscodeProfileService
    {
        private static readonly string[] Codecs = { "h264", "hevc", "av1" };

        private readonly ITranscodeProfileRepository _repository;
        private readonly ILocalizationService _localizationService;
        private readonly Logger _logger;

        // Serialises the read-modify-write of the single-default invariant (Add/Update/Delete), so
        // two concurrent requests cannot both observe "no default" and promote two profiles.
        private readonly object _defaultLock = new object();

        public TranscodeProfileService(ITranscodeProfileRepository repository, ILocalizationService localizationService, Logger logger)
        {
            _repository = repository;
            _localizationService = localizationService;
            _logger = logger;
        }

        public List<TranscodeProfile> GetAll()
        {
            return _repository.All().OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public TranscodeProfile Get(int id)
        {
            return _repository.Get(id);
        }

        public TranscodeProfile Find(int id)
        {
            return _repository.Find(id);
        }

        public TranscodeProfile Add(TranscodeProfile profile)
        {
            Normalize(profile);

            lock (_defaultLock)
            {
                if (!_repository.All().Any())
                {
                    profile.IsDefault = true;
                }

                if (profile.IsDefault)
                {
                    ClearDefaults();
                }

                var added = _repository.Insert(profile);

                EnsureDefault(promoteId: added.Id);

                return added;
            }
        }

        public TranscodeProfile Update(TranscodeProfile profile)
        {
            Normalize(profile);

            lock (_defaultLock)
            {
                if (profile.IsDefault)
                {
                    ClearDefaults(profile.Id);
                }

                _repository.Update(profile);

                EnsureDefault(excludeId: profile.IsDefault ? null : profile.Id);

                return profile;
            }
        }

        public void Delete(int id)
        {
            lock (_defaultLock)
            {
                var profile = _repository.Get(id);

                _repository.Delete(id);

                if (profile != null && profile.IsDefault)
                {
                    var next = _repository.All().OrderBy(candidate => candidate.Id).FirstOrDefault();

                    if (next != null)
                    {
                        next.IsDefault = true;
                        _repository.Update(next);
                    }
                }
            }
        }

        private void ClearDefaults(int? exceptId = null)
        {
            foreach (var profile in _repository.All().Where(candidate => candidate.IsDefault && candidate.Id != exceptId))
            {
                profile.IsDefault = false;
                _repository.Update(profile);
            }
        }

        // Guarantees exactly one profile remains the default. Used after an add/update that may
        // have left no default (e.g. unsetting the only default, or a pre-existing broken state).
        private void EnsureDefault(int? promoteId = null, int? excludeId = null)
        {
            var profiles = _repository.All().ToList();

            if (profiles.Any(profile => profile.IsDefault))
            {
                return;
            }

            // Prefer the profile being promoted/added; otherwise the lowest-id profile that is not
            // the one just updated. If that excluded profile is the only candidate left (unsetting
            // the sole default), fall back to it so a default always survives.
            var candidate = (promoteId.HasValue ? profiles.FirstOrDefault(profile => profile.Id == promoteId.Value) : null)
                ?? profiles.Where(profile => profile.Id != excludeId).OrderBy(profile => profile.Id).FirstOrDefault()
                ?? profiles.OrderBy(profile => profile.Id).FirstOrDefault();

            if (candidate != null)
            {
                candidate.IsDefault = true;
                _repository.Update(candidate);
            }
        }

        private void Normalize(TranscodeProfile profile)
        {
            // The API rejects a blank name, so this fallback is only a defensive default; localize it
            // rather than storing a hard-coded English literal.
            profile.Name = profile.Name.IsNullOrWhiteSpace() ? _localizationService.GetLocalizedString("Untitled") : profile.Name.Trim();
            profile.Preset = TranscodeInput.NormalizePreset(profile.Preset);
            profile.DeviceId = profile.DeviceId.IsNullOrWhiteSpace() ? null : profile.DeviceId.Trim();
            profile.TargetPercent = profile.TargetPercent.HasValue ? Math.Clamp(profile.TargetPercent.Value, 1, 99) : null;
            profile.TargetSizeMB = profile.TargetSizeMB is > 0 ? profile.TargetSizeMB : null;
            profile.MaxHeight = TranscodeInput.NormalizeMaxHeight(profile.MaxHeight);
            profile.Tag = NormalizeTag(profile.Tag);

            if (profile.Mode == TranscodeMode.Remux)
            {
                profile.Codec = null;
                profile.Container = TranscodeInput.NormalizeContainer(profile.Container);
                profile.QualityValue = 0;
                profile.Preset = null;
                profile.TargetSizeMB = null;
                profile.TargetPercent = null;
                profile.MaxHeight = null;
                profile.DeviceId = null;
                return;
            }

            profile.Codec = Codecs.Contains(profile.Codec?.ToLowerInvariant()) ? profile.Codec.ToLowerInvariant() : "hevc";

            // x264/x265 (and their hardware siblings) reject CRF/QP above 51; only the AV1 encoders
            // accept up to 63, so clamp to the range the profile's codec can actually encode.
            profile.QualityValue = Math.Clamp(profile.QualityValue, 0, TranscodeArgumentBuilder.MaxQualityForCodec(TranscodeCodecs.Parse(profile.Codec)));
            profile.Container = null;
        }

        private static string NormalizeTag(string tag)
        {
            if (tag.IsNullOrWhiteSpace())
            {
                return null;
            }

            var cleaned = new string(tag.Trim()
                                        .Where(character => !Path.GetInvalidFileNameChars().Contains(character) && character != '[' && character != ']')
                                        .ToArray())
                                 .Trim();

            if (cleaned.IsNullOrWhiteSpace())
            {
                return null;
            }

            return cleaned.Length > 50 ? cleaned[..50] : cleaned;
        }
    }
}
