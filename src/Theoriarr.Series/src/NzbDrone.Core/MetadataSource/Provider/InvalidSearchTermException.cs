using System.Net;
using NzbDrone.Core.Exceptions;

namespace NzbDrone.Core.MetadataSource.Provider;
public class InvalidSearchTermException : NzbDroneClientException
{
    public InvalidSearchTermException(string message, params object[] args)
        : base(HttpStatusCode.BadRequest, message, args)
    {
    }
}
