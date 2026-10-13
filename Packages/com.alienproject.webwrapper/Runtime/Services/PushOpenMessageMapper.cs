using System.Collections.Generic;

namespace AlienProject.WebWrapper
{
    public static class PushOpenMessageMapper
    {
        private static readonly string[] MessageIdKeys =
        {
            "message_id",
            "google.message_id",
            "gcm.message_id"
        };

        private static readonly string[] UrlKeys =
        {
            "url",
            "link",
            "deep_link",
            "deeplink",
            "open_url"
        };

        public static PushOpenMessage FromPayload(
            bool notificationOpened,
            string messageId,
            string link,
            IDictionary<string, string> data)
        {
            if (!notificationOpened)
            {
                return null;
            }

            var resolvedMessageId = FirstValue(messageId, data, MessageIdKeys);
            if (string.IsNullOrWhiteSpace(resolvedMessageId))
            {
                return null;
            }

            var resolvedUrl = FirstValue(null, data, UrlKeys);
            if (string.IsNullOrWhiteSpace(resolvedUrl)) resolvedUrl = link ?? string.Empty;
            return new PushOpenMessage(resolvedMessageId, resolvedUrl);
        }

        private static string FirstValue(
            string preferred,
            IDictionary<string, string> data,
            IEnumerable<string> keys)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred.Trim();
            }

            if (data == null)
            {
                return string.Empty;
            }

            foreach (var key in keys)
            {
                if (data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }
    }
}
