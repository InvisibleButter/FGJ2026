using System;
using UnityEngine;

namespace FGJ2026.Simulation
{
    /// <summary>The colour of an animal. It is the animal's identity: how it looks and how its song sounds.</summary>
    public enum AnimalColor { Red, Blue, Green, Yellow, Purple, Orange, Pink, White }

    public enum ItemType { GoodSweet, BadSweet }

    public enum RuleKind
    {
        /// <summary>Walk one square forward per tick.</summary>
        MoveForward,
        /// <summary>Sing every tick. The song travels forward in a straight line.</summary>
        Sing,
        /// <summary>A feeling about something the animal hears or sees ("Hearing Blue is good").</summary>
        Preference
    }

    public enum SenseKind { Hear, See }

    public enum StimulusKind { Animal, Item, Flag }

    public enum PuzzlePhase { Preparation, Running, Won, Lost }

    public enum DayEndReason { GoalReached, TickLimit, AnimalsDone }

    /// <summary>Grid directions. y grows to the north.</summary>
    public static class Directions
    {
        public static readonly Vector2Int North = new Vector2Int(0, 1);
        public static readonly Vector2Int East = new Vector2Int(1, 0);
        public static readonly Vector2Int South = new Vector2Int(0, -1);
        public static readonly Vector2Int West = new Vector2Int(-1, 0);

        public static Vector2Int TurnRight(Vector2Int d) => new Vector2Int(d.y, -d.x);
        public static Vector2Int TurnLeft(Vector2Int d) => new Vector2Int(-d.y, d.x);
        public static Vector2Int Opposite(Vector2Int d) => new Vector2Int(-d.x, -d.y);

        public static bool TryParse(string text, out Vector2Int direction)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "north": case "n": case "up": direction = North; return true;
                case "east": case "e": case "right": direction = East; return true;
                case "south": case "s": case "down": direction = South; return true;
                case "west": case "w": case "left": direction = West; return true;
                default: direction = default; return false;
            }
        }

        public static string Name(Vector2Int d)
        {
            if (d == North) return "north";
            if (d == East) return "east";
            if (d == South) return "south";
            if (d == West) return "west";
            return "?";
        }

        /// <summary>Angle in degrees, counter-clockwise from east. Handy for rotating sprites.</summary>
        public static float Angle(Vector2Int d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }

    public static class Names
    {
        public static bool TryParseColor(string text, out AnimalColor color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            foreach (AnimalColor candidate in Enum.GetValues(typeof(AnimalColor)))
            {
                if (string.Equals(candidate.ToString(), text.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    color = candidate;
                    return true;
                }
            }
            return false;
        }

        public static bool TryParseItem(string text, out ItemType item)
        {
            string key = (text ?? string.Empty).Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
            switch (key)
            {
                case "goodsweet": case "goodtreat": item = ItemType.GoodSweet; return true;
                case "badsweet": case "badtreat": item = ItemType.BadSweet; return true;
                default: item = default; return false;
            }
        }

        public static string Of(ItemType item) => item == ItemType.GoodSweet ? "good sweet" : "bad sweet";
    }

    /// <summary>Something an animal can hear or see: another animal (by colour), an item, or a flag.</summary>
    public readonly struct Stimulus : IEquatable<Stimulus>
    {
        public readonly StimulusKind Kind;
        public readonly AnimalColor Color;
        public readonly ItemType Item;

        private Stimulus(StimulusKind kind, AnimalColor color, ItemType item)
        {
            Kind = kind;
            Color = color;
            Item = item;
        }

        public static Stimulus OfAnimal(AnimalColor color) => new Stimulus(StimulusKind.Animal, color, default);
        public static Stimulus OfItem(ItemType item) => new Stimulus(StimulusKind.Item, default, item);
        public static Stimulus OfFlag(AnimalColor color) => new Stimulus(StimulusKind.Flag, color, default);

        public bool Equals(Stimulus other) =>
            Kind == other.Kind && (Kind == StimulusKind.Item ? Item == other.Item : Color == other.Color);

        public override bool Equals(object obj) => obj is Stimulus other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 397) ^ (Kind == StimulusKind.Item ? (int)Item : (int)Color);

        public override string ToString()
        {
            switch (Kind)
            {
                case StimulusKind.Animal: return Color.ToString();
                case StimulusKind.Item: return Names.Of(Item);
                default: return $"{Color} flag";
            }
        }
    }
}
