using FGJ2026.Simulation;

namespace FGJ2026.UI
{
    /// <summary>Rich-text wording for behaviour blocks: behaviours and colours are marked specially.</summary>
    public static class RuleText
    {
        public static string Format(Rule rule)
        {
            switch (rule.Kind)
            {
                case RuleKind.MoveForward:
                    return $"{Verb("Move")} forward";
                case RuleKind.Sing:
                    return $"{Verb("Sing")} forward";
                default:
                    string sense = rule.Sense == SenseKind.Hear ? "Hearing" : "Seeing";
                    string valence = rule.Positive
                        ? $"<b><color={Palette.Hex(Palette.Good)}>good</color></b>"
                        : $"<b><color={Palette.Hex(Palette.Bad)}>bad</color></b>";
                    return $"{Verb(sense)} {Format(rule.Stimulus)} is {valence}";
            }
        }

        public static string Format(Stimulus stimulus)
        {
            switch (stimulus.Kind)
            {
                case StimulusKind.Animal: return Colour(stimulus.Color);
                case StimulusKind.Item: return $"<b><color={Palette.Hex(Palette.Of(stimulus.Item))}>{Names.Of(stimulus.Item)}</color></b>";
                default: return $"the {Colour(stimulus.Color)} flag";
            }
        }

        public static string Colour(AnimalColor color) => $"<b><color={Palette.Hex(Palette.TextOf(color))}>{color}</color></b>";

        public static string Item(ItemType item, bool capital = false)
        {
            string name = Names.Of(item);
            if (capital)
                name = char.ToUpperInvariant(name[0]) + name.Substring(1);
            return name;
        }

        private static string Verb(string text) => $"<b><color={Palette.Hex(Palette.Accent)}>{text}</color></b>";
    }
}
