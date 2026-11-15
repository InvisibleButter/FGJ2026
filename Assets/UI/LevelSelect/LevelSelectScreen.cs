using System.Collections.Generic;
using System.Linq;
using FGJ2026.Board;
using FGJ2026.Simulation;
using FGJ2026.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace FGJ2026
{
    /// <summary>
    /// Puzzle overview. Layout in LevelSelect.uxml / .uss; a LevelCard.uxml per puzzle. Clicking a card shows a
    /// picture of the puzzle, its name and its star conditions; Play (or clicking the card again) opens it.
    /// </summary>
    public sealed class LevelSelectScreen : MonoBehaviour
    {
        [SerializeField] private UIDocument document;

        [Header("Templates")]
        [SerializeField] private VisualTreeAsset levelCardTemplate;
        [SerializeField] private VisualTreeAsset starRowTemplate;

        private GameManager game;
        private int selected = -1;
        private readonly List<Button> cards = new List<Button>();

        private Label detailNumber, detailName, detailInfo;
        private Image preview;
        private VisualElement detailStars;

        private WorldBoard previewBoard;
        private Camera previewCamera;
        private RenderTexture previewTexture;

        private void Start()
        {
            game = GameManager.Instance;
            VisualElement root = document.rootVisualElement;

            int earned = game.Levels.Levels.Sum(l => game.Progress.CountStars(l.Id));
            int total = game.Levels.Levels.Sum(l => l.Stars.Count);
            root.Q<Label>("totalStarsLabel").text = $"{earned} / {total}";

            root.Q<Button>("backButton").clicked += game.OpenMenu;
            root.Q<Button>("playButton").clicked += () => game.OpenLevel(selected);

            detailNumber = root.Q<Label>("detailNumber");
            detailName = root.Q<Label>("detailName");
            detailInfo = root.Q<Label>("detailInfo");
            detailStars = root.Q("detailStars");
            preview = root.Q<Image>("preview");
            preview.scaleMode = ScaleMode.ScaleToFit;

            VisualElement grid = root.Q("levelGrid");
            for (int i = 0; i < game.Levels.Count; i++)
                cards.Add(CreateCard(grid, i, game.Levels.Get(i)));

            bool hasLevels = game.Levels.Count > 0;
            root.Q("emptyLabel").EnableInClassList("hidden", hasLevels);
            root.Q("details").EnableInClassList("hidden", !hasLevels);
            if (hasLevels)
                Select(Mathf.Clamp(game.SelectedLevelIndex, 0, game.Levels.Count - 1));
        }

        private Button CreateCard(VisualElement grid, int index, LevelDefinition level)
        {
            TemplateContainer instance = levelCardTemplate.Instantiate();
            var card = instance.Q<Button>("levelCard");
            instance.Q<Label>("cardNumber").text = (index + 1).ToString();
            instance.Q<Label>("cardName").text = level.Name;

            VisualElement stars = instance.Q("cardStars");
            for (int s = 0; s < level.Stars.Count; s++)
            {
                var star = new VisualElement();
                star.AddToClassList("star");
                star.EnableInClassList("star--on", game.Progress.HasStar(level.Id, s));
                stars.Add(star);
            }

            card.clicked += () => OnCardClicked(index);
            grid.Add(instance);
            return card;
        }

        private void OnCardClicked(int index)
        {
            if (index == selected)
                game.OpenLevel(index); // second click on the selected card plays it
            else
                Select(index);
        }

        private void Select(int index)
        {
            selected = index;
            game.SelectedLevelIndex = index;
            for (int i = 0; i < cards.Count; i++)
                cards[i].EnableInClassList("level-card--selected", i == index);

            LevelDefinition level = game.Levels.Get(index);
            detailNumber.text = $"Puzzle {index + 1}";
            detailName.text = level.Name;

            string bag = level.ItemTypes.Count == 0
                ? "no items"
                : string.Join(", ", level.ItemTypes.Select(t => $"{level.GetItemCount(t)}x {RuleText.Item(t)}"));
            detailInfo.text = $"{level.Days} day{(level.Days == 1 ? "" : "s")}  |  {level.TicksPerDay} ticks per day  |  {bag}";

            detailStars.Clear();
            for (int s = 0; s < level.Stars.Count; s++)
            {
                bool has = game.Progress.HasStar(level.Id, s);
                StarRowView.Add(detailStars, starRowTemplate, level.Stars[s].Description, has, has ? "<color=#6BDE75>earned</color>" : "");
            }

            ShowPreview(level);
        }

        /// <summary>
        /// Builds the puzzle's tilemap board away from everything else and renders it into a texture at
        /// 1 texel per art pixel (point filtered), shown by the preview Image.
        /// </summary>
        private void ShowPreview(LevelDefinition level)
        {
            ClearPreview();
            previewBoard = new WorldBoard(level, new Vector3(1000f, 0f, 0f), false);
            previewBoard.Refresh(new PuzzleSimulation(level));

            Rect frame = previewBoard.FramingRect;
            int width = Mathf.RoundToInt(frame.width * WorldBoard.PixelsPerUnit);
            int height = Mathf.RoundToInt(frame.height * WorldBoard.PixelsPerUnit);
            previewTexture = new RenderTexture(width, height, 16) { filterMode = FilterMode.Point, name = "Puzzle preview" };
            previewTexture.Create();

            previewCamera = new GameObject("Preview camera").AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = frame.height * 0.5f;
            previewCamera.transform.position = new Vector3(frame.center.x, frame.center.y, -10f);
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.17f, 0.19f, 0.23f);
            previewCamera.targetTexture = previewTexture;

            preview.image = previewTexture;
        }

        private void ClearPreview()
        {
            previewBoard?.Destroy();
            previewBoard = null;
            if (previewCamera != null)
                Destroy(previewCamera.gameObject);
            if (previewTexture != null)
            {
                if (preview != null)
                    preview.image = null;
                previewTexture.Release();
                Destroy(previewTexture);
            }
        }

        private void OnDestroy() => ClearPreview();
    }
}
