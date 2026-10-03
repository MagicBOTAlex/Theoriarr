using NUnit.Framework;

namespace NzbDrone.Api.Test
{
    [SetUpFixture]
    public class SetupFixture
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Resolve the core assembly before AutoMoqer installs its own assembly resolver,
            // which assumes a requesting assembly and otherwise faults.
            _ = NzbDrone.Common.Reflection.ReflectionExtensions.CoreAssembly;
        }
    }
}
