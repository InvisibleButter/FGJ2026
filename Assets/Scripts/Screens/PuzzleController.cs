using System.Collections.Generic;
using System.Linq;
using FGJ2026.Simulation;
using FGJ2026.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FGJ2026
{
    /// <summary>
    /// The puzzle screen. Board in the middle, behaviour lists on the sides, calendar and clock on top,
    /// item bag and Start/Skip at the bottom, Rewind and Exit top right.
    /// Builds its placeholder UI from code and drives a <see cref="PuzzleSimulation"/> one tick per second.
    /// </summary>
    public sealed class PuzzleController : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float secondsPerTick = 1f;
        [SerializeField, Min(0f)] private float startDelay = 0.35f;

        private GameManager game;
        private LevelDefinition level;
        private int levelIndex;
        private PuzzleSimulation sim;
        private ItemType? selectedItem;
        private float tickTimer;

        private BoardView board;
        private readonly List<BehaviourPanel> panels = new List<BehaviourPanel>();
        private readonly List<(Image box, TextMeshProUGUI label)> dayBoxes = new List<(Image, TextMeshProUGUI)>();
        private readonly Dictionary<ItemType, (Button button, Image background, TextMeshProUGUI label)> itemButtons =
            new Dictionary<ItemType, (Button, Image, TextMeshProUGUI)>();
        private TextMeshProUGUI clockText, clockCaption, phaseText, messageText;
        private Image clockFill;
        private Button startButton, skipButton, rewindButton;
        private RectTransform overlay, overlayContent;

        private void Start()
        {
            Ui.EnsureEventSystem();
            game = GameManager.Instance;
            levelIndex = Mathf.Clamp(game.SelectedLevelIndex, 0, Mathf.Max(0, game.Levels.Count - 1));
            level = game.Levels.Get(levelIndex);
            if (level == null)
            {
                Debug.LogError("No playable level. Add level files to Assets/Resources/Levels.");
                return;
            }

            sim = new PuzzleSimulation(level);
            BuildUi();
            Restart();
        }

        private void Update()
        {
            if (sim == null)
                return;

            foreach (BehaviourPanel panel in panels)
                panel.Tick(Time.deltaTime);

            if (sim.Phase != PuzzlePhase.Running)
                return;

            tickTimer += Time.deltaTime;
            if (tickTimer < secondsPerTick)
                return;

            tickTimer -= secondsPerTick;
            ApplyTick(sim.Step());
        }

        // ------------------------------------------------------------ Actions

        private void Restart()
        {
            sim = new PuzzleSimulation(level);
            selectedItem = level.ItemTypes.FirstOrDefault(t => sim.GetRemaining(t) > 0);
            if (level.ItemTypes.Count == 0)
                selectedItem = null;
            HideOverlay();
            SetMessage(PreparationHint());
            RefreshAll();
        }

        private void StartDay()
        {
            if (!sim.StartDay())
                return;
            tickTimer = secondsPerTick - startDelay;
            SetMessage($"Day {sim.DayNumber} is running...");
            RefreshAll();
        }

        private void SkipDay()
        {
            if (sim.Phase != PuzzlePhase.Running)
                return;
            TickReport last = sim.RunToEndOfDay();
            RefreshAll();
            if (last?.DayEnded != null)
                OnDayEnded(last.DayEnded);
        }

        private void Rewind()
        {
            sim.Rewind();
            tickTimer = 0f;
            HideOverlay();
            SetMessage($"Back to the start of day {sim.DayNumber}. {PreparationHint()}");
            RefreshAll();
        }

        private void Exit() => game.OpenMenu();

        private void SelectItem(ItemType type)
        {
            selectedItem = type;
            RefreshAll();
        }

        private void OnCellClicked(Vector2Int cell, PointerEventData.InputButton button)
        {
            if (sim.Phase != PuzzlePhase.Preparation)
                return;

            bool changed;
            if (button == PointerEventData.InputButton.Right || sim.IsPlacedToday(cell))
                changed = sim.TryRemoveItem(cell);
            else
                changed = selectedItem.HasValue && sim.TryPlaceItem(cell, selectedItem.Value);

            if (!changed)
                return;

            if (selectedItem.HasValue && sim.GetRemaining(selectedItem.Value) == 0)
            {
                ItemType next = level.ItemTypes.FirstOrDefault(t => sim.GetRemaining(t) > 0);
                if (sim.GetRemaining(next) > 0)
                    selectedItem = next;
            }
            RefreshAll();
        }

        // ------------------------------------------------------------ Simulation feedback

        private void ApplyTick(TickReport report)
        {
            if (report == null)
                return;

            RefreshAll(); // first, so blocks for newly learned rules exist
            foreach (RuleTrigger trigger in report.Triggered.Concat(report.Learned))
                Panel(trigger.Animal)?.Trigger(trigger.Rule.Id);
            if (report.Events.Count > 0)
                SetMessage(string.Join("\n", report.Events.Skip(Mathf.Max(0, report.Events.Count - 3))));

            if (report.DayEnded != null)
                OnDayEnded(report.DayEnded);
        }

        private void OnDayEnded(DayReport day)
        {
            RefreshAll();
            foreach (RuleTrigger confirmed in day.Confirmed)
                Panel(confirmed.Animal)?.Trigger(confirmed.Rule.Id, 2.5f);

            switch (sim.Phase)
            {
                case PuzzlePhase.Won:
                    ShowWin();
                    break;
                case PuzzlePhase.Lost:
                    ShowLoss();
                    break;
                default:
                    string learned = day.Confirmed.Count == 0
                        ? "Nobody learned anything."
                        : string.Join(" ", day.Confirmed.Select(c => $"{RuleText.Colour(c.Animal.Color)} learned: {RuleText.Format(c.Rule)}."));
                    SetMessage($"Day {day.Day} is over ({Reason(day.Reason)}). {learned}\nPrepare day {sim.DayNumber}.");
                    break;
            }
        }

        private BehaviourPanel Panel(Animal animal) => panels.FirstOrDefault(p => p.AnimalIndex == animal.Index);

        private static string Reason(DayEndReason reason)
        {
            switch (reason)
            {
                case DayEndReason.TickLimit: return "the clock ran out";
                case DayEndReason.AnimalsDone: return "all walking animals are done";
                default: return "goal reached";
            }
        }

        private string PreparationHint()
        {
            if (level.ItemTypes.Count == 0 || level.ItemTypes.All(t => sim.GetRemaining(t) == 0))
                return "Press Start to begin the day.";
            return "Pick an item from the bag and click a square to place it (right-click takes it back). Then press Start.";
        }

        // ------------------------------------------------------------ Refresh

        private void RefreshAll()
        {
            bool preparing = sim.Phase == PuzzlePhase.Preparation;
            board.Refresh(sim, preparing && selectedItem.HasValue && sim.GetRemaining(selectedItem.Value) > 0);

            foreach (BehaviourPanel panel in panels)
                panel.Refresh(sim.Animals[panel.AnimalIndex]);

            // Calendar
            for (int i = 0; i < dayBoxes.Count; i++)
            {
                bool past = i < sim.DayIndex || (i == sim.DayIndex && sim.IsFinished);
                bool current = i == sim.DayIndex && !sim.IsFinished;
                dayBoxes[i].box.color = current ? Palette.Accent : past ? Palette.PanelLight : Palette.Panel;
                dayBoxes[i].label.color = current ? Palette.TextDark : past ? Palette.TextDim : Palette.Text;
                dayBoxes[i].label.fontStyle = past ? FontStyles.Strikethrough : FontStyles.Bold;
            }

            // Clock
            clockText.text = sim.Tick.ToString("00");
            clockCaption.text = $"of {level.TicksPerDay} ticks\n<size=80%><color={Palette.Hex(Palette.TextDim)}>total {sim.TotalTicks}</color></size>";
            clockFill.fillAmount = Mathf.Clamp01((float)sim.Tick / level.TicksPerDay);

            // Phase
            switch (sim.Phase)
            {
                case PuzzlePhase.Preparation: phaseText.text = $"Preparation - day {sim.DayNumber} of {level.Days}"; break;
                case PuzzlePhase.Running: phaseText.text = $"Simulation - day {sim.DayNumber} of {level.Days}"; break;
                case PuzzlePhase.Won: phaseText.text = "Solved!"; break;
                default: phaseText.text = "Out of days"; break;
            }

            // Item bag
            foreach (var pair in itemButtons)
            {
                int remaining = sim.GetRemaining(pair.Key);
                pair.Value.label.text = $"{RuleText.Item(pair.Key, true)}\n<size=120%><b>x{remaining}</b></size>";
                pair.Value.button.interactable = preparing && remaining > 0;
                pair.Value.background.color = selectedItem == pair.Key && preparing ? Palette.Button : Palette.ButtonSecondary;
            }

            startButton.interactable = preparing;
            skipButton.interactable = sim.Phase == PuzzlePhase.Running;
            rewindButton.interactable = sim.Phase != PuzzlePhase.Preparation || sim.ItemsUsed > 0 || sim.DayIndex > 0;
        }

        private void SetMessage(string text) => messageText.text = text;

        // ------------------------------------------------------------ Result overlay

        private void ShowWin()
        {
            bool[] earned = sim.EvaluateStars();
            bool[] isNew = game.Progress.Record(level.Id, earned);

            ShowOverlay("Puzzle solved!",
                $"Day {sim.DayNumber} of {level.Days}  |  {sim.TotalTicks} ticks  |  {sim.ItemsUsed} item{(sim.ItemsUsed == 1 ? "" : "s")} used");

            for (int i = 0; i < level.Stars.Count; i++)
            {
                bool before = game.Progress.HasStar(level.Id, i) && !isNew[i];
                string note = isNew[i] ? $"<color={Palette.Hex(Palette.Good)}>new!</color>"
                    : before ? "<color=#9AA3AE>earned before</color>"
                    : "<color=#9AA3AE>not this time</color>";
                AddStarRow(overlayContent, level.Stars[i].Description, earned[i] || before, note);
            }

            RectTransform buttons = AddButtonRow();
            if (game.Levels.Get(levelIndex + 1) != null)
                Ui.CreateButton(buttons, "Next puzzle", () => game.OpenLevel(levelIndex + 1), Palette.ButtonGo).Size(240, 72);
            Ui.CreateButton(buttons, "Play again", Restart, Palette.Button).Size(220, 72);
            Ui.CreateButton(buttons, "Puzzles", game.OpenLevelSelect, Palette.ButtonSecondary).Size(200, 72);
        }

        private void ShowLoss()
        {
            ShowOverlay("Out of days", "The animals didn't reach their flags in time.");
            RectTransform buttons = AddButtonRow();
            Ui.CreateButton(buttons, "Rewind last day", Rewind, Palette.Button).Size(280, 72);
            Ui.CreateButton(buttons, "Restart", Restart, Palette.ButtonSecondary).Size(200, 72);
            Ui.CreateButton(buttons, "Puzzles", game.OpenLevelSelect, Palette.ButtonSecondary).Size(200, 72);
        }

        private void ShowOverlay(string title, string subtitle)
        {
            Ui.DestroyChildren(overlayContent);
            overlay.gameObject.SetActive(true);
            Ui.CreateText(overlayContent, title, 58, Palette.Accent).Size(-1, 76);
            Ui.CreateText(overlayContent, subtitle, 26, Palette.TextDim).Size(-1, 40);
        }

        private RectTransform AddButtonRow()
        {
            RectTransform row = Ui.CreateRect("Buttons", overlayContent);
            row.Horizontal(16, 0, TextAnchor.MiddleCenter);
            row.Size(-1, 90);
            return row;
        }

        private void HideOverlay()
        {
            if (overlay != null)
                overlay.gameObject.SetActive(false);
        }

        internal static void AddStarRow(Transform parent, string description, bool lit, string note)
        {
            RectTransform row = Ui.CreateRect("Star", parent);
            row.Horizontal(16, 0, TextAnchor.MiddleLeft);
            row.Size(-1, 54);
            Ui.CreateImage(row, "Icon", lit ? Palette.StarOn : Palette.StarOff, Ui.Star).Size(48, 48);
            Ui.CreateText(row, description, 28, lit ? Palette.Text : Palette.TextDim, TextAlignmentOptions.MidlineLeft).Size(-1, 54, 1);
            if (!string.IsNullOrEmpty(note))
                Ui.CreateText(row, note, 22, Palette.TextDim, TextAlignmentOptions.MidlineRight).Size(170, 54);
        }

        // ------------------------------------------------------------ Layout

        private void BuildUi()
        {
            Canvas canvas = Ui.CreateCanvas("Puzzle UI");
            canvas.transform.SetParent(transform, false);
            Transform root = canvas.transform;

            Ui.CreateImage(root, "Background", Palette.Background).rectTransform.Stretch();

            // ---- Top bar: name, calendar, clock, rewind + exit
            RectTransform top = Ui.CreateRect("Top bar", root);
            top.anchorMin = new Vector2(0, 1);
            top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.sizeDelta = new Vector2(0, 120);
            top.anchoredPosition = Vector2.zero;

            TextMeshProUGUI title = Ui.CreateText(top, $"<size=60%><color={Palette.Hex(Palette.TextDim)}>Puzzle {levelIndex + 1}</color></size>\n{level.Name}",
                40, Palette.Text, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(460, 100));

            RectTransform calendar = Ui.CreateRect("Calendar", top);
            calendar.Pin(new Vector2(0.5f, 0.5f), new Vector2(-150, 0), new Vector2(600, 90));
            calendar.Horizontal(10, 0, TextAnchor.MiddleCenter);
            Ui.CreateText(calendar, "Day", 26, Palette.TextDim, TextAlignmentOptions.MidlineRight).Size(70, 60);
            for (int day = 1; day <= level.Days; day++)
            {
                Image box = Ui.CreateImage(calendar, $"Day {day}", Palette.Panel);
                box.Size(64, 64);
                TextMeshProUGUI label = Ui.CreateText(box.transform, day.ToString(), 30, Palette.Text);
                label.rectTransform.Stretch();
                dayBoxes.Add((box, label));
            }

            RectTransform clock = Ui.CreateRect("Clock", top);
            clock.Pin(new Vector2(0.5f, 0.5f), new Vector2(330, 0), new Vector2(330, 96));
            clock.Horizontal(14, 0, TextAnchor.MiddleLeft);
            Image dial = Ui.CreateImage(clock, "Dial", Palette.Panel, Ui.Circle);
            dial.Size(88, 88);
            clockFill = Ui.CreateImage(dial.transform, "Progress", new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0.55f), Ui.Circle);
            clockFill.rectTransform.Stretch();
            clockFill.type = Image.Type.Filled;
            clockFill.fillMethod = Image.FillMethod.Radial360;
            clockFill.fillOrigin = (int)Image.Origin360.Top;
            clockFill.fillClockwise = true;
            clockText = Ui.CreateText(dial.transform, "00", 36, Palette.Text);
            clockText.rectTransform.Stretch();
            clockText.fontStyle = FontStyles.Bold;
            clockCaption = Ui.CreateText(clock, "", 22, Palette.Text, TextAlignmentOptions.MidlineLeft);
            clockCaption.Size(200, 88);

            RectTransform topButtons = Ui.CreateRect("Top buttons", top);
            topButtons.Pin(new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(380, 72));
            topButtons.Horizontal(14, 0, TextAnchor.MiddleRight);
            rewindButton = Ui.CreateButton(topButtons, "Rewind", Rewind, Palette.ButtonSecondary);
            rewindButton.Size(180, 66);
            Ui.CreateButton(topButtons, "Exit", Exit, Palette.ButtonSecondary).Size(150, 66);

            // ---- Side columns with behaviour lists
            RectTransform left = Column(root, true);
            RectTransform right = Column(root, false);
            foreach (Animal animal in sim.Animals)
            {
                Transform column = sim.Animals.Count == 1 || animal.Index % 2 == 0 ? left : right;
                panels.Add(new BehaviourPanel(column, animal));
            }

            // ---- Board
            RectTransform boardArea = Ui.CreateRect("Board area", root);
            boardArea.Pin(new Vector2(0.5f, 0.5f), new Vector2(0, 25), new Vector2(980, 770));
            board = new BoardView(boardArea, level, new Vector2(960, 750), true);
            board.CellClicked += OnCellClicked;

            // ---- Bottom bar: phase, item bag, message, start + skip
            RectTransform bottom = Ui.CreateRect("Bottom bar", root);
            bottom.anchorMin = Vector2.zero;
            bottom.anchorMax = new Vector2(1, 0);
            bottom.pivot = new Vector2(0.5f, 0);
            bottom.sizeDelta = new Vector2(0, 160);
            bottom.anchoredPosition = Vector2.zero;
            Ui.CreateImage(bottom, "Panel", Palette.Panel).rectTransform.Stretch();

            RectTransform bag = Ui.CreateRect("Item bag", bottom);
            bag.Pin(new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(640, 120));
            bag.Horizontal(14, 0, TextAnchor.MiddleLeft);
            Ui.CreateText(bag, "Item\nbag", 24, Palette.TextDim, TextAlignmentOptions.MidlineLeft).Size(70, 100);
            foreach (ItemType type in level.ItemTypes)
            {
                ItemType captured = type;
                Button button = Ui.CreateButton(bag, "", () => SelectItem(captured), Palette.ButtonSecondary, 24);
                button.Size(250, 100);
                var background = (Image)button.targetGraphic;
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.rectTransform.Stretch(80, 4, 10, 4);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                Image icon = Ui.CreateImage(button.transform, "Icon", Palette.Of(type), Ui.Circle);
                icon.raycastTarget = false;
                icon.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(52, 52));
                TextMeshProUGUI mark = Ui.CreateText(icon.transform, type == ItemType.GoodSweet ? "+" : "-", 34,
                    type == ItemType.GoodSweet ? Palette.TextDark : Palette.Text);
                mark.rectTransform.Stretch();
                mark.fontStyle = FontStyles.Bold;
                itemButtons[type] = (button, background, label);
            }
            if (level.ItemTypes.Count == 0)
                Ui.CreateText(bag, "<i>empty</i>", 24, Palette.TextDim, TextAlignmentOptions.MidlineLeft).Size(200, 100);

            phaseText = Ui.CreateText(bottom, "", 24, Palette.Accent, TextAlignmentOptions.TopLeft);
            phaseText.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(120, 48), new Vector2(720, 34));
            messageText = Ui.CreateText(bottom, "", 22, Palette.Text, TextAlignmentOptions.TopLeft);
            messageText.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(120, -12), new Vector2(720, 96));

            RectTransform actions = Ui.CreateRect("Actions", bottom);
            actions.Pin(new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(420, 100));
            actions.Horizontal(14, 0, TextAnchor.MiddleRight);
            startButton = Ui.CreateButton(actions, "Start", StartDay, Palette.ButtonGo, 34);
            startButton.Size(210, 92);
            skipButton = Ui.CreateButton(actions, "Skip", SkipDay, Palette.Button, 34);
            skipButton.Size(180, 92);

            // ---- Result overlay
            Image dim = Ui.CreateImage(root, "Result overlay", Palette.Overlay);
            overlay = dim.rectTransform.Stretch();
            Image card = Ui.CreateImage(overlay, "Card", Palette.Panel);
            card.rectTransform.Pin(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 560));
            overlayContent = card.rectTransform;
            card.Vertical(14, 40, TextAnchor.UpperCenter);
            overlay.gameObject.SetActive(false);
        }

        private static RectTransform Column(Transform root, bool leftSide)
        {
            RectTransform column = Ui.CreateRect(leftSide ? "Left behaviours" : "Right behaviours", root);
            column.anchorMin = new Vector2(leftSide ? 0 : 1, 0);
            column.anchorMax = new Vector2(leftSide ? 0 : 1, 1);
            column.pivot = new Vector2(leftSide ? 0 : 1, 1);
            column.offsetMin = new Vector2(leftSide ? 30 : -450, 185);
            column.offsetMax = new Vector2(leftSide ? 450 : -30, -135);
            column.Vertical(16);
            return column;
        }
    }
}
