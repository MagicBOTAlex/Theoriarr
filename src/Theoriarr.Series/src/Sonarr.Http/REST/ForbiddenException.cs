using System.Net;
using Sonarr.Http.Exceptions;

namespace Sonarr.Http.REST
{
    public class ForbiddenException : ApiException
    {
        public ForbiddenException(object content = null)
            : base(HttpStatusCode.Forbidden, content)
        {
        }
    }
}
