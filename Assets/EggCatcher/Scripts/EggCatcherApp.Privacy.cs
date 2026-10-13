using UnityEngine;

namespace EggCatcher
{
    public sealed partial class EggCatcherApp
    {
        private const string PrivacyScreen = "Privacy PolicyPanel";

        private void BindPrivacyPolicy()
        {

            if (!screens.TryGetValue(PrivacyScreen, out var panel)) return;
            var policy = panel.GetComponent<EggCatcherPrivacyPanel>() ?? panel.AddComponent<EggCatcherPrivacyPanel>();
            policy.Configure((RectTransform)Node(PrivacyScreen, "Content"), ShowSettings);
            Bind("SettingPanel", "Politic", () => ShowScreen(PrivacyScreen));
            Bind(PrivacyScreen, "TopPanel/Back", ShowSettings);
        }
    }
}
