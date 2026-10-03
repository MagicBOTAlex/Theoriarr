using System.Net;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http;

namespace NzbDrone.Common.Test.Http
{
    [TestFixture]
    public class HttpResponseFixture
    {
        [Test]
        public void should_cap_and_cleanse_error_content()
        {
            var request = new HttpRequest("http://example.com/");
            var headers = new HttpHeader { ContentType = "application/json" };
            var body = "password=hunter2" + new string('a', 10240);

            var response = new HttpResponse(request, headers, body, HttpStatusCode.BadGateway);

            var text = response.ToString();

            text.Should().Contain("(truncated)");
            text.Should().NotContain("hunter2");
            text.Length.Should().BeLessThan(body.Length);
        }

        [Test]
        public void should_not_append_html_error_content()
        {
            var request = new HttpRequest("http://example.com/");
            var headers = new HttpHeader { ContentType = "text/html" };

            var response = new HttpResponse(request, headers, "password=hunter2", HttpStatusCode.BadGateway);

            response.ToString().Should().NotContain("hunter2");
        }
    }
}
