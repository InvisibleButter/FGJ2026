namespace FGJ2026
{
    public interface IService
    {
        void Initialize();

        void DeInitialize();

        bool IsInitialized { get; set; }
    }
}
