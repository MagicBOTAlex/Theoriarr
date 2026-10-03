using Microsoft.AspNetCore.Http;

namespace Sonarr.Http.Subsystem
{
    public static class SubsystemContext
    {
        public const string ItemsKey = "Theoriarr.AppSubsystem";

        public static void Set(HttpContext context, AppSubsystem subsystem)
        {
            context.Items[ItemsKey] = subsystem;
        }

        public static AppSubsystem? Get(HttpContext context)
        {
            if (context?.Items != null &&
                context.Items.TryGetValue(ItemsKey, out var value) &&
                value is AppSubsystem subsystem)
            {
                return subsystem;
            }

            return null;
        }
    }
}
