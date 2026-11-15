using System;
using UnityEngine;

namespace FGJ2026.Board
{
    /// <summary>Small 16x16 pixel sprites generated in code for board overlays and fallbacks.</summary>
    public static class PixelSprites
    {
        public const int Size = 16;

        private static Sprite white, dot, frame, shadow, disc, gridLines, gridLeft, gridBottom;

        /// <summary>A full white square (tinted per tile for song lines and shading).</summary>
        public static Sprite White => white ? white : white = Make((x, y) => true);

        /// <summary>A 2x2 dot in the middle (marks squares where an item can go).</summary>
        public static Sprite Dot => dot ? dot : dot = Make((x, y) => (x == 7 || x == 8) && (y == 7 || y == 8));

        /// <summary>A 1-pixel frame around the square (flag colour marker).</summary>
        public static Sprite Frame => frame ? frame : frame = Make((x, y) => x == 0 || y == 0 || x == Size - 1 || y == Size - 1);

        /// <summary>
        /// 1-pixel lines on the left and bottom edge. Drawn on every square, neighbours complete each other
        /// into a grid between the squares (the outer edge is left to the grass border).
        /// </summary>
        public static Sprite GridLines => gridLines ? gridLines : gridLines = Make((x, y) => x == 0 || y == 0);
        /// <summary>Only the left line (squares on the bottom row).</summary>
        public static Sprite GridLeft => gridLeft ? gridLeft : gridLeft = Make((x, y) => x == 0);
        /// <summary>Only the bottom line (squares in the left column).</summary>
        public static Sprite GridBottom => gridBottom ? gridBottom : gridBottom = Make((x, y) => y == 0);

        /// <summary>A flat ellipse at the bottom of the square: the coloured spot under an animal.</summary>
        public static Sprite Shadow => shadow ? shadow : shadow = Make((x, y) =>
        {
            float dx = (x + 0.5f - 8f) / 6.5f;
            float dy = (y + 0.5f - 2.5f) / 2.2f;
            return dx * dx + dy * dy <= 1f;
        });

        /// <summary>A round body, used when no animal animation is set.</summary>
        public static Sprite Disc => disc ? disc : disc = Make((x, y) =>
        {
            float dx = x + 0.5f - 8f, dy = y + 0.5f - 8f;
            return dx * dx + dy * dy <= 36f;
        });

        private static Sprite Make(Func<int, int, bool> filled)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    pixels[y * Size + x] = filled(x, y) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
