using System;

namespace AlienProject.WebWrapper
{

    public interface INotificationPromptView
    {
        bool IsAvailable { get; }
        void Show(Action allow, Action skip);
        void SetBusy(bool busy);
        void Hide();
    }
}
