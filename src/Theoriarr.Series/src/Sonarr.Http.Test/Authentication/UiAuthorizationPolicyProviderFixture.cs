using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;
using NzbDrone.Http.Authentication;

namespace Sonarr.Http.Test.Authentication
{
    [TestFixture]
    public class UiAuthorizationPolicyProviderFixture
    {
        private static UiAuthorizationPolicyProvider BuildSubject(AuthenticationType method)
        {
            var config = new Mock<IConfigFileProvider>();
            config.SetupGet(c => c.AuthenticationMethod).Returns(method);

            return new UiAuthorizationPolicyProvider(Options.Create(new AuthorizationOptions()), config.Object);
        }

        [Test]
        public async Task ui_policy_should_require_authentication_only()
        {
            var policy = await BuildSubject(AuthenticationType.None)
                .GetPolicyAsync(UiAuthorizationPolicyProvider.UiPolicyName);

            policy.Requirements.Should().ContainSingle(r => r is BypassableDenyAnonymousAuthorizationRequirement);
            policy.Requirements.OfType<BootstrapKeyAccessRequirement>().Should().BeEmpty();
        }

        [Test]
        public async Task initialize_policy_should_also_require_bootstrap_key_access()
        {
            var policy = await BuildSubject(AuthenticationType.None)
                .GetPolicyAsync(UiAuthorizationPolicyProvider.InitializePolicyName);

            policy.Requirements.Should().Contain(r => r is BypassableDenyAnonymousAuthorizationRequirement);
            policy.Requirements.Should().ContainSingle(r => r is BootstrapKeyAccessRequirement);
        }

        [Test]
        public async Task unknown_policy_should_fall_back_to_the_default_provider()
        {
            var policy = await BuildSubject(AuthenticationType.None)
                .GetPolicyAsync("SomeOtherPolicy");

            policy.Should().BeNull();
        }
    }
}
