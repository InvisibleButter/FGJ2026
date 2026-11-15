using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FGJ2026.Simulation
{
    /// <summary>A song: the squares in front of a singing animal, up to the edge of the map or a blocked square.</summary>
    public readonly struct SongLine
    {
        public Animal Singer { get; }
        /// <summary>The direction the song travels in (the singer's facing).</summary>
        public Vector2Int Direction { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }

        public SongLine(Animal singer, Vector2Int direction, IReadOnlyList<Vector2Int> cells)
        {
            Singer = singer;
            Direction = direction;
            Cells = cells;
        }
    }

    /// <summary>What an animal did in a tick (for animations).</summary>
    public enum AnimalAction { None, Move, Turn, Eat, Wait, LeaveMap, ReachFlag }

    public readonly struct RuleTrigger
    {
        public Animal Animal { get; }
        public Rule Rule { get; }

        public RuleTrigger(Animal animal, Rule rule)
        {
            Animal = animal;
            Rule = rule;
        }
    }

    public sealed class DayReport
    {
        public int Day { get; }
        public DayEndReason Reason { get; }
        /// <summary>Rules learned during the day that became active when it ended.</summary>
        public List<RuleTrigger> Confirmed { get; } = new List<RuleTrigger>();

        public DayReport(int day, DayEndReason reason)
        {
            Day = day;
            Reason = reason;
        }
    }

    public sealed class TickReport
    {
        public int Day { get; }
        public int Tick { get; }
        /// <summary>Behaviour blocks that were used this tick (for highlighting).</summary>
        public List<RuleTrigger> Triggered { get; } = new List<RuleTrigger>();
        /// <summary>New (pending) rules learned this tick.</summary>
        public List<RuleTrigger> Learned { get; } = new List<RuleTrigger>();
        public List<string> Events { get; } = new List<string>();
        /// <summary>What each animal did, by animal index. Animals that did nothing are missing.</summary>
        public Dictionary<int, AnimalAction> Actions { get; } = new Dictionary<int, AnimalAction>();
        /// <summary>Set when this tick ended the day.</summary>
        public DayReport DayEnded { get; internal set; }

        internal TickReport(int day, int tick)
        {
            Day = day;
            Tick = tick;
        }
    }

    /// <summary>
    /// The whole puzzle state and rules, independent of Unity scenes and UI.
    /// Flow: Preparation (place items) -> StartDay -> Step() once per tick -> the day ends ->
    /// Preparation of the next day, or Won / Lost.
    /// </summary>
    public sealed class PuzzleSimulation
    {
        private const int SafetyTickLimit = 100000;

        private readonly List<Animal> animals = new List<Animal>();
        private readonly Dictionary<Vector2Int, ItemType> items = new Dictionary<Vector2Int, ItemType>();
        private readonly Dictionary<ItemType, int> inventory = new Dictionary<ItemType, int>();
        private readonly HashSet<Vector2Int> placedToday = new HashSet<Vector2Int>();
        private readonly List<PlannedPlacement> placements = new List<PlannedPlacement>();
        private PuzzleSimulation dayStart;

        public LevelDefinition Level { get; }
        public PuzzlePhase Phase { get; private set; }
        /// <summary>0-based day index.</summary>
        public int DayIndex { get; private set; }
        public int DayNumber => DayIndex + 1;
        /// <summary>Ticks elapsed in the current day.</summary>
        public int Tick { get; private set; }
        /// <summary>Ticks elapsed over all days.</summary>
        public int TotalTicks { get; private set; }
        /// <summary>Items placed over all days.</summary>
        public int ItemsUsed { get; private set; }
        public DayReport LastDay { get; private set; }

        public IReadOnlyList<Animal> Animals => animals;
        public IReadOnlyDictionary<Vector2Int, ItemType> Items => items;
        /// <summary>Every item placed so far, with its day: a winning run can be stored as a level's solution.</summary>
        public IReadOnlyList<PlannedPlacement> Placements => placements;
        public bool IsFinished => Phase == PuzzlePhase.Won || Phase == PuzzlePhase.Lost;

        public PuzzleSimulation(LevelDefinition level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            for (int i = 0; i < level.Animals.Count; i++)
            {
                AnimalSetup setup = level.Animals[i];
                animals.Add(new Animal(i, setup.Color, setup.Cell, setup.Facing, setup.Rules));
            }
            foreach (ItemType type in level.ItemTypes)
                inventory[type] = level.GetItemCount(type);

            BeginPreparation();
        }

        /// <summary>Snapshot constructor.</summary>
        private PuzzleSimulation(PuzzleSimulation source)
        {
            Level = source.Level;
            CopyFrom(source);
        }

        public PuzzleSimulation CreateSnapshot() => new PuzzleSimulation(this);

        private void CopyFrom(PuzzleSimulation source)
        {
            animals.Clear();
            animals.AddRange(source.animals.Select(a => a.Clone()));
            items.Clear();
            foreach (var pair in source.items)
                items[pair.Key] = pair.Value;
            inventory.Clear();
            foreach (var pair in source.inventory)
                inventory[pair.Key] = pair.Value;
            placedToday.Clear();
            placedToday.UnionWith(source.placedToday);
            placements.Clear();
            placements.AddRange(source.placements);

            Phase = source.Phase;
            DayIndex = source.DayIndex;
            Tick = source.Tick;
            TotalTicks = source.TotalTicks;
            ItemsUsed = source.ItemsUsed;
            LastDay = source.LastDay;
        }

        // ---------------------------------------------------------------- Preparation

        public int GetRemaining(ItemType type) => inventory.TryGetValue(type, out int count) ? count : 0;

        public bool IsPlacedToday(Vector2Int cell) => placedToday.Contains(cell);

        public Animal AnimalAt(Vector2Int cell) => animals.FirstOrDefault(a => !a.OffMap && a.Cell == cell);

        /// <summary>Items can go on any free square without an animal or a flag, during preparation.</summary>
        public bool CanPlaceAt(Vector2Int cell) =>
            Phase == PuzzlePhase.Preparation && Level.Contains(cell) && !Level.IsBlocked(cell) &&
            !Level.TryGetFlag(cell, out _) && !items.ContainsKey(cell) && AnimalAt(cell) == null;

        public bool TryPlaceItem(Vector2Int cell, ItemType type)
        {
            if (!CanPlaceAt(cell) || GetRemaining(type) <= 0)
                return false;

            inventory[type]--;
            items[cell] = type;
            placedToday.Add(cell);
            placements.Add(new PlannedPlacement(DayNumber, type, cell));
            ItemsUsed++;
            return true;
        }

        /// <summary>Takes back an item placed during today's preparation.</summary>
        public bool TryRemoveItem(Vector2Int cell)
        {
            if (Phase != PuzzlePhase.Preparation || !placedToday.Contains(cell) || !items.TryGetValue(cell, out ItemType type))
                return false;

            items.Remove(cell);
            placedToday.Remove(cell);
            int logged = placements.FindLastIndex(p => p.Day == DayNumber && p.Cell == cell);
            if (logged >= 0)
                placements.RemoveAt(logged);
            inventory[type]++;
            ItemsUsed--;
            return true;
        }

        public bool StartDay()
        {
            if (Phase != PuzzlePhase.Preparation)
                return false;
            Phase = PuzzlePhase.Running;
            return true;
        }

        /// <summary>Back to the start of the current day's preparation (before today's items were placed).</summary>
        public void Rewind()
        {
            if (dayStart != null)
                CopyFrom(dayStart);
        }

        // ---------------------------------------------------------------- Simulation

        /// <summary>Simulates one tick. Each active animal does exactly one thing: move, turn 90°, eat, or nothing.</summary>
        public TickReport Step()
        {
            if (Phase != PuzzlePhase.Running)
                return null;

            Tick++;
            TotalTicks++;
            var report = new TickReport(DayNumber, Tick);

            // Senses use the state at the start of the tick.
            List<SongLine> songs = GetSongs();
            foreach (SongLine song in songs)
                report.Triggered.Add(new RuleTrigger(song.Singer, song.Singer.FindRule(RuleKind.Sing)));

            foreach (Animal animal in animals)
            {
                if (animal.IsActive)
                    Act(animal, songs, report);
                report.Actions.TryGetValue(animal.Index, out AnimalAction action);
                animal.LastAction = action;
            }

            if (Level.Flags.All(IsFlagReached))
                EndDay(DayEndReason.GoalReached, report);
            else if (Tick >= Level.TicksPerDay)
                EndDay(DayEndReason.TickLimit, report);
            else if (animals.Any(a => a.CanMove) && animals.Where(a => a.CanMove).All(a => !a.IsActive))
                EndDay(DayEndReason.AnimalsDone, report);

            return report;
        }

        /// <summary>Runs the rest of the current day at once (the Skip button). Returns the last tick's report.</summary>
        public TickReport RunToEndOfDay()
        {
            TickReport last = null;
            int guard = 0;
            while (Phase == PuzzlePhase.Running && guard++ < SafetyTickLimit)
                last = Step();
            return last;
        }

        public List<SongLine> GetSongs()
        {
            var songs = new List<SongLine>();
            foreach (Animal animal in animals)
            {
                if (animal.OffMap || !animal.Sings)
                    continue;

                var cells = new List<Vector2Int>();
                for (Vector2Int cell = animal.Cell + animal.Facing; Level.Contains(cell) && !Level.IsBlocked(cell); cell += animal.Facing)
                    cells.Add(cell);
                songs.Add(new SongLine(animal, animal.Facing, cells));
            }
            return songs;
        }

        /// <summary>What the animal sees: the first animal, item or flag straight ahead.</summary>
        public Stimulus? Look(Animal animal)
        {
            for (Vector2Int cell = animal.Cell + animal.Facing; Level.Contains(cell) && !Level.IsBlocked(cell); cell += animal.Facing)
            {
                Animal other = AnimalAt(cell);
                if (other != null)
                    return Stimulus.OfAnimal(other.Color);
                if (items.TryGetValue(cell, out ItemType item))
                    return Stimulus.OfItem(item);
                if (Level.TryGetFlag(cell, out FlagSetup flag))
                    return Stimulus.OfFlag(flag.Color);
            }
            return null;
        }

        public IEnumerable<SongLine> HeardBy(Animal animal, IEnumerable<SongLine> songs) =>
            songs.Where(s => s.Singer != animal && s.Cells.Contains(animal.Cell));

        public bool IsFlagReached(FlagSetup flag) => animals.Any(a => a.OnFlag && a.Cell == flag.Cell);

        /// <summary>Which of the level's stars this (won) run earns. All false unless the puzzle is won.</summary>
        public bool[] EvaluateStars()
        {
            var earned = new bool[Level.Stars.Count];
            if (Phase != PuzzlePhase.Won)
                return earned;
            for (int i = 0; i < earned.Length; i++)
                earned[i] = Level.Stars[i].IsMet(TotalTicks, ItemsUsed, DayNumber);
            return earned;
        }

        private void Act(Animal animal, List<SongLine> songs, TickReport report)
        {
            List<SongLine> heard = HeardBy(animal, songs).ToList();
            Stimulus? seen = Look(animal);

            // 1. Eating takes the whole tick, and the animal remembers what it senses meanwhile.
            if (items.TryGetValue(animal.Cell, out ItemType item))
            {
                items.Remove(animal.Cell);
                report.Actions[animal.Index] = AnimalAction.Eat;
                report.Events.Add($"{animal.Color} eats a {Names.Of(item)}.");
                Learn(animal, item == ItemType.GoodSweet, heard, seen, report);
                return;
            }

            // 2. Preferences, newest first. Pending ones only work from tomorrow.
            for (int i = animal.rules.Count - 1; i >= 0; i--)
            {
                Rule rule = animal.rules[i];
                if (rule.Kind != RuleKind.Preference || rule.Pending)
                    continue;

                if (rule.Sense == SenseKind.Hear)
                {
                    foreach (SongLine song in heard)
                    {
                        if (!Stimulus.OfAnimal(song.Singer.Color).Equals(rule.Stimulus))
                            continue;

                        report.Triggered.Add(new RuleTrigger(animal, rule));
                        // Good: head towards the singer (against the song). Bad: head away (along the song).
                        Vector2Int desired = rule.Positive ? Directions.Opposite(song.Direction) : song.Direction;
                        Head(animal, desired, report);
                        return;
                    }
                }
                else if (seen.HasValue && seen.Value.Equals(rule.Stimulus))
                {
                    report.Triggered.Add(new RuleTrigger(animal, rule));
                    // Seeing something good: keep going. Seeing something bad: turn right (90°).
                    if (rule.Positive)
                        MoveForward(animal, report);
                    else
                        Turn(animal, Directions.TurnRight(animal.Facing), report);
                    return;
                }
            }

            // 3. Basic behaviour.
            MoveForward(animal, report);
        }

        private void Head(Animal animal, Vector2Int desired, TickReport report)
        {
            if (animal.Facing == desired)
                MoveForward(animal, report);
            else
                Turn(animal, Directions.TurnLeft(animal.Facing) == desired ? desired : Directions.TurnRight(animal.Facing), report);
        }

        private static void Turn(Animal animal, Vector2Int facing, TickReport report)
        {
            animal.Facing = facing;
            report.Actions[animal.Index] = AnimalAction.Turn;
            report.Events.Add($"{animal.Color} turns {Directions.Name(facing)}.");
        }

        private void MoveForward(Animal animal, TickReport report)
        {
            Rule move = animal.FindRule(RuleKind.MoveForward);
            if (move == null)
                return;

            report.Triggered.Add(new RuleTrigger(animal, move));
            Vector2Int next = animal.Cell + animal.Facing;

            if (!Level.Contains(next))
            {
                animal.OffMap = true;
                report.Actions[animal.Index] = AnimalAction.LeaveMap;
                report.Events.Add($"{animal.Color} walks off the map.");
                return;
            }
            if (Level.IsBlocked(next))
            {
                report.Actions[animal.Index] = AnimalAction.Wait;
                report.Events.Add($"{animal.Color} bumps into an obstacle.");
                return;
            }
            Animal other = AnimalAt(next);
            if (other != null)
            {
                report.Actions[animal.Index] = AnimalAction.Wait;
                report.Events.Add($"{animal.Color} waits behind {other.Color}.");
                return;
            }

            animal.Cell = next;
            report.Actions[animal.Index] = AnimalAction.Move;
            if (Level.TryGetFlag(next, out FlagSetup flag) && flag.Color == animal.Color)
            {
                animal.OnFlag = true;
                report.Actions[animal.Index] = AnimalAction.ReachFlag;
                report.Events.Add($"{animal.Color} reaches its flag!");
            }
        }

        private void Learn(Animal animal, bool positive, List<SongLine> heard, Stimulus? seen, TickReport report)
        {
            var lessons = new List<(SenseKind sense, Stimulus stimulus)>();
            foreach (SongLine song in heard)
            {
                var lesson = (SenseKind.Hear, Stimulus.OfAnimal(song.Singer.Color));
                if (!lessons.Contains(lesson))
                    lessons.Add(lesson);
            }
            if (seen.HasValue)
                lessons.Add((SenseKind.See, seen.Value));

            if (lessons.Count == 0)
            {
                report.Events.Add($"{animal.Color} didn't notice anything while eating.");
                return;
            }

            foreach (var (sense, stimulus) in lessons)
            {
                if (animal.rules.Any(r => !r.Pending && r.IsAbout(sense, stimulus) && r.Positive == positive))
                    continue; // already knows this

                animal.rules.RemoveAll(r => r.Pending && r.IsAbout(sense, stimulus));
                Rule rule = Rule.Lesson(sense, stimulus, positive, DayNumber);
                animal.rules.Add(rule);
                report.Learned.Add(new RuleTrigger(animal, rule));
                report.Events.Add($"{animal.Color} will remember \"{rule}\" from tomorrow.");
            }
        }

        private void EndDay(DayEndReason reason, TickReport report)
        {
            var day = new DayReport(DayNumber, reason);

            foreach (Animal animal in animals)
            {
                foreach (Rule lesson in animal.rules.Where(r => r.Pending).ToList())
                {
                    // A new lesson replaces an older feeling about the same thing.
                    animal.rules.RemoveAll(r => !r.Pending && r.IsAbout(lesson.Sense, lesson.Stimulus));
                    lesson.Pending = false;
                    day.Confirmed.Add(new RuleTrigger(animal, lesson));
                }
            }

            LastDay = day;
            report.DayEnded = day;

            if (reason == DayEndReason.GoalReached)
            {
                Phase = PuzzlePhase.Won;
                return;
            }
            if (DayIndex + 1 >= Level.Days)
            {
                Phase = PuzzlePhase.Lost;
                return;
            }

            DayIndex++;
            BeginPreparation();
        }

        private void BeginPreparation()
        {
            foreach (Animal animal in animals)
                animal.ResetForDay();
            Tick = 0;
            placedToday.Clear();
            Phase = PuzzlePhase.Preparation;
            dayStart = new PuzzleSimulation(this);
        }
    }
}
