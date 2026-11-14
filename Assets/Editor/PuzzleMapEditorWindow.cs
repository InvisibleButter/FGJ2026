using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using MapData = FGJ2026.PuzzleMap.MapData;
using Cell = FGJ2026.PuzzleMap.Cell;
using CueData = FGJ2026.PuzzleMap.CueData;
using StartData = FGJ2026.PuzzleMap.StartData;

namespace FGJ2026.Editor
{
    public sealed class PuzzleMapEditorWindow : EditorWindow
    {
        private enum Brush { Ground, Obstacle, Start, Goal, Cue }

        private const string DefaultFolder = "Assets/Maps";
        private const string MainGamePath = "Assets/Scenes/MainGame.unity";
        private const string LevelsProperty = "levels";
        private const string DefaultPaletteGuid = "7779baabf6114f749fb245902c5c8a15";
        private static readonly Color GroundColor = new Color32(50, 76, 67, 255);
        private static readonly Color CoverageColor = new Color32(100, 78, 40, 255);
        private static readonly Color ObstacleColor = new Color32(76, 82, 94, 255);
        private static readonly Color StartColor = new Color32(38, 83, 121, 255);
        private static readonly Color GoalColor = new Color32(56, 101, 47, 255);
        private static readonly Color CueColor = new Color32(141, 92, 36, 255);

        [SerializeField] private string draftJson;
        [SerializeField] private string mapPath;
        [SerializeField] private bool modified;
        private MapData draft;
        private IntegerField width;
        private IntegerField height;
        private EnumField brush;
        private TextField cueType;
        private PopupField<string> facing;
        private Label status;
        private VisualElement board;
        private readonly List<Sprite> previewSprites = new List<Sprite>();
        private Sprite groundPreview;
        private Sprite alternateGroundPreview;
        private Sprite obstaclePreview;

        [MenuItem("Tools/FGJ2026/Puzzle Map Editor")]
        private static void Open()
        {
            var window = GetWindow<PuzzleMapEditorWindow>("Puzzle Map Editor");
            window.minSize = new Vector2(370, 300);
        }

        public void CreateGUI()
        {
            if (draft == null && !PuzzleMap.TryParse(draftJson, out _, out _, out draft))
                draft = CreateBoard(8, 6);

            var root = rootVisualElement;
            root.Clear();
            ReleasePreviews();
            Scene mainGame = SceneManager.GetSceneByPath(MainGamePath);
            var palette = FindBoard(mainGame)?.Palette ?? AssetDatabase.LoadAssetAtPath<PuzzleTilePalette>(
                AssetDatabase.GUIDToAssetPath(DefaultPaletteGuid));
            if (palette != null)
            {
                groundPreview = GetPreview(palette.Ground, palette, palette.GroundRegion);
                alternateGroundPreview = GetPreview(palette.AlternateGround, palette, palette.AlternateGroundRegion);
                obstaclePreview = GetPreview(palette.Obstacle, palette, palette.ObstacleRegion);
            }
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 6;

            var fileRow = AddRow(root);
            fileRow.Add(new Button(LoadMap) { text = "Load JSON" });
            fileRow.Add(new Button(SaveMap) { text = "Save JSON" });
            fileRow.Add(new Button(LoadGameMap) { text = "Load from MainGame" });
            fileRow.Add(new Button(UseInMainGame) { text = "Use in MainGame" });
            status = new Label { style = { whiteSpace = WhiteSpace.Normal } };
            root.Add(status);

            var dimensions = AddRow(root);
            width = AddDimension(dimensions, "Width", draft.width);
            height = AddDimension(dimensions, "Height", draft.height);
            dimensions.Add(new Button(Generate) { text = "Generate Blank" });
            dimensions.Add(new Button(Resize) { text = "Resize" });

            var options = AddRow(root);
            brush = new EnumField("Brush", Brush.Obstacle);
            options.Add(brush);
            facing = new PopupField<string>("Start facing", new List<string> { "north", "east", "south", "west" },
                draft.start.facing);
            facing.RegisterValueChangedCallback(change =>
            {
                draft.start.facing = change.newValue;
                MarkChanged();
                RefreshCells();
            });
            options.Add(facing);
            cueType = new TextField("Cue type") { value = "birdsong" };
            options.Add(cueType);
            cueType.style.display = DisplayStyle.None;
            brush.RegisterValueChangedCallback(change =>
                cueType.style.display = (Brush)change.newValue == Brush.Cue ? DisplayStyle.Flex : DisplayStyle.None);

            root.Add(new Label("Choose a brush and click a cell. Ground erases obstacles and cues.")
                { style = { whiteSpace = WhiteSpace.Normal } });

            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;
            board = new VisualElement { style = { alignItems = Align.FlexStart } };
            scroll.Add(board);
            root.Add(scroll);
            RebuildBoard();
            ShowStatus();
        }

        private static VisualElement AddRow(VisualElement parent)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            parent.Add(row);
            return row;
        }

        private static IntegerField AddDimension(VisualElement parent, string label, int value)
        {
            var field = new IntegerField(label) { value = value, isDelayed = true };
            field.labelElement.style.minWidth = 40;
            field.style.width = 94;
            parent.Add(field);
            return field;
        }

        private static MapData CreateBoard(int width, int height) => new MapData
        {
            width = width,
            height = height,
            start = new StartData { x = 0, y = 0, facing = width > 1 ? "east" : "north" },
            goal = new Cell { x = width - 1, y = height - 1 },
            blocked = Array.Empty<Cell>(),
            cues = Array.Empty<CueData>()
        };

        private bool ReadDimensions(out int newWidth, out int newHeight)
        {
            newWidth = width.value;
            newHeight = height.value;
            if (newWidth >= 1 && newWidth <= PuzzleMap.MaximumSize &&
                newHeight >= 1 && newHeight <= PuzzleMap.MaximumSize && newWidth * newHeight >= 2)
                return true;

            ShowStatus($"Enter dimensions from 1 to {PuzzleMap.MaximumSize} with at least two cells.");
            return false;
        }

        private void Generate()
        {
            if (!ReadDimensions(out int newWidth, out int newHeight) || !CanDiscardChanges())
                return;

            draft = CreateBoard(newWidth, newHeight);
            mapPath = null;
            facing.SetValueWithoutNotify(draft.start.facing);
            MarkChanged();
            RebuildBoard();
        }

        private void Resize()
        {
            if (!ReadDimensions(out int newWidth, out int newHeight) ||
                (newWidth == draft.width && newHeight == draft.height))
                return;

            bool Outside(Cell cell) => cell.x >= newWidth || cell.y >= newHeight;
            if (Outside(draft.start) || Outside(draft.goal) ||
                draft.blocked.Any(Outside) || draft.cues.Any(Outside))
            {
                ShowStatus("Move or erase cells outside the new bounds before shrinking the board.");
                return;
            }

            draft.width = newWidth;
            draft.height = newHeight;
            MarkChanged();
            RebuildBoard();
        }

        private void RebuildBoard()
        {
            board.Clear();
            for (int y = draft.height - 1; y >= 0; y--)
            {
                VisualElement row = AddRow(board);
                row.style.flexWrap = Wrap.NoWrap;
                for (int x = 0; x < draft.width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var button = new Button(() => PaintCell(cell)) { userData = cell };
                    button.style.width = 34;
                    button.style.height = 32;
                    button.style.marginLeft = 1;
                    button.style.marginRight = 1;
                    button.style.marginTop = 1;
                    button.style.marginBottom = 1;
                    button.style.flexShrink = 0;
                    button.style.color = Color.white;
                    row.Add(button);
                }
            }

            RefreshCells();
        }

        private void RefreshCells()
        {
            var obstacles = new HashSet<Vector2Int>(draft.blocked.Select(cell => cell.Position));
            var cueRows = new HashSet<int>(draft.cues.Select(cue => cue.y));
            var sources = draft.cues.ToLookup(cue => cue.Position);
            foreach (VisualElement row in board.Children())
            {
                foreach (Button button in row.Children())
                {
                    var cell = (Vector2Int)button.userData;
                    string description = "Ground";
                    string symbol = "";
                    Color color = GroundColor;
                    Sprite preview = (cell.x + cell.y) % 2 == 0 ? groundPreview : alternateGroundPreview;
                    if (cueRows.Contains(cell.y))
                    {
                        color = CoverageColor;
                        description = "Cue coverage";
                        preview = null;
                    }
                    if (obstacles.Contains(cell))
                    {
                        color = ObstacleColor;
                        symbol = "X";
                        description = "Obstacle";
                        preview = obstaclePreview;
                    }
                    if (sources.Contains(cell))
                    {
                        color = CueColor;
                        symbol = "C";
                        description = string.Join(", ", sources[cell].Select(cue => cue.type));
                        preview = null;
                    }
                    if (cell == draft.start.Position)
                    {
                        color = StartColor;
                        symbol = draft.start.facing switch { "north" => "↑", "east" => "→", "south" => "↓", _ => "←" };
                        description = $"Start facing {draft.start.facing}";
                        preview = null;
                    }
                    if (cell == draft.goal.Position)
                    {
                        color = GoalColor;
                        symbol = "G";
                        description = "Goal";
                        preview = null;
                    }

                    button.style.backgroundColor = color;
                    button.style.backgroundImage = preview != null
                        ? new StyleBackground(Background.FromSprite(preview))
                        : new StyleBackground(StyleKeyword.None);
                    button.text = symbol;
                    button.tooltip = $"({cell.x}, {cell.y}) {description}";
                }
            }
        }

        private Sprite GetPreview(TileBase tile, PuzzleTilePalette palette, RectInt region)
        {
            if (tile != null)
                return (tile as Tile)?.sprite;

            Sprite sprite = palette.CreateTerrainSprite(region);
            if (sprite != null)
                previewSprites.Add(sprite);
            return sprite;
        }

        private void OnDisable() => ReleasePreviews();

        private void ReleasePreviews()
        {
            foreach (Sprite sprite in previewSprites)
                DestroyImmediate(sprite);
            previewSprites.Clear();
            groundPreview = null;
            alternateGroundPreview = null;
            obstaclePreview = null;
        }

        private void PaintCell(Vector2Int cell)
        {
            var selected = (Brush)brush.value;
            if ((cell == draft.start.Position && selected != Brush.Start) ||
                (cell == draft.goal.Position && selected != Brush.Goal))
            {
                ShowStatus("Move the existing start or goal before painting over it.");
                return;
            }

            string type = cueType.value.Trim();
            if (selected == Brush.Cue && type.Length == 0)
            {
                ShowStatus("Enter a cue type, such as birdsong.");
                return;
            }

            draft.blocked = draft.blocked.Where(item => item.Position != cell).ToArray();
            draft.cues = draft.cues.Where(item => item.Position != cell).ToArray();
            switch (selected)
            {
                case Brush.Obstacle:
                    draft.blocked = draft.blocked.Append(new Cell { x = cell.x, y = cell.y }).ToArray();
                    break;
                case Brush.Start:
                    draft.start.x = cell.x;
                    draft.start.y = cell.y;
                    break;
                case Brush.Goal:
                    draft.goal = new Cell { x = cell.x, y = cell.y };
                    break;
                case Brush.Cue:
                    draft.cues = draft.cues.Append(new CueData { x = cell.x, y = cell.y, type = type }).ToArray();
                    break;
            }

            MarkChanged();
            RefreshCells();
        }

        private void LoadMap()
        {
            string path = EditorUtility.OpenFilePanel("Load puzzle map", DefaultFolder, "json");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                LoadDraft(File.ReadAllText(path), path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                ShowStatus($"Could not load the map. {exception.Message}");
            }
        }

        private void LoadDraft(string json, string path)
        {
            if (!PuzzleMap.TryParse(json, out _, out string error, out MapData loaded))
            {
                ShowStatus($"Could not load {Path.GetFileName(path)}. {error}");
                return;
            }
            if (!CanDiscardChanges())
                return;

            draft = loaded;
            draftJson = JsonUtility.ToJson(draft);
            mapPath = path;
            modified = false;
            width.SetValueWithoutNotify(draft.width);
            height.SetValueWithoutNotify(draft.height);
            facing.SetValueWithoutNotify(draft.start.facing);
            RebuildBoard();
            ShowStatus();
        }

        private void LoadGameMap()
        {
            WithMainGame((_, manager) =>
            {
                var levels = new SerializedObject(manager).FindProperty(LevelsProperty);
                var map = levels.arraySize == 0
                    ? null
                    : levels.GetArrayElementAtIndex(0).objectReferenceValue as TextAsset;
                if (map == null)
                    ShowStatus("MainGame has no map assigned.");
                else
                    LoadDraft(map.text, AssetDatabase.GetAssetPath(map));
            });
        }

        private void UseInMainGame()
        {
            if (modified || string.IsNullOrEmpty(mapPath))
            {
                ShowStatus("Save the current map as project JSON before assigning it.");
                return;
            }

            string assetPath = GetMapAssetPath();
            TextAsset map = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (map == null)
            {
                ShowStatus("Save the map under Assets before assigning it.");
                return;
            }
            if (!PuzzleMap.TryParse(map.text, out _, out string error))
            {
                ShowStatus($"Could not assign {map.name}. {error}");
                return;
            }

            WithMainGame((scene, manager) =>
            {
                var serializedManager = new SerializedObject(manager);
                var levels = serializedManager.FindProperty(LevelsProperty);
                if (levels.arraySize == 0)
                    levels.arraySize = 1;

                var firstLevel = levels.GetArrayElementAtIndex(0);
                if (firstLevel.objectReferenceValue == map)
                {
                    ShowStatus($"MainGame already uses {map.name}.");
                    return;
                }

                bool hadUnsavedChanges = scene.isDirty;
                firstLevel.objectReferenceValue = map;
                serializedManager.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(scene);
                if (hadUnsavedChanges)
                    ShowStatus($"Assigned {map.name}. Save MainGame to keep the assignment.");
                else if (EditorSceneManager.SaveScene(scene))
                    ShowStatus($"MainGame now uses {map.name}.");
                else
                    ShowStatus($"Could not save MainGame. Save the open scene to keep the {map.name} assignment.");
            });
        }

        private void WithMainGame(Action<Scene, GameManager> action)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ShowStatus("Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(MainGamePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(MainGamePath, OpenSceneMode.Additive);

            try
            {
                GameManager[] managers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameManager>(true))
                    .ToArray();
                if (managers.Length != 1)
                {
                    ShowStatus("MainGame needs exactly one GameManager.");
                    return;
                }

                action(scene, managers[0]);
            }
            finally
            {
                if (!wasLoaded && !scene.isDirty)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static PuzzleGridView FindBoard(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            PuzzleGridView[] grids = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PuzzleGridView>(true))
                .ToArray();
            return grids.Length == 1 ? grids[0] : null;
        }

        private string GetMapAssetPath()
        {
            string path = FileUtil.GetProjectRelativePath(Path.GetFullPath(mapPath));
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) ? path : null;
        }

        private void SaveMap()
        {
            string json = JsonUtility.ToJson(draft, true);
            if (!PuzzleMap.TryParse(json, out _, out string error))
            {
                ShowStatus($"Could not save the map. {error}");
                return;
            }

            string name = string.IsNullOrEmpty(mapPath) ? "PuzzleMap" : Path.GetFileNameWithoutExtension(mapPath);
            string folder = DefaultFolder;
            if (!string.IsNullOrEmpty(mapPath))
            {
                string relative = GetMapAssetPath();
                if (relative != null)
                    folder = Path.GetDirectoryName(relative).Replace('\\', '/');
            }
            string path = EditorUtility.SaveFilePanelInProject("Save puzzle map", name, "json",
                "Choose where to save the map JSON.", folder);
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                File.WriteAllText(path, json + Environment.NewLine);
                AssetDatabase.ImportAsset(path);
                draftJson = json;
                mapPath = path;
                modified = false;
                ShowStatus($"Saved {path}. Click Use in MainGame to play this map.");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                ShowStatus($"Could not save the map. {exception.Message}");
            }
        }

        private bool CanDiscardChanges() => !modified ||
            EditorUtility.DisplayDialog("Unsaved map changes", "Replace the current draft?", "Replace Draft", "Cancel");

        private void MarkChanged()
        {
            draftJson = JsonUtility.ToJson(draft);
            modified = true;
            ShowStatus();
        }

        private void ShowStatus(string message = null)
        {
            string name = string.IsNullOrEmpty(mapPath) ? "Untitled map" : Path.GetFileName(mapPath);
            status.text = message ?? (modified ? $"{name} — unsaved changes" : name);
        }
    }
}
