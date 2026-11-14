using FGJ2026;
using UnityEngine;

public class AnimalMovement : MonoBehaviour, IService
{
    [SerializeField] private GameObject animalObject;
    [SerializeField, Min(0.01f)] private float moveSpeed = 4f;
    [SerializeField, Min(0f)] private float stopDuration = 0.25f;

    private PuzzleManager _puzzleManager;
    private GridService _gridService;
    private Transform _animal;
    private Vector3 _destination;
    private AttemptResult _result;
    private float _stopTimeRemaining;

    public void StartMovement()
    {
        if (!IsInitialized || _gridService?.Map == null)
            return;

        PuzzleMap map = _gridService.Map;
        _result = FindStop(map, out Vector2Int stopCell);
        _destination = _gridService.GetCellCenterWorld(stopCell);
        _stopTimeRemaining = stopDuration;
        enabled = true;
    }

    public void ResetToStart()
    {
        enabled = false;
        if (_animal != null && _gridService?.Map != null)
            _animal.position = _gridService.GetCellCenterWorld(_gridService.Map.Start);
    }

    private AttemptResult FindStop(PuzzleMap map, out Vector2Int stopCell)
    {
        stopCell = map.Start;
        while (true)
        {
            Vector2Int nextCell = stopCell + map.StartDirection;
            if (!_gridService.TryGetCellState(nextCell, out GridCellState state))
                return AttemptResult.LeftBoard;
            if (state.IsBlocked)
                return AttemptResult.Blocked;

            stopCell = nextCell;
            if (state.IsGoal)
                return AttemptResult.ReachedGoal;
            if (state.Candy.HasValue)
                return AttemptResult.ReachedCandy;
            if (state.HasPreplacedClue || state.PlacedClueType != null)
                return AttemptResult.ReachedClue;
        }
    }

    private void Update()
    {
        if (!IsInitialized)
        {
            return;
        }
        
        if (!_animal.position.Equals(_destination))
        {
            _animal.position = Vector3.MoveTowards(_animal.position, _destination, moveSpeed * Time.deltaTime);
            return;
        }

        _stopTimeRemaining -= Time.deltaTime;
        if (_stopTimeRemaining > 0f)
            return;

        enabled = false;
        _puzzleManager.OnAttemptFinished(_result);
    }

    public void Initialize()
    {
        _animal = animalObject.transform;
        
        _gridService = ServiceLocator.Instance.GetService<GridService>();
        _puzzleManager = ServiceLocator.Instance.GetService<PuzzleManager>();
        
        enabled = false;
        IsInitialized = true;
    }

    public void DeInitialize()
    {
        IsInitialized = false;
    }

    public bool IsInitialized { get; set; }
}
