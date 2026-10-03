#if DEBUG
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Diagnostics;
using Sonarr.Http;

namespace Sonarr.Api.V3.System
{
    /// <summary>
    /// Debug-only endpoint that returns a single text file capturing the whole state of the
    /// running server (see <see cref="IDiagnosticsService"/>). It is compiled only in debug
    /// builds. It is no longer anonymous: the normal authentication policy applies, because
    /// the dump contains the full database and configuration.
    /// The SPA POSTs its browser state here (returning the file name), then downloads the file
    /// through <see cref="GetDiagnosticsFile"/> so the dump can be streamed rather than buffered.
    /// </summary>
    [V3ApiController("system/diagnostics")]
    public class DiagnosticsController : Controller
    {
        // The browser state is small; refuse oversized bodies so the dump endpoint cannot be
        // used to buffer arbitrary data into memory.
        private const int MaxFrontendStateBytes = 1_000_000;

        private static readonly NLog.Logger Logger = NzbDroneLogger.GetLogger(typeof(DiagnosticsController));

        private readonly IDiagnosticsService _diagnosticsService;

        public DiagnosticsController(IDiagnosticsService diagnosticsService)
        {
            _diagnosticsService = diagnosticsService;
        }

        [HttpGet]
        [Produces("text/plain")]
        public IActionResult GetDiagnostics()
        {
            try
            {
                var path = _diagnosticsService.CreateDiagnosticsFile();

                return PhysicalFile(path, "text/plain", Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create diagnostics dump");

                return StatusCode(500, "Failed to create diagnostics dump. See the logs for details.");
            }
        }

        [HttpPost]
        [RequestSizeLimit(MaxFrontendStateBytes)]
        [Produces("application/json")]
        public async Task<IActionResult> CreateDiagnostics()
        {
            try
            {
                string frontendState;

                using (var reader = new StreamReader(Request.Body))
                {
                    frontendState = await reader.ReadToEndAsync();
                }

                var path = _diagnosticsService.CreateDiagnosticsFile(frontendState);

                return Ok(new { File = Path.GetFileName(path) });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create diagnostics dump");

                return StatusCode(500, "Failed to create diagnostics dump. See the logs for details.");
            }
        }

        [HttpGet("{file}")]
        [Produces("text/plain")]
        public IActionResult GetDiagnosticsFile(string file)
        {
            var path = _diagnosticsService.GetDiagnosticsFile(file);

            if (path == null)
            {
                return NotFound();
            }

            return PhysicalFile(path, "text/plain", Path.GetFileName(path));
        }
    }
}
#endif
