using FGJ2026;
using UnityEngine;

public class PuzzleManager : MonoBehaviour, IService
{
    [SerializeField] private PuzzleGridView puzzleGridView;
    private LevelService _levelService;
    private GridService _gridService;
    
    public Phase CurrentPhase { get; private set; } 
    
    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        var gridService = new GridService(puzzleGridView);
        ServiceLocator.Instance.Add(gridService);
        
        _levelService = ServiceLocator.Instance.GetService<LevelService>();
        if (!_levelService.LoadNextLevel())
        {
            CurrentPhase = Phase.LoadFailed;
            IsInitialized = false;
            return;
        }

        IsInitialized = true;
    }

    public void DeInitialize()
    {
        IsInitialized = false;
    }

    public bool IsInitialized { get; set; }
}

public enum Phase
{
    Uninitialized,
    Planning,
    Running,
    AttemptFailed,
    LevelCleared,
    CampaignComplete,
    LoadFailed
}

public enum AttemptResult
{
    ReachedGoal,
    Blocked,
    LeftBoard,
    StepLimit
}
