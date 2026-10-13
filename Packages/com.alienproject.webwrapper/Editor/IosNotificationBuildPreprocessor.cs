using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace AlienProject.WebWrapper.Editor
{
    internal sealed class IosNotificationBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.iOS && !IosNotificationProjectSettings.Apply())
            {
                throw new BuildFailedException("[WebWrapper] Mobile Notifications is required for deferred iOS notification authorization.");
            }
        }
    }
}
