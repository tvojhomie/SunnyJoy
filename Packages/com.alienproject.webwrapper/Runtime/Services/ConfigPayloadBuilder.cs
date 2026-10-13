using System.Collections.Generic;

namespace AlienProject.WebWrapper
{
    public static class ConfigPayloadBuilder
    {
        public static ConfigRequest Build(
            AttributionSnapshot attribution,
            PushRegistration pushRegistration,
            AppIdentity identity)
        {
            var values = new Dictionary<string, object>();
            attribution = attribution ?? AttributionSnapshot.Empty;
            pushRegistration = pushRegistration ?? PushRegistration.Empty;

            foreach (var payload in attribution.Payloads)
            {
                if (payload?.Values == null)
                {
                    continue;
                }

                foreach (var pair in payload.Values)
                {
                    if (!values.ContainsKey(pair.Key))
                    {
                        values.Add(pair.Key, pair.Value);
                    }
                }
            }

            values.Remove("push_token");
            values.Remove("firebase_project_id");

            if (identity != null)
            {
                values["af_id"] = attribution.AppsFlyerId;
                values["bundle_id"] = identity.BundleId;
                values["os"] = identity.OperatingSystem;
                values["store_id"] = identity.StoreId;
                values["locale"] = identity.Locale;
            }

            if (pushRegistration.IsComplete)
            {
                values["push_token"] = pushRegistration.Token;
                values["firebase_project_id"] = pushRegistration.FirebaseProjectId;
            }

            return new ConfigRequest(values);
        }
    }
}
