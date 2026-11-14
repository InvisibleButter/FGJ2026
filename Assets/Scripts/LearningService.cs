using UnityEngine;

public class LearningService : MonoBehaviour, IService
{
    
    public void Initialize()
    {
        IsInitialized = true;
    }

    public void DeInitialize()
    {
        IsInitialized = false;
    }

    public bool IsInitialized { get; set; }
}
