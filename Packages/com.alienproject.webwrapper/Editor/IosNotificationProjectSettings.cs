using System;
using System.Linq;
using System.Reflection;

namespace AlienProject.WebWrapper.Editor
{
    internal static class IosNotificationProjectSettings
    {

        private static Type SettingsType => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("Unity.Notifications.NotificationSettings+iOSSettings", false))
            .FirstOrDefault(type => type != null);

        public static bool HasDeferredAuthorization =>
            ReadBoolean("RequestAuthorizationOnAppLaunch") == false &&
            ReadBoolean("NotificationRequestAuthorizationForRemoteNotificationsOnAppLaunch") == false;

        public static bool UsesProductionApns => ReadBoolean("UseAPSReleaseEnvironment") == true;

        public static bool Apply()
        {
            var type = SettingsType;
            if (type == null)
            {
                return false;
            }

            SetBoolean(type, "RequestAuthorizationOnAppLaunch", false);
            SetBoolean(type, "NotificationRequestAuthorizationForRemoteNotificationsOnAppLaunch", false);
            SetBoolean(type, "AddRemoteNotificationCapability", true);
            return true;
        }

        private static bool? ReadBoolean(string name)
        {
            return SettingsType?.GetProperty(name, BindingFlags.Static | BindingFlags.Public)
                ?.GetValue(null) as bool?;
        }

        private static void SetBoolean(Type type, string name, bool value)
        {
            type.GetProperty(name, BindingFlags.Static | BindingFlags.Public)?.SetValue(null, value);
        }
    }
}
