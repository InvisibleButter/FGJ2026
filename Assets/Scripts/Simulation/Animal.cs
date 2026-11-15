using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FGJ2026.Simulation
{
    public sealed class Animal
    {
        internal readonly List<Rule> rules;

        /// <summary>Position in the level's animal list. Stays the same for the whole puzzle.</summary>
        public int Index { get; }
        public AnimalColor Color { get; }
        public Vector2Int StartCell { get; }
        public Vector2Int StartFacing { get; }

        public Vector2Int Cell { get; internal set; }
        public Vector2Int Facing { get; internal set; }
        public bool OffMap { get; internal set; }
        public bool OnFlag { get; internal set; }
        public AnimalAction LastAction { get; internal set; }

        /// <summary>Still taking part in the current day (on the map and not yet at its flag).</summary>
        public bool IsActive => !OffMap && !OnFlag;
        public IReadOnlyList<Rule> Rules => rules;
        public bool CanMove => rules.Any(r => r.Kind == RuleKind.MoveForward);
        public bool Sings => rules.Any(r => r.Kind == RuleKind.Sing);
        public bool OnlySings => rules.Count > 0 && rules.All(r => r.Kind == RuleKind.Sing);

        internal Animal(int index, AnimalColor color, Vector2Int startCell, Vector2Int startFacing, IEnumerable<Rule> startRules)
        {
            Index = index;
            Color = color;
            StartCell = startCell;
            StartFacing = startFacing;
            rules = startRules.Select(r => r.Clone()).ToList();
            ResetForDay();
        }

        private Animal(Animal source)
        {
            Index = source.Index;
            Color = source.Color;
            StartCell = source.StartCell;
            StartFacing = source.StartFacing;
            rules = source.rules.Select(r => r.Clone()).ToList();
            Cell = source.Cell;
            Facing = source.Facing;
            OffMap = source.OffMap;
            OnFlag = source.OnFlag;
            LastAction = source.LastAction;
        }

        internal Animal Clone() => new Animal(this);

        internal void ResetForDay()
        {
            Cell = StartCell;
            Facing = StartFacing;
            OffMap = false;
            OnFlag = false;
            LastAction = AnimalAction.None;
        }

        internal Rule FindRule(RuleKind kind) => rules.FirstOrDefault(r => r.Kind == kind);
    }
}
