using FGJ2026.Simulation;
using UnityEngine;

namespace FGJ2026.UI
{
    /// <summary>All placeholder colours in one place.</summary>
    public static class Palette
    {
        public static readonly Color Background = new Color(0.11f, 0.13f, 0.16f);
        public static readonly Color Panel = new Color(0.17f, 0.19f, 0.23f);
        public static readonly Color PanelLight = new Color(0.24f, 0.27f, 0.32f);
        public static readonly Color Highlight = new Color(0.98f, 0.9f, 0.55f);
        public static readonly Color Cell = new Color(0.77f, 0.84f, 0.68f);
        public static readonly Color CellAlt = new Color(0.72f, 0.8f, 0.63f);
        public static readonly Color Blocked = new Color(0.33f, 0.31f, 0.29f);
        public static readonly Color Text = new Color(0.95f, 0.95f, 0.95f);
        public static readonly Color TextDim = new Color(0.66f, 0.69f, 0.74f);
        public static readonly Color TextDark = new Color(0.12f, 0.13f, 0.15f);
        public static readonly Color Accent = new Color(1f, 0.78f, 0.28f);
        public static readonly Color Good = new Color(0.42f, 0.87f, 0.46f);
        public static readonly Color Bad = new Color(0.98f, 0.42f, 0.42f);
        public static readonly Color Button = new Color(0.26f, 0.45f, 0.76f);
        public static readonly Color ButtonSecondary = new Color(0.33f, 0.36f, 0.42f);
        public static readonly Color ButtonGo = new Color(0.27f, 0.62f, 0.38f);
        public static readonly Color StarOn = new Color(1f, 0.82f, 0.22f);
        public static readonly Color StarOff = new Color(0.34f, 0.36f, 0.42f);
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.65f);

        /// <summary>The animal's colour, taken from the [Color]_Background swatches in ui.png.</summary>
        public static Color Of(AnimalColor color)
        {
            switch (color)
            {
                case AnimalColor.Red: return Hex("A83848");
                case AnimalColor.Blue: return Hex("6888B8");
                case AnimalColor.Green: return Hex("A8B858");
                case AnimalColor.Yellow: return Hex("D8B868");
                case AnimalColor.Purple: return Hex("686898");
                case AnimalColor.Orange: return Hex("E88858");
                case AnimalColor.Turquoise: return Hex("386858");
                case AnimalColor.Pink: return new Color(0.96f, 0.5f, 0.72f);
                default: return new Color(0.95f, 0.95f, 0.95f);
            }
        }

        /// <summary>A lighter version for text on the dark panels (the darker art colours would be hard to read).</summary>
        public static Color TextOf(AnimalColor color) => Color.Lerp(Of(color), Color.white, 0.3f);

        private static Color Hex(string rgb) => ColorUtility.TryParseHtmlString("#" + rgb, out Color c) ? c : Color.magenta;

        public static Color Of(ItemType item) =>
            item == ItemType.GoodSweet ? new Color(1f, 0.6f, 0.78f) : new Color(0.38f, 0.24f, 0.46f);

        public static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
    }
}
