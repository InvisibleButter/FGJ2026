using FGJ2026;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private ServiceLocator _serviceLocator;
    public static GameManager Instance => _instance;
    
    private static GameManager _instance;
    private int _currentLevelIndex;
    
    public int CurrentLevelIndex => _currentLevelIndex;
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
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

    public void SelectStartingLevel(int index)
    { 
        _currentLevelIndex = index;
        
        SceneManager.LoadSceneAsync("Scenes/MainGame");
    }

    protected virtual void AddAdditionalServices() { }
    
    protected virtual void OnAllInitialized() { }
}
