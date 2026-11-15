using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FGJ2026.Simulation
{
    public enum StarKind { Solve, MaxTicks, MaxItems, MaxDays }

    public sealed class StarCondition
    {
        public StarKind Kind { get; }
        public int Value { get; }
        public string Description { get; }

        public StarCondition(StarKind kind, int value, string description)
        {
            Kind = kind;
            Value = value;
            Description = string.IsNullOrWhiteSpace(description) ? DefaultDescription(kind, value) : description.Trim();
        }

        public static string DefaultDescription(StarKind kind, int value)
        {
            switch (kind)
            {
                case StarKind.Solve: return "Solve the puzzle";
                case StarKind.MaxTicks: return $"Solve it in at most {value} ticks";
                case StarKind.MaxItems: return value == 1 ? "Use at most 1 item" : $"Use at most {value} items";
                case StarKind.MaxDays: return value == 1 ? "Solve it on the first day" : $"Solve it within {value} days";
                default: return kind.ToString();
            }
        }

        /// <param name="totalTicks">Ticks over all days played.</param>
        /// <param name="itemsUsed">Items placed over all days.</param>
        /// <param name="daysUsed">Days needed, including the winning day.</param>
        public bool IsMet(int totalTicks, int itemsUsed, int daysUsed)
        {
            switch (Kind)
            {
                case StarKind.Solve: return true;
                case StarKind.MaxTicks: return totalTicks <= Value;
                case StarKind.MaxItems: return itemsUsed <= Value;
                case StarKind.MaxDays: return daysUsed <= Value;
                default: return false;
            }
        }
    }

    public readonly struct AnimalSetup
    {
        public AnimalColor Color { get; }
        public Vector2Int Cell { get; }
        public Vector2Int Facing { get; }
        public IReadOnlyList<Rule> Rules { get; }

        public AnimalSetup(AnimalColor color, Vector2Int cell, Vector2Int facing, IReadOnlyList<Rule> rules)
        {
            Color = color;
            Cell = cell;
            Facing = facing;
            Rules = rules;
        }
    }

    public readonly struct FlagSetup
    {
        public AnimalColor Color { get; }
        public Vector2Int Cell { get; }

        public FlagSetup(AnimalColor color, Vector2Int cell)
        {
            Color = color;
            Cell = cell;
        }
    }

    /// <summary>One step of a level's reference solution (used by tests to prove a level can be solved).</summary>
    public readonly struct PlannedPlacement
    {
        public int Day { get; }
        public ItemType Item { get; }
        public Vector2Int Cell { get; }

        public PlannedPlacement(int day, ItemType item, Vector2Int cell)
        {
            Day = day;
            Item = item;
            Cell = cell;
        }
    }

    /// <summary>A validated, read-only puzzle loaded from a level JSON file.</summary>
    public sealed class LevelDefinition
    {
        public const int MaxSize = 32;
        public const int MaxStars = 3;

        private readonly HashSet<Vector2Int> blocked;
        private readonly Dictionary<Vector2Int, FlagSetup> flagsByCell;
        private readonly Dictionary<ItemType, int> itemCounts;

        public string Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int Days { get; }
        public int TicksPerDay { get; }
        public IReadOnlyList<AnimalSetup> Animals { get; }
        public IReadOnlyList<FlagSetup> Flags { get; }
        public IReadOnlyCollection<Vector2Int> Blocked => blocked;
        /// <summary>Item types in the bag, in the order the level lists them.</summary>
        public IReadOnlyList<ItemType> ItemTypes { get; }
        public IReadOnlyList<StarCondition> Stars { get; }
        public IReadOnlyList<PlannedPlacement> Solution { get; }

        private LevelDefinition(string id, string name, int width, int height, int days, int ticksPerDay,
            List<AnimalSetup> animals, List<FlagSetup> flags, HashSet<Vector2Int> blocked,
            List<ItemType> itemTypes, Dictionary<ItemType, int> itemCounts, List<StarCondition> stars,
            List<PlannedPlacement> solution)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            Days = days;
            TicksPerDay = ticksPerDay;
            Animals = animals.AsReadOnly();
            Flags = flags.AsReadOnly();
            this.blocked = blocked;
            flagsByCell = flags.ToDictionary(f => f.Cell);
            ItemTypes = itemTypes.AsReadOnly();
            this.itemCounts = itemCounts;
            Stars = stars.AsReadOnly();
            Solution = solution.AsReadOnly();
        }

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        public bool IsBlocked(Vector2Int cell) => blocked.Contains(cell);
        public bool TryGetFlag(Vector2Int cell, out FlagSetup flag) => flagsByCell.TryGetValue(cell, out flag);
        public int GetItemCount(ItemType type) => itemCounts.TryGetValue(type, out int count) ? count : 0;
        public int TotalItems => itemCounts.Values.Sum();

        public static bool TryParse(string json, string fallbackId, out LevelDefinition level, out string error)
        {
            level = null;
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    throw new FormatException("the file is empty.");

                LevelFile data = JsonUtility.FromJson<LevelFile>(json);
                if (data == null)
                    throw new FormatException("the file is not a level.");

                level = Build(data, fallbackId);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static LevelDefinition Build(LevelFile data, string fallbackId)
        {
            string id = string.IsNullOrWhiteSpace(data.id) ? fallbackId : data.id.Trim();
            if (string.IsNullOrWhiteSpace(id))
                throw new FormatException("the level needs an id.");
            string name = string.IsNullOrWhiteSpace(data.name) ? id : data.name.Trim();

            if (data.width < 1 || data.width > MaxSize || data.height < 1 || data.height > MaxSize)
                throw new FormatException($"width and height must be between 1 and {MaxSize}.");
            if (data.days < 1)
                throw new FormatException("days must be at least 1.");
            if (data.ticksPerDay < 1)
                throw new FormatException("ticksPerDay must be at least 1.");

            Vector2Int ReadCell(CellEntry entry, string field)
            {
                if (entry == null || entry.x < 0 || entry.y < 0 || entry.x >= data.width || entry.y >= data.height)
                    throw new FormatException($"{field} must be a square inside the board (x 0-{data.width - 1}, y 0-{data.height - 1}).");
                return new Vector2Int(entry.x, entry.y);
            }

            // Blocked squares
            var blocked = new HashSet<Vector2Int>();
            CellEntry[] blockedEntries = data.blocked ?? Array.Empty<CellEntry>();
            for (int i = 0; i < blockedEntries.Length; i++)
                blocked.Add(ReadCell(blockedEntries[i], $"blocked[{i}]"));

            // Animals
            AnimalEntry[] animalEntries = data.animals ?? Array.Empty<AnimalEntry>();
            if (animalEntries.Length == 0)
                throw new FormatException("the level needs at least one animal.");

            var animals = new List<AnimalSetup>();
            var occupied = new HashSet<Vector2Int>();
            for (int i = 0; i < animalEntries.Length; i++)
            {
                AnimalEntry entry = animalEntries[i];
                string field = $"animals[{i}]";
                if (entry == null)
                    throw new FormatException($"{field} is empty.");
                if (!Names.TryParseColor(entry.color, out AnimalColor color))
                    throw new FormatException($"{field}.color '{entry.color}' is not one of {string.Join(", ", Enum.GetNames(typeof(AnimalColor)))}.");

                Vector2Int cell = ReadCell(entry, field);
                if (blocked.Contains(cell))
                    throw new FormatException($"{field} stands on a blocked square.");
                if (!occupied.Add(cell))
                    throw new FormatException($"{field} shares its square with another animal.");
                if (!Directions.TryParse(entry.facing, out Vector2Int facing))
                    throw new FormatException($"{field}.facing must be north, east, south or west.");

                var rules = new List<Rule>();
                foreach (string text in entry.behaviours ?? Array.Empty<string>())
                {
                    if (!Rule.TryParse(text, out Rule rule, out string ruleError))
                        throw new FormatException($"{field}: {ruleError}");
                    rules.Add(rule);
                }

                animals.Add(new AnimalSetup(color, cell, facing, rules.AsReadOnly()));
            }

            // Flags
            FlagEntry[] flagEntries = data.flags ?? Array.Empty<FlagEntry>();
            if (flagEntries.Length == 0)
                throw new FormatException("the level needs at least one flag.");

            var flags = new List<FlagSetup>();
            var flagCells = new HashSet<Vector2Int>();
            for (int i = 0; i < flagEntries.Length; i++)
            {
                FlagEntry entry = flagEntries[i];
                string field = $"flags[{i}]";
                if (entry == null)
                    throw new FormatException($"{field} is empty.");
                if (!Names.TryParseColor(entry.color, out AnimalColor color))
                    throw new FormatException($"{field}.color '{entry.color}' is not a known colour.");

                Vector2Int cell = ReadCell(entry, field);
                if (blocked.Contains(cell) || occupied.Contains(cell))
                    throw new FormatException($"{field} must be on a free square (not blocked, no animal).");
                if (!flagCells.Add(cell))
                    throw new FormatException($"{field} shares its square with another flag.");
                if (animals.All(a => a.Color != color))
                    throw new FormatException($"{field}: there is no {color} animal for this flag.");

                flags.Add(new FlagSetup(color, cell));
            }

            // Item bag
            var itemTypes = new List<ItemType>();
            var itemCounts = new Dictionary<ItemType, int>();
            ItemEntry[] itemEntries = data.items ?? Array.Empty<ItemEntry>();
            for (int i = 0; i < itemEntries.Length; i++)
            {
                ItemEntry entry = itemEntries[i];
                if (entry == null || !Names.TryParseItem(entry.type, out ItemType type))
                    throw new FormatException($"items[{i}].type must be goodSweet or badSweet.");
                if (entry.count < 0)
                    throw new FormatException($"items[{i}].count must not be negative.");

                if (!itemCounts.ContainsKey(type))
                {
                    itemTypes.Add(type);
                    itemCounts[type] = 0;
                }
                itemCounts[type] += entry.count;
            }

            // Stars
            var stars = new List<StarCondition>();
            StarEntry[] starEntries = data.stars ?? Array.Empty<StarEntry>();
            for (int i = 0; i < starEntries.Length; i++)
            {
                StarEntry entry = starEntries[i];
                if (entry == null || !TryParseStarKind(entry.type, out StarKind kind))
                    throw new FormatException($"stars[{i}].type must be solve, maxTicks, maxItems or maxDays.");
                if (kind != StarKind.Solve && entry.value < 0)
                    throw new FormatException($"stars[{i}].value must not be negative.");
                stars.Add(new StarCondition(kind, entry.value, entry.description));
            }

            // One star is always for solving the puzzle; keep it first.
            stars.RemoveAll(s => s.Kind == StarKind.Solve);
            stars.Insert(0, new StarCondition(StarKind.Solve, 0,
                starEntries.FirstOrDefault(s => s != null && TryParseStarKind(s.type, out StarKind k) && k == StarKind.Solve)?.description));
            if (stars.Count > MaxStars)
                throw new FormatException($"a level can have at most {MaxStars} stars (one is always 'solve').");

            // Reference solution (optional)
            var solution = new List<PlannedPlacement>();
            PlacementEntry[] placementEntries = data.solution ?? Array.Empty<PlacementEntry>();
            for (int i = 0; i < placementEntries.Length; i++)
            {
                PlacementEntry entry = placementEntries[i];
                string field = $"solution[{i}]";
                if (entry == null || !Names.TryParseItem(entry.item, out ItemType item))
                    throw new FormatException($"{field}.item must be goodSweet or badSweet.");
                if (entry.day < 1 || entry.day > data.days)
                    throw new FormatException($"{field}.day must be between 1 and {data.days}.");
                solution.Add(new PlannedPlacement(entry.day, item, ReadCell(entry, field)));
            }

            return new LevelDefinition(id, name, data.width, data.height, data.days, data.ticksPerDay,
                animals, flags, blocked, itemTypes, itemCounts, stars, solution);
        }

        private static bool TryParseStarKind(string text, out StarKind kind)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "solve": case "solved": kind = StarKind.Solve; return true;
                case "maxticks": case "ticks": kind = StarKind.MaxTicks; return true;
                case "maxitems": case "items": case "maxsweets": kind = StarKind.MaxItems; return true;
                case "maxdays": case "days": kind = StarKind.MaxDays; return true;
                default: kind = default; return false;
            }
        }
    }

    // ---- JSON file format (see Assets/Docs/LevelFormat.md) ----

    [Serializable]
    public sealed class LevelFile
    {
        public string id;
        public string name;
        public int width;
        public int height;
        public int days = 1;
        public int ticksPerDay = 24;
        public AnimalEntry[] animals;
        public FlagEntry[] flags;
        public CellEntry[] blocked;
        public ItemEntry[] items;
        public StarEntry[] stars;
        public PlacementEntry[] solution;
    }

    [Serializable]
    public class CellEntry
    {
        public int x = -1;
        public int y = -1;
    }

    [Serializable]
    public sealed class AnimalEntry : CellEntry
    {
        public string color;
        public string facing;
        public string[] behaviours;
    }

    [Serializable]
    public sealed class FlagEntry : CellEntry
    {
        public string color;
    }

    [Serializable]
    public sealed class ItemEntry
    {
        public string type;
        public int count;
    }

    [Serializable]
    public sealed class StarEntry
    {
        public string type;
        public int value;
        public string description;
    }

    [Serializable]
    public sealed class PlacementEntry : CellEntry
    {
        public int day = 1;
        public string item;
    }
}
