using System;
using System.Net;
using NzbDrone.Core.Exceptions;

namespace NzbDrone.Core.MetadataSource.Provider
{
    public class MetadataSourceException : NzbDroneClientException
    {
        public MetadataSourceException(string message)
            : base(HttpStatusCode.ServiceUnavailable, message)
        {
        }

        public MetadataSourceException(string message, params object[] args)
            : base(HttpStatusCode.ServiceUnavailable, message, args)
        {
        }

        public MetadataSourceException(string message, Exception innerException, params object[] args)
            : base(HttpStatusCode.ServiceUnavailable, message, innerException, args)
        {
        }
    }
}
