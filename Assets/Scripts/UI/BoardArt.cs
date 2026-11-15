using UnityEngine;
using UnityEngine.Tilemaps;

namespace FGJ2026.UI
{
    /// <summary>
    /// The art the board uses, in one asset (Assets/Resources/BoardArt.asset).
    /// All sprites are 16 px = one square. Empty slots fall back to plain placeholder shapes.
    /// </summary>
    [CreateAssetMenu(menuName = "FGJ2026/Board Art", fileName = "BoardArt")]
    public sealed class BoardArt : ScriptableObject
    {
        public const string ResourcePath = "BoardArt";

        [Header("Tiles")]
        [Tooltip("Underlay below the grass: fills the board and a one-square margin around it (water). Shows through the grass edges and blocked squares.")]
        public TileBase underlay;

        [Tooltip("Ground of every free square. A rule tile gives the board rounded edges and borders around blocked squares.")]
        public TileBase grass;

        [Tooltip("Optional obstacle drawn on blocked squares, above the underlay (e.g. rocks). Empty: blocked squares just show the underlay.")]
        public TileBase water;

        [Tooltip("Small decorations scattered on free grass squares.")]
        public TileBase[] grassDetails;

        [Range(0f, 1f), Tooltip("Share of free squares that get a decoration.")]
        public float grassDetailDensity = 0.35f;

        [Header("Animations")]
        [Tooltip("Animated flag shown on flag squares.")]
        public RuntimeAnimatorController flag;

        [Tooltip("Animated frame shown on the square under the mouse.")]
        public RuntimeAnimatorController hoverCursor;

        [Tooltip("Animated arrow on the square an animal faces. The sprite must point east (right).")]
        public RuntimeAnimatorController facingDirection;

        [Tooltip("Animal animations. States used: Animal_Idle, Animal_Move, Animal_Eat, Animal_Sing, Animal_Love, Animal_Sad.")]
        public RuntimeAnimatorController animal;

        [Header("Songs")]
        [Tooltip("Notes drawn on every square a song covers, tinted in the singer's colour. Empty = tint the squares.")]
        public Sprite[] songNotes;

        [Header("Items")]
        public Sprite goodSweet;
        public Sprite badSweet;

        private static BoardArt cached;

        public static BoardArt Load() => cached != null ? cached : cached = Resources.Load<BoardArt>(ResourcePath);
    }
}
