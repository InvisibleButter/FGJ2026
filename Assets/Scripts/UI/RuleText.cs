using FGJ2026.Simulation;

namespace FGJ2026.UI
{
    /// <summary>Rich-text wording for behaviour blocks: behaviours and colours are marked specially.</summary>
    public static class RuleText
    {
        /// <param name="onLight">Dark wording for light backgrounds (the coloured behaviour blocks).</param>
        public static string Format(Rule rule, bool onLight = false)
        {
            switch (rule.Kind)
            {
                case RuleKind.MoveForward:
                    return $"{Verb("Move", onLight)} forward";
                case RuleKind.Sing:
                    return $"{Verb("Sing", onLight)} forward";
                default:
                    string sense = rule.Sense == SenseKind.Hear ? "Hearing" : "Seeing";
                    string valence = rule.Positive
                        ? $"<b><color={(onLight ? "#2F5A26" : Palette.Hex(Palette.Good))}>good</color></b>"
                        : $"<b><color={(onLight ? "#8A2030" : Palette.Hex(Palette.Bad))}>bad</color></b>";
                    return $"{Verb(sense, onLight)} {Format(rule.Stimulus, onLight)} is {valence}";
            }
        }

        public static string Format(Stimulus stimulus, bool onLight = false)
        {
            switch (stimulus.Kind)
            {
                case StimulusKind.Animal: return Colour(stimulus.Color, onLight);
                case StimulusKind.Item:
                    UnityEngine.Color item = Palette.Of(stimulus.Item);
                    if (onLight)
                        item = UnityEngine.Color.Lerp(item, UnityEngine.Color.black, 0.45f);
                    return $"<b><color={Palette.Hex(item)}>{Names.Of(stimulus.Item)}</color></b>";
                default: return $"the {Colour(stimulus.Color, onLight)} flag";
            }
        }

        public static string Colour(AnimalColor color, bool onLight = false)
        {
            UnityEngine.Color c = onLight ? UnityEngine.Color.Lerp(Palette.Of(color), UnityEngine.Color.black, 0.45f) : Palette.TextOf(color);
            return $"<b><color={Palette.Hex(c)}>{color}</color></b>";
        }

        public static string Item(ItemType item, bool capital = false)
        {
            string name = Names.Of(item);
            if (capital)
                name = char.ToUpperInvariant(name[0]) + name.Substring(1);
            return name;
        }

        private static string Verb(string text, bool onLight = false) => onLight ? $"<b><u>{text}</u></b>" : $"<b><color={Palette.Hex(Palette.Accent)}>{text}</color></b>";
    }
}
