using System.Net;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Configuration
{
    [TestFixture]
    public class AllowedHostsParserFixture : TestBase
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void should_return_empty_list_for_null_or_empty(string value)
        {
            AllowedHostsParser.Parse(value).Should().BeEmpty();
        }

        [Test]
        public void should_split_on_comma_and_semicolon_and_trim_whitespace()
        {
            var result = AllowedHostsParser.Parse("sonarr.local, 192.168.1.5; myhost");

            result.Should().BeEquivalentTo("sonarr.local", "192.168.1.5", "myhost");
        }

        [Test]
        public void should_wrap_bare_ipv6_addresses_in_brackets()
        {
            var result = AllowedHostsParser.Parse("::1, 2001:db8::1, [fe80::1]");

            result.Should().BeEquivalentTo("[::1]", "[2001:db8::1]", "[fe80::1]");
        }

        [Test]
        public void should_not_duplicate_bare_and_bracketed_ipv6_addresses()
        {
            var result = AllowedHostsParser.Parse("::1,[::1]");

            result.Should().BeEquivalentTo("[::1]");
        }

        [Test]
        public void should_remove_empty_entries_and_duplicates()
        {
            var result = AllowedHostsParser.Parse("a,,b, a ");

            result.Should().BeEquivalentTo("a", "b");
        }

        [TestCase("sonarr.local")]
        [TestCase("sonarr.example.co.uk")]
        [TestCase("my-host")]
        [TestCase("192.168.1.5")]
        [TestCase("[::1]")]
        [TestCase("[2001:db8::1]")]
        [TestCase("2001:db8::1")]
        [TestCase("*.example.com")]
        [TestCase("*.example.co.uk")]
        [TestCase("*.subdomain.example.com")]
        [TestCase("*.subdomain.example.co.uk")]
        public void should_be_valid_host(string host)
        {
            AllowedHostsParser.IsValidHost(host).Should().BeTrue();
        }

        [TestCase("192.168.50.1/24")]
        [TestCase("192.168.50.0/24")]
        [TestCase("10.0.0.0/8")]
        [TestCase("172.16.0.0/12")]
        [TestCase("0.0.0.0/0")]
        [TestCase("fc00::/7")]
        [TestCase("2001:db8::/32")]
        public void should_be_valid_host_or_network(string host)
        {
            AllowedHostsParser.IsValidHostOrNetwork(host).Should().BeTrue();
        }

        [TestCase("192.168.50.1/33")]
        [TestCase("sonarr.local/24")]
        [TestCase("192.168.50.1/abc")]
        [TestCase("192.168.50.1/24/8")]
        [TestCase("http://sonarr.local")]
        public void should_not_be_valid_host_or_network(string host)
        {
            AllowedHostsParser.IsValidHostOrNetwork(host).Should().BeFalse();
        }

        [Test]
        public void parse_hosts_should_exclude_networks()
        {
            var result = AllowedHostsParser.ParseHosts("sonarr.local, 192.168.1.5, 192.168.50.1/24, 10.0.0.0/8");

            result.Should().BeEquivalentTo("sonarr.local", "192.168.1.5");
        }

        [Test]
        public void parse_networks_should_mask_host_bits()
        {
            var result = AllowedHostsParser.ParseNetworks("192.168.50.1/24");

            result.Should().HaveCount(1);
            result[0].Network.Should().Be(IPAddress.Parse("192.168.50.0"));
            result[0].PrefixLength.Should().Be(24);
        }

        [Test]
        public void parse_networks_should_ignore_non_networks()
        {
            var result = AllowedHostsParser.ParseNetworks("sonarr.local,192.168.1.5,10.0.0.0/8");

            result.Should().HaveCount(1);
            result[0].Network.Should().Be(IPAddress.Parse("10.0.0.0"));
        }

        [TestCase("192.168.50.59", true)]
        [TestCase("192.168.50.1", true)]
        [TestCase("192.168.50.255", true)]
        [TestCase("192.168.51.1", false)]
        [TestCase("10.0.0.1", false)]
        public void network_should_contain_ipv4(string address, bool expected)
        {
            AllowedHostsParser.ParseNetworks("192.168.50.1/24")[0]
                .Contains(IPAddress.Parse(address))
                .Should()
                .Be(expected);
        }

        [Test]
        public void network_should_contain_ipv4_mapped_to_ipv6()
        {
            AllowedHostsParser.ParseNetworks("192.168.50.0/24")[0]
                .Contains(IPAddress.Parse("::ffff:192.168.50.59"))
                .Should()
                .BeTrue();
        }

        [Test]
        public void network_should_not_contain_other_address_family()
        {
            AllowedHostsParser.ParseNetworks("192.168.50.0/24")[0]
                .Contains(IPAddress.Parse("2001:db8::1"))
                .Should()
                .BeFalse();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("http://sonarr.local")]
        [TestCase("sonarr.local:8989")]
        [TestCase("sonarr local")]
        [TestCase("sonarr.*")]
        [TestCase("*")]
        [TestCase("*.")]
        [TestCase("*.192.168.1.5")]
        [TestCase("*.[::1]")]
        [TestCase("*.*.example.com")]
        [TestCase("[not-an-address]")]
        [TestCase(".example.com")]
        [TestCase(".example.co.uk")]
        public void should_not_be_valid_host(string host)
        {
            AllowedHostsParser.IsValidHost(host).Should().BeFalse();
        }
    }
}
