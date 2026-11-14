using UnityEngine;

public class AnimalMovement : MonoBehaviour, IService
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
