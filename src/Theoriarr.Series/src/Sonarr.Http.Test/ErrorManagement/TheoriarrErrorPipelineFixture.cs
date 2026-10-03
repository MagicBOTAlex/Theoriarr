using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;
using Sonarr.Http.ErrorManagement;

namespace Sonarr.Http.Test.ErrorManagement
{
    [TestFixture]
    public class TheoriarrErrorPipelineFixture : TestBase<TheoriarrErrorPipeline>
    {
        [Test]
        public async Task error_description_should_only_be_exposed_in_debug_builds()
        {
            var context = new DefaultHttpContext();
            var body = new MemoryStream();
            context.Response.Body = body;

            context.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
            {
                Error = new InvalidOperationException("boom")
            });

            await Subject.HandleException(context);

            body.Position = 0;
            var json = await new StreamReader(body).ReadToEndAsync();

            json.Should().Contain("boom");

            if (BuildInfo.IsDebug)
            {
                json.Should().Contain("InvalidOperationException");
            }
            else
            {
                json.Should().NotContain("InvalidOperationException");
            }
        }
    }
}
