using System;
using System.Linq;
using System.Reflection;
using AlienProject.WebWrapper;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class SunnyJoyTestSetup
{
    private const string BundleId = "com.gameandjoy.sunnyjoygame";
    private const string AppleId = "6820946065";
    private const string SettingsPath = "Assets/AlienProject/WebWrapper/Resources/WebWrapperSettings.asset";
    private const string ArtFolder = "Assets/AlienProject/WebWrapper/SunnyJoyTestBranding";
    private static readonly Vector2[] SunRays = Enumerable.Range(0, 12)
        .Select(index => new Vector2(Mathf.Cos(index * Mathf.PI / 6f), Mathf.Sin(index * Mathf.PI / 6f)))
        .ToArray();

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Sunny Joy] Leave Play Mode before applying the test identity.");
            return;
        }

        var settings = AssetDatabase.LoadAssetAtPath<WebWrapperSettings>(SettingsPath);
        if (settings == null)
        {
            throw new InvalidOperationException("Web Wrapper settings were not found at " + SettingsPath);
        }

        EnsureFolder(ArtFolder);
        var appIcon = CreateTextureAsset("SunnyJoyAppIcon1024", 1024, 1024, Artwork.AppIcon);
        var promptIcon = CreateTextureAsset("SunnyJoyAppIcon512", 512, 512, Artwork.AppIcon);
        var foreground = CreateTextureAsset("SunnyJoyAdaptiveForeground", 1024, 1024, Artwork.AdaptiveForeground);
        var background = CreateTextureAsset("SunnyJoyAdaptiveBackground", 1024, 1024, Artwork.Background);
        var logo = CreateTextureAsset("SunnyJoyLoadingLogo", 512, 512, Artwork.Logo);
        AssetDatabase.SaveAssets();

        PlayerSettings.productName = "Sunny Joy";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);

        PlayerSettings.applicationIdentifier = BundleId;

        var defaultSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Unknown, IconKind.Any);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown,
            Enumerable.Repeat(appIcon, Math.Max(1, defaultSizes.Length)).ToArray(), IconKind.Any);
        var androidSlots = SetPlatformIcons(NamedBuildTarget.Android, appIcon, background, foreground);
        var iosSlots = SetPlatformIcons(NamedBuildTarget.iOS, appIcon, background, foreground);
        SetAppBundleWarningThreshold();

        Undo.RecordObject(settings, "Apply Sunny Joy test settings");
        var serialized = new SerializedObject(settings);
        serialized.FindProperty("configEndpoint").stringValue = "https://spinandreel.site/config.php";
        serialized.FindProperty("androidStoreId").stringValue = BundleId;
        serialized.FindProperty("iosStoreId").stringValue = AppleId;
        serialized.FindProperty("appsFlyerIosAppId").stringValue = AppleId;
        serialized.FindProperty("useMockServices").boolValue = true;
        serialized.FindProperty("loadingLogo").objectReferenceValue = logo;
        serialized.FindProperty("notificationPromptIcon").objectReferenceValue = promptIcon;
        serialized.FindProperty("loadingTitle").stringValue = "Sunny Joy";
        serialized.FindProperty("loadingBackgroundColor").colorValue = new Color32(16, 43, 78, 255);
        serialized.FindProperty("loadingTextColor").colorValue = new Color32(255, 238, 182, 255);
        serialized.FindProperty("notificationAccentColor").colorValue = new Color32(255, 163, 62, 255);
        serialized.FindProperty("notificationPromptTitle").stringValue = "Stay in the Sunny Joy loop";
        serialized.FindProperty("notificationPromptMessage").stringValue = "Allow notifications for Sunny Joy news and updates. You can change this later in Settings.";
        serialized.FindProperty("notificationChannelName").stringValue = "Sunny Joy updates";

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log("[Sunny Joy] Test identity and artwork applied. Android icon slots: " + androidSlots +
                  "; iOS icon slots: " + iosSlots +
                  ". Mock Mode remains enabled. The App Bundle warning threshold is 100 MB.");
    }

    private static int SetPlatformIcons(NamedBuildTarget target, Texture2D icon, Texture2D background, Texture2D foreground)
    {
        var count = 0;
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
        {
            var slots = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var slot in slots)
            {
                var layers = new Texture2D[slot.maxLayerCount];
                for (var layer = 0; layer < layers.Length; layer++)
                {

                    layers[layer] = target == NamedBuildTarget.Android && layers.Length > 1
                        ? (layer == 0 ? background : foreground)
                        : icon;
                }
                slot.SetTextures(layers);
                count++;
            }
            PlayerSettings.SetPlatformIcons(target, kind, slots);
        }

        if (count == 0)
        {
            Debug.LogWarning("[Sunny Joy] No " + target.TargetName + " icon slots were available. Install its Unity build support and run this menu again.");
        }
        return count;
    }

    private static void SetAppBundleWarningThreshold()
    {

        var method = typeof(PlayerSettings).GetMethod("GetSerializedObject", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var settings = method?.Invoke(null, null) as SerializedObject;
        var enabled = settings?.FindProperty("AndroidValidateAppBundleSize");
        var limit = settings?.FindProperty("AndroidAppBundleSizeToValidate");
        if (enabled == null || limit == null)
        {
            throw new InvalidOperationException("This Unity version does not expose the expected Android App Bundle size settings.");
        }
        enabled.boolValue = true;
        limit.intValue = 100;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private enum Artwork { AppIcon, AdaptiveForeground, Background, Logo }

    private static Texture2D CreateTextureAsset(string name, int width, int height, Artwork artwork)
    {
        var path = ArtFolder + "/" + name + ".asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var isNew = texture == null;
        if (isNew)
        {
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        }
        else if (texture.width != width || texture.height != height)
        {
            texture.Reinitialize(width, height, TextureFormat.RGBA32, false);
        }

        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[width * height];
        var edge = 1.25f / Math.Min(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var point = new Vector2((x + 0.5f) / width, (y + 0.5f) / height);
                var color = artwork == Artwork.AppIcon || artwork == Artwork.Background
                    ? Sky(point) : Color.clear;
                if (artwork != Artwork.Background)
                {

                    var scale = artwork == Artwork.AdaptiveForeground ? 0.84f : (artwork == Artwork.Logo ? 1.16f : 1.12f);
                    color = Composite(color, Sun((point - new Vector2(0.5f, 0.52f)) / scale, edge / scale));
                }
                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        if (isNew)
        {
            AssetDatabase.CreateAsset(texture, path);
        }
        else
        {
            EditorUtility.SetDirty(texture);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Color Sky(Vector2 point)
    {
        var color = Color.Lerp(new Color32(8, 80, 148, 255), new Color32(66, 190, 228, 255), point.y);
        var glow = Mathf.Clamp01(1f - Vector2.Distance(point, new Vector2(0.23f, 0.92f)) / 0.85f);
        return Color.Lerp(color, new Color32(137, 224, 240, 255), glow * glow * 0.27f);
    }

    private static Color Sun(Vector2 point, float edge)
    {
        var result = Color.clear;
        var gold = (Color)new Color32(255, 192, 53, 255);
        for (var ray = 0; ray < 12; ray++)
        {
            var axis = SunRays[ray];
            var start = axis * 0.245f;
            var end = axis * 0.312f;
            var along = Mathf.Clamp01(Vector2.Dot(point - start, end - start) / (end - start).sqrMagnitude);
            var closest = Vector2.Lerp(start, end, along);
            result = Composite(result, WithAlpha(gold, Coverage(Vector2.Distance(point, closest) - 0.018f, edge)));
        }

        var body = Color.Lerp(new Color32(255, 168, 44, 255), new Color32(255, 231, 122, 255), Mathf.Clamp01((point.y + 0.215f) / 0.43f));
        result = Composite(result, WithAlpha(body, Coverage(point.magnitude - 0.215f, edge)));
        var face = (Color)new Color32(87, 62, 37, 255);
        var leftEye = Coverage(Vector2.Distance(point, new Vector2(-0.066f, 0.035f)) - 0.015f, edge);
        var rightEye = Coverage(Vector2.Distance(point, new Vector2(0.066f, 0.035f)) - 0.015f, edge);
        result = Composite(result, WithAlpha(face, Mathf.Max(leftEye, rightEye)));
        if (point.y < -0.012f && Mathf.Abs(point.x) < 0.074f)
        {
            var smile = Mathf.Abs(Vector2.Distance(point, new Vector2(0, 0.004f)) - 0.079f) - 0.0075f;
            var endFade = Mathf.Clamp01((0.074f - Mathf.Abs(point.x)) / (edge * 2f));
            result = Composite(result, WithAlpha(face, Coverage(smile, edge) * endFade));
        }
        return result;
    }

    private static float Coverage(float signedDistance, float edge)
    {
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-edge, edge, signedDistance));
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Color Composite(Color under, Color over)
    {
        var alpha = over.a + under.a * (1f - over.a);
        if (alpha < 0.00001f)
        {
            return Color.clear;
        }
        var color = (over * over.a + under * (under.a * (1f - over.a))) / alpha;
        color.a = alpha;
        return color;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }
        var slash = folder.LastIndexOf('/');
        var parent = folder.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
    }
}
