using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Statistics;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.Statistics;

[V5ApiController("statistics")]
[AppSubsystem(AppSubsystem.Series)]
public class StatisticsController : Controller
{
    private readonly IStatisticsService _statisticsService;

    public StatisticsController(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    [HttpGet]
    [Produces("application/json")]
    public Ok<StatisticsResource> GetLibraryStatistics([FromQuery] StatisticsFilter filter)
    {
        return TypedResults.Ok(_statisticsService.GetLibraryStatistics(filter).MapToResource());
    }
}
