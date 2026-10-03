using System.Net.Http;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http;

namespace NzbDrone.Common.Test.Http
{
    [TestFixture]
    public class HttpRequestFixture
    {
        [Test]
        public void should_format_request_without_recursing()
        {
            var request = new HttpRequest("http://example.com/path");

            request.ToString().Should().Be("Req: [GET] http://example.com/path");
        }

        [Test]
        public void clone_should_be_independent_of_original()
        {
            var request = new HttpRequest("http://example.com/path");
            request.Headers.Add("Authorization", "Bearer secret");
            request.Cookies["session"] = "value";

            var clone = request.Clone();
            clone.Url = new HttpUri("http://other.example.com/");
            clone.Headers.Set("Authorization", "none");
            clone.Cookies["session"] = "changed";
            clone.Method = HttpMethod.Post;

            request.Url.FullUri.Should().Be("http://example.com/path");
            request.Headers["Authorization"].Should().Be("Bearer secret");
            request.Cookies["session"].Should().Be("value");
            request.Method.Should().Be(HttpMethod.Get);
        }
    }
}
