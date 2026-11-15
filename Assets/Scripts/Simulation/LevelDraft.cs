using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FGJ2026.Simulation
{
    /// <summary>
    /// A level being built in the level editor. Wraps the level file data and keeps it consistent:
    /// one thing per square, nothing outside the board, valid behaviours. Changes to the map clear the
    /// stored solution, because an old solution may no longer work.
    /// </summary>
    public sealed class LevelDraft
    {
        public const int MinSize = 2;
        public const int MaxTicksPerDay = 99;
        public const int MaxDays = 20;

        public LevelFile Data { get; }

        public LevelDraft(LevelFile data)
        {
            Data = Clone(data ?? new LevelFile());
            Normalise();
        }

        public static LevelDraft CreateNew() => new LevelDraft(new LevelFile
        {
            id = "",
            name = "New level",
            width = 6,
            height = 5,
            days = 2,
            ticksPerDay = 24,
            items = new[] { new ItemEntry { type = "goodSweet", count = 1 } },
            stars = new[] { new StarEntry { type = "solve" } }
        });

        public static LevelDraft FromJson(string json) => new LevelDraft(JsonUtility.FromJson<LevelFile>(json));

        public string ToJson() => JsonUtility.ToJson(Data, true);

        public static LevelFile Clone(LevelFile data) => JsonUtility.FromJson<LevelFile>(JsonUtility.ToJson(data));

        /// <summary>The playable content only (no id, name, stars or solution): tells whether a test run still fits.</summary>
        public string ContentKey()
        {
            LevelFile copy = Clone(Data);
            copy.id = copy.name = string.Empty;
            copy.stars = new StarEntry[0];
            copy.solution = new PlacementEntry[0];
            return JsonUtility.ToJson(copy);
        }

        // ------------------------------------------------------------ Queries

        public int Width => Data.width;
        public int Height => Data.height;

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Data.width && cell.y < Data.height;
        public int AnimalIndexAt(Vector2Int cell) => Array.FindIndex(Data.animals, a => a.x == cell.x && a.y == cell.y);
        public int FlagIndexAt(Vector2Int cell) => Array.FindIndex(Data.flags, f => f.x == cell.x && f.y == cell.y);
        public bool IsBlocked(Vector2Int cell) => Data.blocked.Any(b => b.x == cell.x && b.y == cell.y);
        public bool IsFree(Vector2Int cell) => Contains(cell) && !IsBlocked(cell) && AnimalIndexAt(cell) < 0 && FlagIndexAt(cell) < 0;

        public AnimalEntry Animal(int index) => index >= 0 && index < Data.animals.Length ? Data.animals[index] : null;
        public FlagEntry Flag(int index) => index >= 0 && index < Data.flags.Length ? Data.flags[index] : null;

        public int GetItemCount(ItemType type) =>
            Data.items.Where(i => Names.TryParseItem(i.type, out ItemType t) && t == type).Sum(i => i.count);

        /// <summary>The extra star in slot 1 or 2 (slot 0 is always "solve"), or null.</summary>
        public StarEntry GetStar(int slot) => ExtraStars().ElementAtOrDefault(slot - 1);

        public bool Validate(out LevelDefinition level, out string error) =>
            LevelDefinition.TryParse(JsonUtility.ToJson(Data), "draft", out level, out error);

        /// <summary>A definition that can be drawn even while the level is unfinished (no flags yet, ...).</summary>
        public LevelDefinition BuildPreview() =>
            LevelDefinition.TryParse(JsonUtility.ToJson(Data), "draft", out LevelDefinition level, out _, lenient: true) ? level : null;

        // ------------------------------------------------------------ Map edits

        public bool SetBlocked(Vector2Int cell, bool blocked)
        {
            if (!Contains(cell) || IsBlocked(cell) == blocked)
                return false;
            if (blocked && (AnimalIndexAt(cell) >= 0 || FlagIndexAt(cell) >= 0))
                return false;

            List<CellEntry> list = Data.blocked.ToList();
            if (blocked)
                list.Add(new CellEntry { x = cell.x, y = cell.y });
            else
                list.RemoveAll(b => b.x == cell.x && b.y == cell.y);
            Data.blocked = list.ToArray();
            MapChanged();
            return true;
        }

        /// <returns>The new animal's index, or -1 when the square isn't free.</returns>
        public int AddAnimal(Vector2Int cell, AnimalColor color, Vector2Int facing, params string[] behaviours)
        {
            if (!IsFree(cell))
                return -1;
            Data.animals = Data.animals.Append(new AnimalEntry
            {
                x = cell.x,
                y = cell.y,
                color = ColorName(color),
                facing = Directions.Name(facing),
                behaviours = behaviours ?? new string[0]
            }).ToArray();
            MapChanged();
            return Data.animals.Length - 1;
        }

        /// <returns>The new flag's index, or -1 when the square isn't free.</returns>
        public int AddFlag(Vector2Int cell, AnimalColor color)
        {
            if (!IsFree(cell))
                return -1;
            Data.flags = Data.flags.Append(new FlagEntry { x = cell.x, y = cell.y, color = ColorName(color) }).ToArray();
            MapChanged();
            return Data.flags.Length - 1;
        }

        /// <summary>Removes the animal or flag on a square.</summary>
        public bool RemoveAt(Vector2Int cell)
        {
            int animal = AnimalIndexAt(cell);
            if (animal >= 0)
            {
                Data.animals = Data.animals.Where((_, i) => i != animal).ToArray();
                MapChanged();
                return true;
            }
            int flag = FlagIndexAt(cell);
            if (flag >= 0)
            {
                Data.flags = Data.flags.Where((_, i) => i != flag).ToArray();
                MapChanged();
                return true;
            }
            return false;
        }

        public void RotateAnimal(int index, bool clockwise)
        {
            AnimalEntry animal = Animal(index);
            if (animal == null)
                return;
            Directions.TryParse(animal.facing, out Vector2Int facing);
            if (facing == Vector2Int.zero)
                facing = Directions.North;
            animal.facing = Directions.Name(clockwise ? Directions.TurnRight(facing) : Directions.TurnLeft(facing));
            MapChanged();
        }

        public void SetAnimalColor(int index, AnimalColor color)
        {
            AnimalEntry animal = Animal(index);
            if (animal == null)
                return;
            animal.color = ColorName(color);
            MapChanged();
        }

        public void SetFlagColor(int index, AnimalColor color)
        {
            FlagEntry flag = Flag(index);
            if (flag == null)
                return;
            flag.color = ColorName(color);
            MapChanged();
        }

        public bool AddBehaviour(int index, string text, out string error)
        {
            error = null;
            AnimalEntry animal = Animal(index);
            if (animal == null)
            {
                error = "Select an animal first.";
                return false;
            }
            if (!Rule.TryParse(text, out _, out error))
                return false;
            if (animal.behaviours.Any(b => string.Equals(b.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                error = "The animal already has this behaviour.";
                return false;
            }
            animal.behaviours = animal.behaviours.Append(text.Trim()).ToArray();
            MapChanged();
            return true;
        }

        public void RemoveBehaviour(int index, int behaviourIndex)
        {
            AnimalEntry animal = Animal(index);
            if (animal == null || behaviourIndex < 0 || behaviourIndex >= animal.behaviours.Length)
                return;
            animal.behaviours = animal.behaviours.Where((_, i) => i != behaviourIndex).ToArray();
            MapChanged();
        }

        /// <summary>Changes the board size; everything outside the new size is removed.</summary>
        public void Resize(int width, int height)
        {
            Data.width = Mathf.Clamp(width, MinSize, LevelDefinition.MaxSize);
            Data.height = Mathf.Clamp(height, MinSize, LevelDefinition.MaxSize);
            bool Inside(CellEntry c) => c.x < Data.width && c.y < Data.height;
            Data.animals = Data.animals.Where(Inside).ToArray();
            Data.flags = Data.flags.Where(Inside).ToArray();
            Data.blocked = Data.blocked.Where(Inside).ToArray();
            MapChanged();
        }

        // ------------------------------------------------------------ Settings

        public void SetId(string id) => Data.id = id;

        public void SetName(string name) => Data.name = name ?? string.Empty;

        public void SetDays(int days)
        {
            Data.days = Mathf.Clamp(days, 1, MaxDays);
            Data.solution = Data.solution.Where(p => p.day <= Data.days).ToArray();
        }

        public void SetTicksPerDay(int ticks)
        {
            Data.ticksPerDay = Mathf.Clamp(ticks, 1, MaxTicksPerDay);
            MapChanged();
        }

        public void SetItemCount(ItemType type, int count)
        {
            count = Mathf.Max(0, count);
            List<ItemEntry> items = Data.items.Where(i => !(Names.TryParseItem(i.type, out ItemType t) && t == type)).ToList();
            if (count > 0)
                items.Add(new ItemEntry { type = type == ItemType.GoodSweet ? "goodSweet" : "badSweet", count = count });
            Data.items = items.OrderBy(i => Names.TryParseItem(i.type, out ItemType t) ? (int)t : 99).ToArray();
            MapChanged();
        }

        /// <summary>Sets the extra star in slot 1 or 2. A null kind removes it.</summary>
        public void SetStar(int slot, StarKind? kind, int value)
        {
            List<StarEntry> extra = ExtraStars().ToList();
            while (extra.Count < 2)
                extra.Add(null);
            extra[slot - 1] = kind.HasValue ? new StarEntry { type = StarTypeName(kind.Value), value = value } : null;
            Data.stars = new[] { new StarEntry { type = "solve" } }.Concat(extra.Where(s => s != null)).ToArray();
        }

        public void SetSolution(IEnumerable<PlannedPlacement> placements)
        {
            Data.solution = placements.Select(p => new PlacementEntry
            {
                day = p.Day,
                item = p.Item == ItemType.GoodSweet ? "goodSweet" : "badSweet",
                x = p.Cell.x,
                y = p.Cell.y
            }).ToArray();
        }

        public bool HasSolution => Data.solution.Length > 0;

        // ------------------------------------------------------------ Helpers

        /// <summary>"Pond Watch" -> "pond-watch".</summary>
        public static string MakeId(string name)
        {
            var id = new StringBuilder();
            foreach (char c in (name ?? string.Empty).Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c) && c < 128)
                    id.Append(c);
                else if (id.Length > 0 && id[id.Length - 1] != '-')
                    id.Append('-');
            }
            string result = id.ToString().Trim('-');
            return result.Length == 0 ? "level" : result;
        }

        public static StarKind? ParseStarKind(string text)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "maxticks": case "ticks": return StarKind.MaxTicks;
                case "maxitems": case "items": case "maxsweets": return StarKind.MaxItems;
                case "maxdays": case "days": return StarKind.MaxDays;
                case "solve": case "solved": return StarKind.Solve;
                default: return null;
            }
        }

        public static string StarTypeName(StarKind kind)
        {
            switch (kind)
            {
                case StarKind.MaxTicks: return "maxTicks";
                case StarKind.MaxItems: return "maxItems";
                case StarKind.MaxDays: return "maxDays";
                default: return "solve";
            }
        }

        private static string ColorName(AnimalColor color) => color.ToString().ToLowerInvariant();

        private IEnumerable<StarEntry> ExtraStars() =>
            Data.stars.Where(s => s != null && ParseStarKind(s.type) is StarKind kind && kind != StarKind.Solve);

        private void MapChanged() => Data.solution = new PlacementEntry[0];

        private void Normalise()
        {
            Data.animals ??= new AnimalEntry[0];
            Data.flags ??= new FlagEntry[0];
            Data.blocked ??= new CellEntry[0];
            Data.items ??= new ItemEntry[0];
            Data.stars ??= new StarEntry[0];
            Data.solution ??= new PlacementEntry[0];
            Data.name ??= string.Empty;
            Data.id ??= string.Empty;
            foreach (AnimalEntry animal in Data.animals)
                animal.behaviours ??= new string[0];
            Data.width = Mathf.Clamp(Data.width, MinSize, LevelDefinition.MaxSize);
            Data.height = Mathf.Clamp(Data.height, MinSize, LevelDefinition.MaxSize);
            Data.days = Mathf.Clamp(Data.days, 1, MaxDays);
            Data.ticksPerDay = Mathf.Clamp(Data.ticksPerDay, 1, MaxTicksPerDay);
            if (!Data.stars.Any(s => s != null && ParseStarKind(s.type) == StarKind.Solve))
                Data.stars = new[] { new StarEntry { type = "solve" } }.Concat(Data.stars).ToArray();
        }
    }
}
