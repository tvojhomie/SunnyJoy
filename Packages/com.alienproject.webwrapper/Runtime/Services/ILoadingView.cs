namespace AlienProject.WebWrapper
{

    public interface ILoadingView
    {
        bool IsAvailable { get; }
        void Show();
        void Hide();
    }
}
