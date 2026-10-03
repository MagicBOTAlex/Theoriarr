using System;

namespace NzbDrone.Common.Exceptions
{
    public class RadarrStartupException : NzbDroneException
    {
        public RadarrStartupException(string message, params object[] args)
            : base("Theoriarr failed to start: " + string.Format(message, args))
        {
        }

        public RadarrStartupException(string message)
            : base("Theoriarr failed to start: " + message)
        {
        }

        public RadarrStartupException()
            : base("Theoriarr failed to start")
        {
        }

        public RadarrStartupException(Exception innerException, string message, params object[] args)
            : base("Theoriarr failed to start: " + string.Format(message, args), innerException)
        {
        }

        public RadarrStartupException(Exception innerException, string message)
            : base("Theoriarr failed to start: " + message, innerException)
        {
        }

        public RadarrStartupException(Exception innerException)
            : base("Theoriarr failed to start: " + innerException.Message)
        {
        }
    }
}
