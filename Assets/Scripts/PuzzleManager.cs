using System.Collections.Generic;
using FGJ2026;
using UnityEngine;

public class PuzzleManager : MonoBehaviour, IService
{
    [SerializeField] private PuzzleGridView puzzleGridView;
   
    private LevelService _levelService;
    private GridService _gridService;
    private CandyPlacementService _candyPlacement;
    private AnimalMovement _animalMovement;
    private TrackedGameState _trackedGameState;
    
    public Phase CurrentPhase { get; private set; } = Phase.LoadFailed;
    
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

        var services = GetComponentsInChildren<IService>();
        foreach(var service in services)
        {
            ServiceLocator.Instance.Add(service);
        }
        
        _animalMovement = ServiceLocator.Instance.GetService<AnimalMovement>();
        _candyPlacement = ServiceLocator.Instance.GetService<CandyPlacementService>();
        
        _animalMovement.Initialize();
        _candyPlacement.Initialize();
        
        ChangePhase(Phase.Planning);
        IsInitialized = true;
    }

    public int GetRemainingCandy(CandyType type) => _trackedGameState?.GetRemainingCandy(type) ?? 0;

    public void ChangePhase(Phase nextPhase)
    {
        CurrentPhase = nextPhase;
        _gridService.CanPlaceItems = nextPhase == Phase.Planning;
        _candyPlacement.ToggleUI(nextPhase == Phase.Planning);
        
        switch (nextPhase)
        {
            case Phase.Planning:
                _animalMovement.ResetToStart();
                break;
            case Phase.Running:
                _animalMovement.StartMovement();
                break;
            case Phase.AttemptFinished:
                ChangePhase(_trackedGameState.ReduceAttemptsLeft() ? Phase.Planning : Phase.LevelFailed);
                break;
            case Phase.LevelCleared:
                if (!_levelService.LoadNextLevel())
                    ChangePhase(Phase.LoadFailed);
                break;
            case Phase.LevelFailed:
                _levelService.ReturnToMenu();
                break;
        }
    }

    internal void OnAttemptFinished(AttemptResult result)
    {
        if (CurrentPhase == Phase.Running)
            ChangePhase(result == AttemptResult.ReachedGoal ? Phase.LevelCleared : Phase.AttemptFinished);
    }

    internal void OnMapLoaded(PuzzleMap map)
    {
        _trackedGameState = new TrackedGameState(map);
        if (IsInitialized)
            ChangePhase(Phase.Planning);
    }

    internal bool TrySpendCandy(CandyType type) => _trackedGameState.TrySpendCandy(type);

    internal void RefundCandy(CandyType type) => _trackedGameState.RefundCandy(type);

    public void DeInitialize()
    {
        _animalMovement.DeInitialize();
        IsInitialized = false;
    }

    private void OnDestroy()
    {
        if (IsInitialized)
            DeInitialize();
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
    StepLimit,
    ReachedCandy,
    ReachedClue
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
