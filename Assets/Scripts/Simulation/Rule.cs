using System;

namespace FGJ2026.Simulation
{
    /// <summary>
    /// One behaviour block of an animal: a basic action (move, sing) or a preference
    /// such as "Hearing Blue is good". Learned preferences start out pending and only
    /// take effect on the next day.
    /// </summary>
    public sealed class Rule
    {
        private static int nextId;

        /// <summary>Stable id, kept when the simulation copies itself (rewind), so the UI can track blocks.</summary>
        public int Id { get; }
        public RuleKind Kind { get; }
        public SenseKind Sense { get; }
        public Stimulus Stimulus { get; }
        public bool Positive { get; }
        /// <summary>True when the animal learned this during play (as opposed to being part of the level).</summary>
        public bool Learned { get; }
        /// <summary>Learned today; becomes active when the day ends.</summary>
        public bool Pending { get; internal set; }
        public int LearnedOnDay { get; }

        private Rule(int id, RuleKind kind, SenseKind sense, Stimulus stimulus, bool positive, bool learned, bool pending, int day)
        {
            Id = id;
            Kind = kind;
            Sense = sense;
            Stimulus = stimulus;
            Positive = positive;
            Learned = learned;
            Pending = pending;
            LearnedOnDay = day;
        }

        public static Rule Move() => new Rule(++nextId, RuleKind.MoveForward, default, default, true, false, false, 0);
        public static Rule Sing() => new Rule(++nextId, RuleKind.Sing, default, default, true, false, false, 0);

        public static Rule Preference(SenseKind sense, Stimulus stimulus, bool positive) =>
            new Rule(++nextId, RuleKind.Preference, sense, stimulus, positive, false, false, 0);

        internal static Rule Lesson(SenseKind sense, Stimulus stimulus, bool positive, int day) =>
            new Rule(++nextId, RuleKind.Preference, sense, stimulus, positive, true, true, day);

        internal Rule Clone() => new Rule(Id, Kind, Sense, Stimulus, Positive, Learned, Pending, LearnedOnDay);

        public bool IsAbout(SenseKind sense, Stimulus stimulus) =>
            Kind == RuleKind.Preference && Sense == sense && Stimulus.Equals(stimulus);

        public override string ToString()
        {
            switch (Kind)
            {
                case RuleKind.MoveForward: return "Move forward";
                case RuleKind.Sing: return "Sing";
                default:
                    return $"{(Sense == SenseKind.Hear ? "Hearing" : "Seeing")} {Stimulus} is {(Positive ? "good" : "bad")}";
            }
        }

        /// <summary>
        /// Parses a behaviour from a level file:
        /// "move", "sing", "hear blue good", "see red bad", "see goodSweet good", "see flag red bad".
        /// </summary>
        public static bool TryParse(string text, out Rule rule, out string error)
        {
            rule = null;
            error = null;
            string[] parts = (text ?? string.Empty).Trim().ToLowerInvariant()
                .Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
            {
                error = "a behaviour is empty.";
                return false;
            }

            switch (parts[0])
            {
                case "move":
                case "walk":
                    rule = Move();
                    return true;
                case "sing":
                    rule = Sing();
                    return true;
                case "hear":
                case "see":
                    break;
                default:
                    error = $"unknown behaviour '{text}'. Use move, sing, 'hear <colour> good|bad' or 'see <target> good|bad'.";
                    return false;
            }

            if (parts.Length < 3)
            {
                error = $"'{text}' needs a target and good or bad, e.g. 'hear blue good'.";
                return false;
            }

            string valence = parts[parts.Length - 1];
            if (valence != "good" && valence != "bad")
            {
                error = $"'{text}' must end with good or bad.";
                return false;
            }

            SenseKind sense = parts[0] == "hear" ? SenseKind.Hear : SenseKind.See;
            if (!TryParseStimulus(parts, 1, parts.Length - 1, out Stimulus stimulus))
            {
                error = $"'{text}': unknown target. Use a colour, goodSweet, badSweet or 'flag <colour>'.";
                return false;
            }

            if (sense == SenseKind.Hear && stimulus.Kind != StimulusKind.Animal)
            {
                error = $"'{text}': only animals can be heard.";
                return false;
            }

            rule = Preference(sense, stimulus, valence == "good");
            return true;
        }

        private static bool TryParseStimulus(string[] parts, int from, int to, out Stimulus stimulus)
        {
            stimulus = default;
            int count = to - from;

            if (count == 2 && parts[from] == "flag" && Names.TryParseColor(parts[from + 1], out AnimalColor flagColor))
            {
                stimulus = Stimulus.OfFlag(flagColor);
                return true;
            }
            if (count == 2 && parts[from + 1] == "flag" && Names.TryParseColor(parts[from], out flagColor))
            {
                stimulus = Stimulus.OfFlag(flagColor);
                return true;
            }
            if (count == 1 && Names.TryParseColor(parts[from], out AnimalColor color))
            {
                stimulus = Stimulus.OfAnimal(color);
                return true;
            }
            if (Names.TryParseItem(string.Join("", parts, from, count), out ItemType item))
            {
                stimulus = Stimulus.OfItem(item);
                return true;
            }
            return false;
        }
    }
}
