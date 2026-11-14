using System.Collections.Generic;
using FGJ2026;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PuzzleManager : MonoBehaviour, IService
{
    [SerializeField] private PuzzleGridView puzzleGridView;
    private LevelService _levelService;
    private GridService _gridService;
    private TrackedGameState _trackedGameState;
    
    public Phase CurrentPhase { get; private set; }
    
    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        _gridService = new GridService(puzzleGridView, this);
        ServiceLocator.Instance.Add(_gridService);
        
        _levelService = ServiceLocator.Instance.GetService<LevelService>();
        if (!_levelService.LoadNextLevel())
        {
            CurrentPhase = Phase.LoadFailed;
            IsInitialized = false;
            return;
        }

        ChangePhase(Phase.Planning);
        IsInitialized = true;
    }

    private void ChangePhase(Phase nextPhase)
    {
        CurrentPhase = nextPhase;
        _gridService.CanPlaceItems = false;
        
        switch (nextPhase)
        {
            case Phase.Planning:
                _gridService.CanPlaceItems = true;
                break;
            case Phase.Running:
                break;
            case Phase.AttemptFinished:
                ChangePhase(_trackedGameState.ReduceAttemptsLeft() ? Phase.Planning : Phase.LevelFailed);
                break;
            case Phase.LevelCleared:
                _levelService.LoadNextLevel();
                break;
            case Phase.LevelFailed:
                SceneManager.LoadSceneAsync("Scenes/StarterMenu");
                break;
        }
    }

    internal void OnMapLoaded(PuzzleMap map)
    {
        _trackedGameState = new TrackedGameState(map);
    }

    internal bool TrySpendCandy(CandyType type) => _trackedGameState.TrySpendCandy(type);

    internal void RefundCandy(CandyType type) => _trackedGameState.RefundCandy(type);

    public void DeInitialize()
    {
        IsInitialized = false;
    }

    public bool IsInitialized { get; set; }
}

public enum Phase
{
    Planning,
    Running,
    AttemptFinished,
    LevelCleared,
    LevelFailed,
    LoadFailed
}

public enum AttemptResult
{
    ReachedGoal,
    Blocked,
    LeftBoard,
    StepLimit
}

public class TrackedGameState
{
    public int AttemptsLeft { get; private set; }
    private readonly Dictionary<CandyType, int> _candyLeft;

    public TrackedGameState(PuzzleMap map)
    {
        AttemptsLeft = map.AttemptCount;
        _candyLeft = new Dictionary<CandyType, int>
        {
            [CandyType.Green] = map.GreenCandy,
            [CandyType.Red] = map.RedCandy
        };
    }
    
    public bool ReduceAttemptsLeft()
    {
        AttemptsLeft -= 1;
        if (AttemptsLeft < 0)
        {
            return false;
        }
        return true;
    }

    public int GetRemainingCandy(CandyType type) => _candyLeft.GetValueOrDefault(type, 0);

    internal bool TrySpendCandy(CandyType type)
    {
        int remaining = GetRemainingCandy(type);
        if (remaining == 0)
            return false;

        _candyLeft[type] = remaining - 1;
        return true;
    }

    internal void RefundCandy(CandyType type)
    {
        _candyLeft[type]++;
    }
}
