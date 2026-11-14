using UnityEngine;
using UnityEngine.Tilemaps;

namespace FGJ2026
{
    [CreateAssetMenu(fileName = "PuzzleTilePalette", menuName = "FGJ2026/Puzzle Tile Palette")]
    public sealed class PuzzleTilePalette : ScriptableObject
    {
        [Tooltip("A Rule Tile fills the ground layer and selects its sprite from neighboring cells.")]
        [SerializeField] private TileBase ground;
        [Tooltip("Alternates with regular ground tiles. For a Rule Tile, set variations in the rules.")]
        [SerializeField] private TileBase alternateGround;
        [SerializeField] private TileBase obstacle;
        [SerializeField] private TileBase cueCoverage;
        [SerializeField] private TileBase cueSource;
        [SerializeField] private TileBase start;
        [SerializeField] private TileBase goal;

        [Header("Terrain Sheet")]
        [Tooltip("Used for ground and obstacles when there is no Tile assigned. Rectangles use a bottom-left origin.")]
        [SerializeField] private Texture2D terrainSheet;
        [SerializeField] private TileRegion groundRegion;
        [SerializeField] private TileRegion alternateGroundRegion;
        [SerializeField] private TileRegion obstacleRegion;

        public TileBase Ground => ground;
        public TileBase AlternateGround => ground is RuleTile || alternateGround == null ? ground : alternateGround;
        public TileBase Obstacle => obstacle;
        public TileBase CueCoverage => cueCoverage;
        public TileBase CueSource => cueSource;
        public TileBase Start => start;
        public TileBase Goal => goal;
        public RectInt GroundRegion => groundRegion.Rect;
        public RectInt AlternateGroundRegion => alternateGroundRegion.Rect.width > 0 ? alternateGroundRegion.Rect : GroundRegion;
        public RectInt ObstacleRegion => obstacleRegion.Rect;

        [System.Serializable]
        private struct TileRegion
        {
            [SerializeField] private int x;
            [SerializeField] private int y;
            [SerializeField] private int width;
            [SerializeField] private int height;

            public RectInt Rect => new RectInt(x, y, width, height);
        }

        public Sprite CreateTerrainSprite(RectInt region)
        {
            if (terrainSheet == null || region.width <= 0 || region.height <= 0 ||
                region.xMin < 0 || region.yMin < 0 ||
                region.xMax > terrainSheet.width || region.yMax > terrainSheet.height)
                return null;

            return Sprite.Create(terrainSheet, new Rect(region.x, region.y, region.width, region.height),
                new Vector2(0.5f, 0.5f), region.width, 0, SpriteMeshType.FullRect);
        }
    }
}
