using System;

namespace AlienProject.WebWrapper
{

    public interface IWebViewLifecycleSource
    {
        event Action LoadStarted;
        event Action LoadCompleted;
        event Action<string> LoadFailed;
    }
}
