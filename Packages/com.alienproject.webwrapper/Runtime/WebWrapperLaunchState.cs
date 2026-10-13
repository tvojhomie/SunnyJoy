using UnityEngine;

namespace AlienProject.WebWrapper
{

    public static class WebWrapperLaunchState
    {
        public static LaunchDecision CurrentDecision { get; private set; }
        public static bool IsGameReady { get; private set; }
        public static bool CanStartGame(bool autoStartEnabled) =>
            !autoStartEnabled || CurrentDecision?.Kind == LaunchDecisionKind.Wrapper;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { CurrentDecision = null; IsGameReady = false; }

        public static void NotifyGameReady()
        {
            if (CurrentDecision?.Kind == LaunchDecisionKind.Wrapper) IsGameReady = true;
        }

        internal static void Publish(LaunchDecision decision)
        {
            if (decision?.Kind != LaunchDecisionKind.Wrapper || CurrentDecision?.Kind != LaunchDecisionKind.Wrapper)
                IsGameReady = false;
            CurrentDecision = decision;
        }
    }
}
