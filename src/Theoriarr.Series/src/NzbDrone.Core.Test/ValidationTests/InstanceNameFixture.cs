using FluentAssertions;
using FluentValidation;
using NUnit.Framework;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Test.ValidationTests
{
    [TestFixture]
    public class InstanceNameFixture
    {
        private InstanceNameValidator _validator;

        [SetUp]
        public void Setup()
        {
            _validator = new InstanceNameValidator();
        }

        [TestCase("Theoriarr")]
        [TestCase("Sonarr")]
        [TestCase("MyTheoriarr")]
        [TestCase("MySonarr")]
        public void should_be_valid_instance_name(string instanceName)
        {
            _validator.Validate(new InstanceNameSettings { InstanceName = instanceName }).IsValid.Should().BeTrue();
        }

        [TestCase("")]
        [TestCase("Movies")]
        [TestCase("Radarr")]
        public void should_not_be_valid_instance_name(string instanceName)
        {
            _validator.Validate(new InstanceNameSettings { InstanceName = instanceName }).IsValid.Should().BeFalse();
        }

        private class InstanceNameSettings
        {
            public string InstanceName { get; set; }
        }

        private class InstanceNameValidator : AbstractValidator<InstanceNameSettings>
        {
            public InstanceNameValidator()
            {
                RuleFor(c => c.InstanceName).StartsOrEndsWithSonarr();
            }
        }
    }
}
