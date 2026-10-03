using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Sonarr.Http.Subsystem;

namespace Sonarr.Http.Test.Subsystem
{
    [TestFixture]
    public class AppSubsystemConventionFixture
    {
        [Test]
        public void should_default_untagged_controller_to_series()
        {
            var selector = ApplyConvention(BuildApplication(BuildController()), AppSubsystem.Series, typeof(FakeController).Assembly);

            selector.EndpointMetadata.OfType<AppSubsystemAttribute>()
                .Should().ContainSingle()
                .Which.Subsystem.Should().Be(AppSubsystem.Series);
        }

        [Test]
        public void should_respect_explicit_controller_subsystem()
        {
            var controller = BuildController(new List<object> { new AppSubsystemAttribute(AppSubsystem.Movies) });
            var selector = ApplyConvention(BuildApplication(controller), AppSubsystem.Series, typeof(FakeController).Assembly);

            selector.EndpointMetadata.OfType<AppSubsystemAttribute>().Should().BeEmpty();
        }

        [Test]
        public void should_respect_explicit_action_subsystem()
        {
            var controller = BuildController(actionAttributes: new List<object> { new AppSubsystemAttribute(AppSubsystem.Movies) });
            var selector = ApplyConvention(BuildApplication(controller), AppSubsystem.Series, typeof(FakeController).Assembly);

            selector.EndpointMetadata.OfType<AppSubsystemAttribute>().Should().BeEmpty();
        }

        [Test]
        public void should_ignore_controllers_from_other_assemblies()
        {
            var selector = ApplyConvention(BuildApplication(BuildController()), AppSubsystem.Series, typeof(AppSubsystemConvention).Assembly);

            selector.EndpointMetadata.OfType<AppSubsystemAttribute>().Should().BeEmpty();
        }

        [Test]
        public void should_register_convention_in_mvc_options()
        {
            var services = new ServiceCollection();
            services.AddAppSubsystemConvention(AppSubsystem.Series, typeof(FakeController).Assembly);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<MvcOptions>>().Value;

            options.Conventions.OfType<AppSubsystemConvention>().Should().ContainSingle();
        }

        private static SelectorModel ApplyConvention(ApplicationModel application, AppSubsystem subsystem, Assembly assembly)
        {
            new AppSubsystemConvention(subsystem, assembly).Apply(application);

            return application.Controllers.Single().Actions.Single().Selectors.Single();
        }

        private static ApplicationModel BuildApplication(params ControllerModel[] controllers)
        {
            var application = new ApplicationModel();

            foreach (var controller in controllers)
            {
                application.Controllers.Add(controller);
            }

            return application;
        }

        private static ControllerModel BuildController(IReadOnlyList<object> attributes = null, IReadOnlyList<object> actionAttributes = null)
        {
            var controller = new ControllerModel(typeof(FakeController).GetTypeInfo(), attributes ?? new List<object>());
            var action = new ActionModel(typeof(FakeController).GetMethod(nameof(FakeController.FakeAction)), actionAttributes ?? new List<object>());

            action.Selectors.Add(new SelectorModel());
            controller.Actions.Add(action);

            return controller;
        }

        private class FakeController
        {
            public void FakeAction()
            {
            }
        }
    }
}
