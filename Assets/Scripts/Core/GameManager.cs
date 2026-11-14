using System;
using FGJ2026;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private ServiceLocator _serviceLocator;
    public static GameManager Instance => _instance;
    
    private static GameManager _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            _instance = this;
        }
        
        RegisterServices();
    }
    
    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }
    
    private void Update()
    {
        #if UNITY_EDITOR
        if (Keyboard.current?.spaceKey.wasPressedThisFrame == true)
        {
            ServiceLocator.Instance.GetService<LevelService>().LoadNextLevel();
        }
        #endif
    }

    private void RegisterServices()
    {
        _serviceLocator = ServiceLocator.Instance;
        _serviceLocator.OnAllInitilized += OnAllInitialized;

        var services = GetComponentsInChildren<IService>();
        foreach(var service in services)
        {
            _serviceLocator.Add(service);
        }
        
        _serviceLocator.InitializeAll();
    }

    public void SelectStartingLevel()
    { 
        SceneManager.LoadSceneAsync("MainGame");
    }

    protected virtual void AddAdditionalServices() { }
    
    protected virtual void OnAllInitialized() { }
}
