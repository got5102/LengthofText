namespace TextLength.Services
{
    public interface IStartupService
    {
        bool IsStartupEnabled();
        void EnableStartup();
        void DisableStartup();
    }
} 