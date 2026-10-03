using System;
using System.Collections.Generic;
using System.Linq;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tags;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Movies.Calendar
{
    [V3FeedController("calendar")]
    [AppSubsystem(AppSubsystem.Movies)]
    public class CalendarFeedController : Controller
    {
        private readonly IMovieService _movieService;
        private readonly ITagService _tagService;

        public CalendarFeedController(IMovieService movieService, ITagService tagService)
        {
            _movieService = movieService;
            _tagService = tagService;
        }

        [HttpGet("Radarr.ics")]
        public IActionResult GetCalendarFeed(int pastDays = 7, int futureDays = 28, string tags = "", bool unmonitored = false, IReadOnlyCollection<Sonarr.Api.V3.Calendar.CalendarReleaseType> releaseTypes = null)
        {
            var start = DateTime.Today.AddDays(-pastDays);
            var end = DateTime.Today.AddDays(futureDays);
            var parsedTags = new List<int>();

            if (tags.IsNotNullOrWhiteSpace())
            {
                parsedTags.AddRange(tags.Split(',').Select(_tagService.GetTag).Select(t => t.Id));
            }

            var movies = _movieService.GetMoviesBetweenDates(start, end, unmonitored);
            var calendar = new Ical.Net.Calendar
            {
                ProductId = "-//theoriarr//Theoriarr//EN"
            };

            var calendarName = "Radarr Movies Calendar";
            calendar.AddProperty(new CalendarProperty("NAME", calendarName));
            calendar.AddProperty(new CalendarProperty("X-WR-CALNAME", calendarName));

            foreach (var movie in movies.OrderBy(v => v.Added))
            {
                if (parsedTags.Any() && parsedTags.None(movie.Tags.Contains))
                {
                    continue;
                }

                if (releaseTypes is not { Count: not 0 } || releaseTypes.Contains(Sonarr.Api.V3.Calendar.CalendarReleaseType.CinemaRelease))
                {
                    CreateEvent(calendar, movie.MovieMetadata, "cinematic");
                }

                if (releaseTypes is not { Count: not 0 } || releaseTypes.Contains(Sonarr.Api.V3.Calendar.CalendarReleaseType.DigitalRelease))
                {
                    CreateEvent(calendar, movie.MovieMetadata, "digital");
                }

                if (releaseTypes is not { Count: not 0 } || releaseTypes.Contains(Sonarr.Api.V3.Calendar.CalendarReleaseType.PhysicalRelease))
                {
                    CreateEvent(calendar, movie.MovieMetadata, "physical");
                }
            }

            var serializer = (IStringSerializer)new SerializerFactory().Build(calendar.GetType(), new SerializationContext());
            var icalendar = serializer.SerializeToString(calendar);

            return Content(icalendar, "text/calendar");
        }

        private void CreateEvent(Ical.Net.Calendar calendar, MovieMetadata movie, string releaseType)
        {
            var date = movie.InCinemas;
            var eventType = "_cinemas";
            var summaryText = "(Theatrical Release)";

            if (releaseType == "digital")
            {
                date = movie.DigitalRelease;
                eventType = "_digital";
                summaryText = "(Digital Release)";
            }
            else if (releaseType == "physical")
            {
                date = movie.PhysicalRelease;
                eventType = "_physical";
                summaryText = "(Physical Release)";
            }

            if (!date.HasValue)
            {
                return;
            }

            var occurrence = calendar.Create<CalendarEvent>();
            occurrence.Uid = "Radarr_movie_" + movie.Id + eventType;
            occurrence.Status = movie.Status == MovieStatusType.Announced ? EventStatus.Tentative : EventStatus.Confirmed;

            occurrence.Start = new CalDateTime(date.Value, true);
            occurrence.End = occurrence.Start;

            occurrence.Description = movie.Overview;
            occurrence.Categories = new List<string> { movie.Studio };

            occurrence.Summary = $"{movie.Title} {summaryText}";
        }
    }
}
