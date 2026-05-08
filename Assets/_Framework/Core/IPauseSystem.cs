namespace Reach.Framework.Core
{
    public interface IPauseSystem
    {
        bool IsPaused { get; }
        void Pause();
        void Resume();
        void Toggle();
    }
}