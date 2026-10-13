using UnityEngine;

namespace AlienProject.WebWrapper
{
    public sealed class UnityConnectivityService : IConnectivityService
    {
        public bool IsInternetReachable => Application.internetReachability != NetworkReachability.NotReachable;
    }
}
