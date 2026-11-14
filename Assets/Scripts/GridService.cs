using System.Collections.Generic;
using UnityEngine;

namespace FGJ2026
{
    public enum CandyType
    {
        Green,
        Red
    }

    public readonly struct GridCellState
    {
        public bool IsBlocked { get; }
        public bool IsStart { get; }
        public bool IsGoal { get; }
        public bool HasPreplacedClue { get; }
        public string PlacedClueType { get; }
        public CandyType? Candy { get; }

        internal GridCellState(bool isBlocked, bool isStart, bool isGoal, bool hasPreplacedClue,
            string placedClueType, CandyType? candy)
        {
            IsBlocked = isBlocked;
            IsStart = isStart;
            IsGoal = isGoal;
            HasPreplacedClue = hasPreplacedClue;
            PlacedClueType = placedClueType;
            Candy = candy;
        }
    }

    public sealed class GridService : IService
    {
        private readonly PuzzleGridView view;
        private readonly HashSet<Vector2Int> preplacedClueCells = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, CandyType> candies = new Dictionary<Vector2Int, CandyType>();
        private readonly Dictionary<Vector2Int, PuzzleMap.CueSource> placedClues =
            new Dictionary<Vector2Int, PuzzleMap.CueSource>();

        internal GridService(PuzzleGridView view)
        {
            this.view = view;
        }

        public PuzzleMap Map { get; private set; }
        public bool CanPlaceItems { get; internal set; }

        public void LoadMap(PuzzleMap map)
        {
            view.RenderMap(map);
            Map = map;
            candies.Clear();
            placedClues.Clear();
            preplacedClueCells.Clear();
            foreach (var cue in map.Cues)
                preplacedClueCells.Add(cue.Cell);
        }

        public bool Contains(Vector2Int cell) => Map != null && Map.Contains(cell);

        public bool TryGetCellState(Vector2Int cell, out GridCellState state)
        {
            state = default;
            if (!Contains(cell))
                return false;

            placedClues.TryGetValue(cell, out var clue);
            CandyType? candy = candies.TryGetValue(cell, out CandyType type) ? type : (CandyType?)null;
            state = new GridCellState(Map.IsBlocked(cell), Map.Start == cell, Map.Goal == cell,
                preplacedClueCells.Contains(cell), clue.Type, candy);
            return true;
        }

        public Vector3 GetCellCenterWorld(Vector2Int cell) => view.Grid.GetCellCenterWorld((Vector3Int)cell);

        public bool TryGetCellAtWorldPosition(Vector3 position, out Vector2Int cell)
        {
            cell = (Vector2Int)view.Grid.WorldToCell(position);
            return Contains(cell);
        }

        public IEnumerable<PuzzleMap.CueSource> GetCuesAt(Vector2Int cell)
        {
            if (!Contains(cell))
                yield break;

            foreach (var cue in Map.Cues)
                if (Map.IsCueAt(cue, cell))
                    yield return cue;

            foreach (var cue in placedClues.Values)
                if (Map.IsCueAt(cue, cell))
                    yield return cue;
        }

        public bool TryPlaceCandy(Vector2Int cell, CandyType type)
        {
            if ((type != CandyType.Green && type != CandyType.Red) || !CanPlaceAt(cell))
                return false;

            candies.Add(cell, type);
            view.SetCandy(cell, type);
            return true;
        }

        public bool TryRemoveCandy(Vector2Int cell)
        {
            if (!CanPlaceItems || !candies.Remove(cell))
                return false;

            view.SetCandy(cell, null);
            return true;
        }

        public bool TryPlaceClue(Vector2Int cell, string type)
        {
            if (string.IsNullOrWhiteSpace(type) || !CanPlaceAt(cell))
                return false;

            placedClues.Add(cell, new PuzzleMap.CueSource(type.Trim(), cell));
            view.RenderClues(placedClues.Values);
            return true;
        }

        public bool TryRemoveClue(Vector2Int cell)
        {
            if (!CanPlaceItems || !placedClues.Remove(cell))
                return false;

            view.RenderClues(placedClues.Values);
            return true;
        }

        private bool CanPlaceAt(Vector2Int cell)
        {
            return CanPlaceItems && TryGetCellState(cell, out GridCellState state) &&
                !state.IsBlocked && !state.IsStart && !state.IsGoal &&
                !state.HasPreplacedClue && state.PlacedClueType == null && state.Candy == null;
        }

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
}
