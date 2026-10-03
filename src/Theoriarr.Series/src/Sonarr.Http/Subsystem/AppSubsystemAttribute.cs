using System;

namespace Sonarr.Http.Subsystem
{
    public interface IAppSubsystemMetadata
    {
        AppSubsystem Subsystem { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class AppSubsystemAttribute : Attribute, IAppSubsystemMetadata
    {
        public AppSubsystemAttribute(AppSubsystem subsystem)
        {
            Subsystem = subsystem;
        }

        public AppSubsystem Subsystem { get; }
    }
}
