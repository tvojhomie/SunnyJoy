using System;

namespace AlienProject.WebWrapper
{
    public static class InteractionCallbackRequestBuilder
    {
        public static Uri BuildUri(string configEndpoint, string messageId)
        {
            if (!Uri.TryCreate(configEndpoint, UriKind.Absolute, out var configUri))
            {
                throw new ArgumentException("Config endpoint must be an absolute URI.", nameof(configEndpoint));
            }

            var basePath = configUri.AbsolutePath;
            var slashIndex = basePath.LastIndexOf('/');
            var directory = slashIndex >= 0 ? basePath.Substring(0, slashIndex + 1) : "/";
            var builder = new UriBuilder(configUri)
            {
                Path = directory + "interaction.php",
                Query = "message_id=" + Uri.EscapeDataString(messageId ?? string.Empty)
            };
            return builder.Uri;
        }

        public static string BuildBody(string appsFlyerId)
        {
            return JsonObjectWriter.Serialize(new System.Collections.Generic.Dictionary<string, object>
            {
                ["af_id"] = appsFlyerId ?? string.Empty
            });
        }
    }
}
