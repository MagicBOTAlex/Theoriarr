using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Sonarr.Api.V5.System;
using Sonarr.Http.Subsystem;

namespace NzbDrone.Host.Test.Subsystem
{
    [TestFixture]
    public class V5SubsystemTaggingFixture
    {
        [Test]
        public void every_v5_controller_should_be_series_only()
        {
            var controllers = typeof(SystemController).Assembly
                .GetExportedTypes()
                .Where(type => !type.IsAbstract && type.Name.EndsWith("Controller"))
                .ToArray();

            controllers.Should().NotBeEmpty();

            foreach (var controller in controllers)
            {
                var subsystems = controller
                    .GetCustomAttributes(inherit: true)
                    .OfType<AppSubsystemAttribute>()
                    .Select(attribute => attribute.Subsystem)
                    .ToArray();

                subsystems.Should().Contain(
                    AppSubsystem.Series,
                    $"{controller.FullName} must be tagged [AppSubsystem(AppSubsystem.Series)]");
            }
        }
    }
}
