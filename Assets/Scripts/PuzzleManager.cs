using FGJ2026;
using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    [SerializeField] private TextAsset[] levels;
    [SerializeField] private PuzzleGridView puzzleGridView;
    [SerializeField] private PuzzleGameLoop puzzleGameLoop;
    
    private void Start()
    {
        var gridService = new GridService(puzzleGridView);
        ServiceLocator.Instance.Add(gridService);
        ServiceLocator.Instance.Add(new LevelService(levels, GameManager.Instance.CurrentLevelIndex));
        ServiceLocator.Instance.Add(puzzleGameLoop);
        
        puzzleGameLoop.Initialize();
    }
}
