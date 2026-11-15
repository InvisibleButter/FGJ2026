using System.Collections.Generic;
using System.Linq;
using FGJ2026.Simulation;
using FGJ2026.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FGJ2026
{
    /// <summary>
    /// Puzzle overview: every puzzle as a card with its stars. Clicking a card shows a small picture of the
    /// puzzle, its name and its star conditions (with the ones already earned); Play opens it.
    /// </summary>
    public sealed class LevelSelectScreen : MonoBehaviour
    {
        private GameManager game;
        private int selected = -1;
        private readonly List<Image> cards = new List<Image>();
        private RectTransform details;

        private void Start()
        {
            Ui.EnsureEventSystem();
            game = GameManager.Instance;
            Build();
            if (game.Levels.Count > 0)
                Select(Mathf.Clamp(game.SelectedLevelIndex, 0, game.Levels.Count - 1));
        }

        private void Build()
        {
            Canvas canvas = Ui.CreateCanvas("Level Select UI");
            canvas.transform.SetParent(transform, false);
            Transform root = canvas.transform;
            Ui.CreateImage(root, "Background", Palette.Background).rectTransform.Stretch();

            Ui.CreateText(root, "Puzzles", 64, Palette.Text, TextAlignmentOptions.MidlineLeft)
                .rectTransform.Pin(new Vector2(0, 1), new Vector2(70, -40), new Vector2(600, 90));

            int earned = game.Levels.Levels.Sum(l => game.Progress.CountStars(l.Id));
            int total = game.Levels.Levels.Sum(l => l.Stars.Count);
            RectTransform totals = Ui.CreateRect("Total stars", root);
            totals.Pin(new Vector2(0, 1), new Vector2(760, -50), new Vector2(360, 70));
            totals.Horizontal(12, 0, TextAnchor.MiddleRight);
            Ui.CreateImage(totals, "Star", Palette.StarOn, Ui.Star).Size(54, 54);
            Ui.CreateText(totals, $"{earned} / {total}", 40, Palette.Text, TextAlignmentOptions.MidlineLeft).Size(180, 60);

            Ui.CreateButton(root, "Back", game.OpenMenu, Palette.ButtonSecondary)
                .GetComponent<RectTransform>().Pin(new Vector2(0, 0), new Vector2(70, 50), new Vector2(200, 72));

            // Grid of puzzle cards
            RectTransform grid = Ui.CreateRect("Puzzle grid", root);
            grid.anchorMin = new Vector2(0, 0);
            grid.anchorMax = new Vector2(0, 1);
            grid.pivot = new Vector2(0, 1);
            grid.offsetMin = new Vector2(70, 150);
            grid.offsetMax = new Vector2(1110, -170);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(240, 220);
            layout.spacing = new Vector2(24, 24);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;

            for (int i = 0; i < game.Levels.Count; i++)
                cards.Add(CreateCard(grid, i, game.Levels.Get(i)));

            if (game.Levels.Count == 0)
            {
                Ui.CreateText(grid, "No puzzles found.\nAdd level files to Assets/Resources/Levels.", 30, Palette.TextDim,
                    TextAlignmentOptions.TopLeft);
            }

            // Details panel
            Image panel = Ui.CreateImage(root, "Details", Palette.Panel);
            panel.rectTransform.anchorMin = new Vector2(1, 0);
            panel.rectTransform.anchorMax = new Vector2(1, 1);
            panel.rectTransform.pivot = new Vector2(1, 1);
            panel.rectTransform.offsetMin = new Vector2(-760, 50);
            panel.rectTransform.offsetMax = new Vector2(-60, -50);
            details = panel.rectTransform;
            panel.Vertical(14, 30, TextAnchor.UpperCenter);
        }

        private Image CreateCard(Transform grid, int index, LevelDefinition level)
        {
            Button button = Ui.CreateButton(grid, "", () => OnCardClicked(index), Palette.Panel);
            var background = (Image)button.targetGraphic;
            Ui.DestroyChildren(button.transform);

            TextMeshProUGUI number = Ui.CreateText(button.transform, (index + 1).ToString(), 64, Palette.Accent);
            number.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(220, 80));
            number.fontStyle = FontStyles.Bold;

            TextMeshProUGUI name = Ui.CreateText(button.transform, level.Name, 26, Palette.Text);
            name.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(220, 60));

            RectTransform stars = Ui.CreateRect("Stars", button.transform);
            stars.Pin(new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(220, 44));
            stars.Horizontal(6, 0, TextAnchor.MiddleCenter);
            for (int s = 0; s < level.Stars.Count; s++)
                Ui.CreateImage(stars, "Star", game.Progress.HasStar(level.Id, s) ? Palette.StarOn : Palette.StarOff, Ui.Star).Size(40, 40);

            return background;
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
                cards[i].color = i == index ? Palette.PanelLight : Palette.Panel;

            LevelDefinition level = game.Levels.Get(index);
            Ui.DestroyChildren(details);

            Ui.CreateText(details, $"<size=60%><color={Palette.Hex(Palette.TextDim)}>Puzzle {index + 1}</color></size>\n{level.Name}",
                48, Palette.Text).Size(-1, 110);

            // Small picture of the puzzle: the board in its starting state.
            RectTransform picture = Ui.CreateRect("Picture", details);
            picture.Size(-1, 330);
            var preview = new BoardView(picture, level, new Vector2(600, 310), false, 80f);
            preview.Refresh(new PuzzleSimulation(level));

            string bag = level.ItemTypes.Count == 0
                ? "no items"
                : string.Join(", ", level.ItemTypes.Select(t => $"{level.GetItemCount(t)}x {RuleText.Item(t)}"));
            Ui.CreateText(details, $"{level.Days} day{(level.Days == 1 ? "" : "s")}  |  {level.TicksPerDay} ticks per day  |  {bag}",
                24, Palette.TextDim).Size(-1, 36);

            for (int s = 0; s < level.Stars.Count; s++)
            {
                bool has = game.Progress.HasStar(level.Id, s);
                PuzzleController.AddStarRow(details, level.Stars[s].Description, has,
                    has ? $"<color={Palette.Hex(Palette.Good)}>earned</color>" : "");
            }

            RectTransform spacer = Ui.CreateRect("Spacer", details);
            spacer.Size(-1, 10, -1, 1);

            Ui.CreateButton(details, "Play", () => game.OpenLevel(index), Palette.ButtonGo, 36).Size(-1, 86);
        }
    }
}
