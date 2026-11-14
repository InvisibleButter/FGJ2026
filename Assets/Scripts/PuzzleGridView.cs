using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace FGJ2026
{
    [RequireComponent(typeof(Grid))]
    public sealed class PuzzleGridView : MonoBehaviour
    {
        [SerializeField] private TextAsset mapJson;
        [SerializeField] private PuzzleTilePalette palette;
        [SerializeField] private Camera boardCamera;
        [SerializeField, Min(0)] private float cameraPadding = 0.75f;

        private readonly List<Tile> fallbackTiles = new List<Tile>();
        private readonly List<Sprite> terrainSprites = new List<Sprite>();
        private Grid grid;
        private Tilemap terrain;
        private Tilemap cues;
        private Tilemap obstacles;
        private Tilemap markers;
        private Tilemap candyLayer;
        private TileBase coverageTile;
        private TileBase clueTile;
        private TileBase startTile;
        private TileBase goalTile;
        private TileBase greenCandyTile;
        private TileBase redCandyTile;
        private Sprite fallbackSprite;
        private float previousAspect;

        public PuzzleMap Map { get; private set; }
        public TextAsset MapJson => mapJson;
        public PuzzleTilePalette Palette => palette;
        public Grid Grid => grid != null ? grid : GetComponent<Grid>();

        private void Awake()
        {
            grid = GetComponent<Grid>();
            if (mapJson != null)
                LoadMap(mapJson);
        }

        public bool LoadMap(TextAsset json)
        {
            if (!PuzzleMap.TryParse(json != null ? json.text : null, out PuzzleMap map, out string error))
            {
                Debug.LogError($"Could not load puzzle map '{(json != null ? json.name : "unassigned")}': {error}", this);
                return false;
            }

            RenderMap(map);
            return true;
        }

        internal void RenderMap(PuzzleMap map)
        {
            if (terrain == null)
            {
                terrain = CreateLayer("Ground", 0);
                cues = CreateLayer("Cue Coverage", 1);
                obstacles = CreateLayer("Obstacles", 2);
                markers = CreateLayer("Markers", 3);
                candyLayer = CreateLayer("Candy", 4);
            }

            terrain.ClearAllTiles();
            cues.ClearAllTiles();
            obstacles.ClearAllTiles();
            markers.ClearAllTiles();
            candyLayer.ClearAllTiles();
            ReleaseFallbackTiles();

            TileBase ground = SelectTile(SelectTerrain(palette?.Ground, palette?.GroundRegion),
                new Color32(49, 78, 68, 255), 0.96f, 0.96f);
            TileBase alternate = SelectTile(SelectTerrain(palette?.AlternateGround, palette?.AlternateGroundRegion),
                new Color32(58, 89, 77, 255), 0.96f, 0.96f);
            TileBase obstacle = SelectTile(SelectTerrain(palette?.Obstacle, palette?.ObstacleRegion),
                new Color32(100, 114, 124, 255), 0.83f, 0.83f);
            coverageTile = SelectTile(palette?.CueCoverage, new Color32(255, 215, 122, 190), 0.96f, 0.16f);
            clueTile = SelectTile(palette?.CueSource, new Color32(255, 208, 104, 255), 0.72f, 0.72f);
            startTile = SelectTile(palette?.Start, new Color32(121, 196, 255, 255), 0.56f, 0.56f);
            goalTile = SelectTile(palette?.Goal, new Color32(178, 243, 141, 255), 0.78f, 0.78f);
            greenCandyTile = SelectTile(null, new Color32(152, 241, 153, 255), 0.4f, 0.4f);
            redCandyTile = SelectTile(null, new Color32(245, 105, 119, 255), 0.4f, 0.4f);

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var position = new Vector3Int(x, y, 0);
                    terrain.SetTile(position, (x + y) % 2 == 0 ? ground : alternate);
                    if (map.IsBlocked(new Vector2Int(x, y)))
                        obstacles.SetTile(position, obstacle);
                }
            }

            Map = map;
            RenderClues(System.Array.Empty<PuzzleMap.CueSource>());
            FrameBoard();
        }

        internal void SetCandy(Vector2Int cell, CandyType? candy)
        {
            TileBase tile = candy switch
            {
                CandyType.Green => greenCandyTile,
                CandyType.Red => redCandyTile,
                _ => null
            };
            candyLayer.SetTile((Vector3Int)cell, tile);
        }

        internal void RenderClues(IEnumerable<PuzzleMap.CueSource> placedClues)
        {
            cues.ClearAllTiles();
            markers.ClearAllTiles();
            foreach (var clue in Map.Cues)
                PaintClue(clue);
            foreach (var clue in placedClues)
                PaintClue(clue);

            markers.SetTile((Vector3Int)Map.Start, startTile);
            markers.SetTile((Vector3Int)Map.Goal, goalTile);
        }

        private void PaintClue(PuzzleMap.CueSource clue)
        {
            for (int x = 0; x < Map.Width; x++)
                cues.SetTile(new Vector3Int(x, clue.Cell.y, 0), coverageTile);
            markers.SetTile((Vector3Int)clue.Cell, clueTile);
        }

        private void LateUpdate()
        {
            if (Map != null && boardCamera != null && !Mathf.Approximately(boardCamera.aspect, previousAspect))
                FrameBoard();
        }

        private Tilemap CreateLayer(string layerName, int sortingOrder)
        {
            var layer = new GameObject(layerName, typeof(Tilemap), typeof(TilemapRenderer));
            layer.layer = gameObject.layer;
            layer.transform.SetParent(transform, false);
            var renderer = layer.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.default2DMaterial != null)
                renderer.sharedMaterial = pipeline.default2DMaterial;
            return layer.GetComponent<Tilemap>();
        }

        private TileBase SelectTile(TileBase tile, Color color, float width, float height)
        {
            if (tile != null)
                return tile;

            if (fallbackSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                fallbackSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), texture.width);
            }

            return CreateTile(fallbackSprite, color, width, height);
        }

        private TileBase SelectTerrain(TileBase tile, RectInt? region)
        {
            if (tile != null || !region.HasValue)
                return tile;

            RectInt pixels = region.Value;
            Sprite sprite = palette.CreateTerrainSprite(pixels);
            if (sprite == null)
                return null;

            terrainSprites.Add(sprite);
            return CreateTile(sprite, Color.white, 1, (float)pixels.width / pixels.height);
        }

        private Tile CreateTile(Sprite sprite, Color color, float width, float height)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = color;
            tile.transform = Matrix4x4.Scale(new Vector3(width, height, 1));
            tile.colliderType = Tile.ColliderType.None;
            fallbackTiles.Add(tile);
            return tile;
        }

        private void FrameBoard()
        {
            if (boardCamera == null || !boardCamera.orthographic)
                return;

            Vector3 first = Grid.CellToWorld(Vector3Int.zero);
            Vector3 far = Grid.CellToWorld(new Vector3Int(Map.Width, Map.Height, 0));
            Vector3 center = (first + far) * 0.5f;
            Vector3 cameraPosition = boardCamera.transform.position;
            boardCamera.transform.position = new Vector3(center.x, center.y, cameraPosition.z);
            previousAspect = boardCamera.aspect;
            float halfWidth = Mathf.Abs(far.x - first.x) * 0.5f + cameraPadding;
            float halfHeight = Mathf.Abs(far.y - first.y) * 0.5f + cameraPadding;
            boardCamera.orthographicSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.01f, previousAspect));
        }

        private void ReleaseFallbackTiles()
        {
            foreach (Tile tile in fallbackTiles)
                Destroy(tile);
            fallbackTiles.Clear();
            foreach (Sprite sprite in terrainSprites)
                Destroy(sprite);
            terrainSprites.Clear();
        }

        private void OnDestroy()
        {
            ReleaseFallbackTiles();
            if (fallbackSprite != null)
                Destroy(fallbackSprite);
        }
    }
}
