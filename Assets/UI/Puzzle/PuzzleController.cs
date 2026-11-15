using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FGJ2026.Board;
using FGJ2026.Simulation;
using FGJ2026.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace FGJ2026
{
    /// <summary>
    /// The puzzle screen. The layout lives in PuzzleScreen.uxml / .uss (UI Toolkit); this script only looks up
    /// the named elements, reacts to clicks and switches classes. The board is a tilemap in the game world
    /// (<see cref="WorldBoard"/>) which the camera fits pixel-perfectly into the empty "boardArea" element.
    /// Drives a <see cref="PuzzleSimulation"/> one tick per second. Also plays test runs from the level editor.
    /// </summary>
    public sealed class PuzzleController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;

        [Header("Templates")]
        [SerializeField] private VisualTreeAsset dayTemplate;
        [SerializeField] private VisualTreeAsset itemButtonTemplate;
        [SerializeField] private VisualTreeAsset behaviourPanelTemplate;
        [SerializeField] private VisualTreeAsset behaviourBlockTemplate;
        [SerializeField] private VisualTreeAsset starRowTemplate;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float secondsPerTick = 1f;
        [SerializeField, Min(0f)] private float startDelay = 0.35f;

        private const string Hidden = "hidden";
        private const string BannerCentered = "phase-banner__track--centered";
        private const string BannerExiting = "phase-banner__track--exiting";
        private const string PlanningBanner = "Planning Phase";
        private const float BannerPositionTolerance = 0.5f;

        private GameManager game;
        private SoundService sound;
        private TestRun test;
        private LevelDefinition level;
        private int levelIndex;
        private PuzzleSimulation sim;
        private ItemType? selectedItem;
        private float tickTimer;
        private Coroutine planningQuaks;

        private WorldBoard board;
        private Camera boardCamera;

        private readonly List<BehaviourPanel> panels = new List<BehaviourPanel>();
        private readonly List<VisualElement> days = new List<VisualElement>();
        private readonly Dictionary<ItemType, (Button button, Label label)> itemButtons = new Dictionary<ItemType, (Button, Label)>();

        private VisualElement root, boardArea, resultOverlay, resultStars;
        private VisualElement phaseBanner, phaseBannerTrack;
        private Label phaseBannerLabel;
        private Coroutine phaseBannerAnimation;
        private Label calendarLabel, clockTick, clockCaption, phaseLabel, messageLabel, resultTitle, resultSubtitle;
        private ProgressBar clockBar;
        private Button startButton, skipButton, rewindButton;
        private Button nextPuzzleButton, playAgainButton, rewindDayButton, restartButton;

        private void Start()
        {
            game = GameManager.Instance;
            sound = game.Sound;
            test = game.CurrentTest;
            levelIndex = Mathf.Clamp(game.SelectedLevelIndex, 0, Mathf.Max(0, game.Levels.Count - 1));
            level = test != null ? test.Level : game.Levels.Get(levelIndex);
            if (level == null)
            {
                Debug.LogError("No playable level. Add level files to Assets/Resources/Levels.");
                return;
            }

            sim = new PuzzleSimulation(level);
            BindUi();
            BuildLevelUi();

            board = new WorldBoard(level, Vector3.zero, true);
            boardCamera = Camera.main;
            boardCamera.enabled = false; // shown once it is framed (after the first UI layout)

            Restart();
        }

        private void OnEnable()
        {
            if (sim?.Phase == PuzzlePhase.Preparation)
                StartPlanningQuaks();
        }

        private void OnDisable()
        {
            CancelPhaseBanner();
            StopPlanningQuaks();
            sound?.StopAnimalSounds();
        }

        private void Update()
        {
            if (sim == null)
                return;

            board.Tick(Time.deltaTime);
            HandlePointer();

            if (sim.Phase != PuzzlePhase.Running)
                return;

            tickTimer += Time.deltaTime;
            if (tickTimer < secondsPerTick)
                return;

            tickTimer -= secondsPerTick;
            board.CapturePreviousSim(sim);
            ApplyTick(sim.Step());
        }

        private void LateUpdate()
        {
            if (board == null || !PanelGeometry.TryGetScreenRect(boardArea, out Rect screenArea))
                return;

            // Every frame, so resizing the window keeps the board pixel-perfect.
            WorldBoard.FrameCamera(boardCamera, board.FramingRect, screenArea);
            boardCamera.enabled = true;
        }

        // ------------------------------------------------------------ UI binding

        private void BindUi()
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore; // the board shows through the empty middle

            root.Q<Label>("puzzleNumber").text = test != null ? "Test run" : $"Puzzle {levelIndex + 1}";
            calendarLabel = root.Q<Label>("calendarLabel");
            root.Q<Label>("puzzleName").text = level.Name;

            boardArea = root.Q("boardArea");
            clockTick = root.Q<Label>("clockTick");
            clockCaption = root.Q<Label>("clockCaption");
            clockBar = root.Q<ProgressBar>("clockBar");
            phaseLabel = root.Q<Label>("phaseLabel");
            messageLabel = root.Q<Label>("messageLabel");
            phaseBanner = root.Q("phaseBanner");
            phaseBannerTrack = root.Q("phaseBannerTrack");
            phaseBannerLabel = root.Q<Label>("phaseBannerLabel");

            startButton = root.Q<Button>("startButton");
            skipButton = root.Q<Button>("skipButton");
            rewindButton = root.Q<Button>("rewindButton");
            startButton.clicked += StartDay;
            skipButton.clicked += SkipDay;
            rewindButton.clicked += Rewind;
            root.Q<Button>("exitButton").clicked += Leave;

            resultOverlay = root.Q("resultOverlay");
            resultTitle = root.Q<Label>("resultTitle");
            resultSubtitle = root.Q<Label>("resultSubtitle");
            resultStars = root.Q("resultStars");
            nextPuzzleButton = root.Q<Button>("nextPuzzleButton");
            playAgainButton = root.Q<Button>("playAgainButton");
            rewindDayButton = root.Q<Button>("rewindDayButton");
            restartButton = root.Q<Button>("restartButton");
            nextPuzzleButton.clicked += () => game.OpenLevel(levelIndex + 1);
            playAgainButton.clicked += Restart;
            restartButton.clicked += Restart;
            rewindDayButton.clicked += Rewind;
            var puzzlesButton = root.Q<Button>("puzzlesButton");
            puzzlesButton.clicked += () => { if (test != null) game.OpenLevelEditor(); else game.OpenLevelSelect(); };
            if (test != null)
                puzzlesButton.text = "Back to editor";
        }

        /// <summary>Fills the level-dependent lists from templates: calendar days, behaviour panels, item bag.</summary>
        private void BuildLevelUi()
        {
            VisualElement dayList = root.Q("dayList");
            int dayBoxes = test != null && test.InfiniteDays ? 0 : level.Days; // endless test runs just count the days
            for (int day = 1; day <= dayBoxes; day++)
            {
                TemplateContainer instance = dayTemplate.Instantiate();
                instance.Q<Label>("dayNumber").text = day.ToString();
                dayList.Add(instance);
                days.Add(instance.Q("day"));
            }

            VisualElement left = root.Q("leftColumn");
            VisualElement right = root.Q("rightColumn");
            foreach (Animal animal in sim.Animals)
            {
                VisualElement column = sim.Animals.Count == 1 || animal.Index % 2 == 0 ? left : right;
                panels.Add(new BehaviourPanel(column, behaviourPanelTemplate, behaviourBlockTemplate, animal));
            }

            VisualElement itemList = root.Q("itemList");
            foreach (ItemType type in level.ItemTypes)
            {
                TemplateContainer instance = itemButtonTemplate.Instantiate();
                var button = instance.Q<Button>("itemButton");
                instance.Q("icon").AddToClassList(type == ItemType.GoodSweet ? "item-icon--good-sweet" : "item-icon--bad-sweet");
                ItemType captured = type;
                button.clicked += () => SelectItem(captured);
                itemList.Add(instance);
                itemButtons[type] = (button, instance.Q<Label>("itemLabel"));
            }
            root.Q("emptyBagLabel").EnableInClassList(Hidden, level.ItemTypes.Count > 0);
        }

        // ------------------------------------------------------------ Board input

        private void HandlePointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !boardCamera.enabled)
                return;

            Vector2 position = mouse.position.ReadValue();
            Vector3 world = boardCamera.ScreenToWorldPoint(position);
            Vector2Int? hovered = !PanelGeometry.IsPointerOverUi(root, position) && board.TryGetCell(world, out Vector2Int cell) ? cell : (Vector2Int?)null;
            board.SetHover(hovered);

            if (!hovered.HasValue)
                return;
            if (mouse.leftButton.wasPressedThisFrame)
                OnCellClicked(hovered.Value, false);
            else if (mouse.rightButton.wasPressedThisFrame)
                OnCellClicked(hovered.Value, true);
        }

        // ------------------------------------------------------------ Actions

        private void Restart()
        {
            sound.StopAnimalSounds();
            sim = new PuzzleSimulation(level);
            selectedItem = level.ItemTypes.Count > 0 ? level.ItemTypes[0] : (ItemType?)null;
            HideResult();
            SetMessage(PreparationHint());
            RefreshAll();
            StartPlanningQuaks();
            ShowPhaseBanner(PlanningBanner);
        }

        private void StartDay()
        {
            if (!sim.StartDay())
                return;
            StopPlanningQuaks();
            tickTimer = secondsPerTick - startDelay;
            SetMessage($"Day {sim.DayNumber} is running...");
            RefreshAll();
            ShowPhaseBanner("Simulation Phase");
        }

        private void SkipDay()
        {
            if (sim.Phase != PuzzlePhase.Running)
                return;
            sound.StopAnimalSounds();
            TickReport last = sim.RunToEndOfDay();
            RefreshAll();
            if (last?.DayEnded != null)
                OnDayEnded(last.DayEnded);
        }

        private void Rewind()
        {
            sound.StopAnimalSounds();
            PuzzlePhase previousPhase = sim.Phase;
            sim.Rewind();
            tickTimer = 0f;
            HideResult();
            SetMessage($"Back to the start of day {sim.DayNumber}. {PreparationHint()}");
            RefreshAll();
            if (sim.Phase == PuzzlePhase.Preparation)
                StartPlanningQuaks();
            if (previousPhase != PuzzlePhase.Preparation && sim.Phase == PuzzlePhase.Preparation)
                ShowPhaseBanner(PlanningBanner);
        }

        /// <summary>Exit: back to the level editor after a test run, otherwise to the main menu.</summary>
        private void Leave()
        {
            CancelPhaseBanner();
            StopPlanningQuaks();
            if (test != null)
                game.OpenLevelEditor();
            else
                game.OpenMenu();
        }

        private void SelectItem(ItemType type)
        {
            selectedItem = type;
            RefreshAll();
        }

        private void OnCellClicked(Vector2Int cell, bool remove)
        {
            if (sim.Phase != PuzzlePhase.Preparation)
                return;

            bool changed = remove || sim.IsPlacedToday(cell)
                ? sim.TryRemoveItem(cell)
                : selectedItem.HasValue && sim.TryPlaceItem(cell, selectedItem.Value);
            if (!changed)
                return;

            // Bag type used up: switch to another type that is left.
            if (selectedItem.HasValue && sim.GetRemaining(selectedItem.Value) == 0)
            {
                foreach (ItemType type in level.ItemTypes)
                {
                    if (sim.GetRemaining(type) > 0)
                    {
                        selectedItem = type;
                        break;
                    }
                }
            }
            RefreshAll();
        }

        // ------------------------------------------------------------ Simulation feedback

        private void StartPlanningQuaks()
        {
            StopPlanningQuaks();
            planningQuaks = StartCoroutine(PlayPlanningQuaks());
        }

        private void StopPlanningQuaks()
        {
            if (planningQuaks == null)
                return;

            StopCoroutine(planningQuaks);
            planningQuaks = null;
            sound.StopIdleSound();
        }

        private IEnumerator PlayPlanningQuaks()
        {
            while (sim.Phase == PuzzlePhase.Preparation)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(1.5f, 4f));

                AnimalColor? color = board.PickIdleAnimalColor();
                if (!color.HasValue)
                    continue;

                sound.PlayIdleSound(color.Value);
                yield return new WaitWhile(() => sound.IsIdleSoundPlaying);
            }
            planningQuaks = null;
        }

        private void ApplyTick(TickReport report)
        {
            if (report == null)
                return;

            RefreshAll(); // first, so blocks for newly learned rules exist
            board.PlayTick(report, sim);
            PlayTickSounds(report);
            foreach (RuleTrigger trigger in report.Triggered.Concat(report.Learned))
                Panel(trigger.Animal)?.Trigger(trigger.Rule.Id);
            if (report.Events.Count > 0)
                SetMessage(string.Join("\n", report.Events.Skip(Mathf.Max(0, report.Events.Count - 3))));

            if (report.DayEnded != null)
                OnDayEnded(report.DayEnded);
        }

        private void PlayTickSounds(TickReport report)
        {
            foreach (RuleTrigger trigger in report.Triggered)
            {
                if (trigger.Rule.Kind == RuleKind.Sing)
                {
                    sound.PlayAnimalSound(trigger.Animal.Color, AnimalSoundCue.Sing);
                }
            }

            foreach (KeyValuePair<int, AnimalAction> entry in report.Actions)
            {
                AnimalColor color = sim.Animals[entry.Key].Color;
                switch (entry.Value)
                {
                    case AnimalAction.LeaveMap:
                        sound.PlayAnimalSound(color, AnimalSoundCue.Plopp);
                        break;
                    case AnimalAction.ReachFlag:
                        sound.PlayAnimalSound(color, AnimalSoundCue.Happy);
                        break;
                    case AnimalAction.Eat:
                        sound.PlayAnimalSound(color, AnimalSoundCue.Eat);
                        break;
                }
            }
        }

        private void OnDayEnded(DayReport day)
        {
            RefreshAll();
            foreach (RuleTrigger confirmed in day.Confirmed)
                Panel(confirmed.Animal)?.Trigger(confirmed.Rule.Id, learned: true);

            switch (sim.Phase)
            {
                case PuzzlePhase.Won:
                    PrepareWinResult();
                    break;
                case PuzzlePhase.Lost:
                    foreach (Animal animal in sim.Animals)
                    {
                        if (!animal.OnFlag)
                            sound.PlayAnimalSound(animal.Color, AnimalSoundCue.Sad);
                    }
                    PrepareLossResult();
                    break;
                default:
                    string learned = day.Confirmed.Count == 0
                        ? "Nobody learned anything."
                        : string.Join(" ", day.Confirmed.Select(c => $"{RuleText.Colour(c.Animal.Color)} learned: {RuleText.Format(c.Rule)}."));
                    SetMessage($"Day {day.Day} is over ({Reason(day.Reason)}). {learned}\nPrepare day {sim.DayNumber}.");
                    StartPlanningQuaks();
                    ShowPhaseBanner(PlanningBanner);
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
            if (level.ItemTypes.All(t => sim.GetRemaining(t) == 0))
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

            for (int i = 0; i < days.Count; i++)
            {
                bool current = i == sim.DayIndex && !sim.IsFinished;
                bool past = i < sim.DayIndex || (i == sim.DayIndex && sim.IsFinished);
                days[i].EnableInClassList("day--current", current);
                days[i].EnableInClassList("day--past", past);
            }

            if (dayCountHidden)
                calendarLabel.text = $"Day {sim.DayNumber}";

            clockTick.text = sim.Tick.ToString("00");
            int ticksLeft = level.TicksPerDay - sim.Tick;
            clockTick.EnableInClassList("clock__tick--late", ticksLeft <= level.TicksPerDay / 3 && ticksLeft > 3);
            clockTick.EnableInClassList("clock__tick--last", ticksLeft <= 3);
            clockCaption.text = $"of {level.TicksPerDay} ticks  <color=#D8C0A0>(total {sim.TotalTicks})</color>";
            clockBar.value = Mathf.Clamp01((float)sim.Tick / level.TicksPerDay);

            switch (sim.Phase)
            {
                case PuzzlePhase.Preparation: phaseLabel.text = $"Preparation - {DayText()}"; break;
                case PuzzlePhase.Running: phaseLabel.text = $"Simulation - {DayText()}"; break;
                case PuzzlePhase.Won: phaseLabel.text = "Solved!"; break;
                default: phaseLabel.text = "Out of days"; break;
            }

            foreach (var pair in itemButtons)
            {
                int remaining = sim.GetRemaining(pair.Key);
                pair.Value.label.text = $"{RuleText.Item(pair.Key, true)}\n<b>x{remaining}</b>";
                pair.Value.button.SetEnabled(preparing && remaining > 0);
                pair.Value.button.EnableInClassList("item-button--selected", preparing && selectedItem == pair.Key);
            }

            startButton.SetEnabled(preparing);
            skipButton.SetEnabled(sim.Phase == PuzzlePhase.Running);
            rewindButton.SetEnabled(sim.Phase != PuzzlePhase.Preparation || sim.ItemsUsed > 0 || sim.DayIndex > 0);
        }

        private bool dayCountHidden => test != null && test.InfiniteDays;

        private string DayText() => dayCountHidden ? $"day {sim.DayNumber} (endless test)" : $"day {sim.DayNumber} of {level.Days}";

        private void SetMessage(string text) => messageLabel.text = text;

        private void ShowPhaseBanner(string text, Action onFinished = null)
        {
            CancelPhaseBanner();
            phaseBannerLabel.text = text;
            phaseBannerAnimation = StartCoroutine(AnimatePhaseBanner(onFinished));
        }

        private IEnumerator AnimatePhaseBanner(Action onFinished)
        {
            yield return new WaitUntil(() => phaseBanner.worldBound.width > 1f &&
                phaseBannerTrack.worldBound.xMax <= phaseBanner.worldBound.xMin + BannerPositionTolerance);
            phaseBannerTrack.AddToClassList(BannerCentered);
            yield return new WaitUntil(() => phaseBannerTrack.worldBound.xMin >= phaseBanner.worldBound.xMin - BannerPositionTolerance);
            yield return new WaitForSecondsRealtime(0.65f);

            phaseBannerTrack.RemoveFromClassList(BannerCentered);
            phaseBannerTrack.AddToClassList(BannerExiting);
            yield return new WaitUntil(() => phaseBannerTrack.worldBound.xMin >= phaseBanner.worldBound.xMax - BannerPositionTolerance);

            phaseBannerAnimation = null;
            CancelPhaseBanner();
            onFinished?.Invoke();
        }

        private void CancelPhaseBanner()
        {
            if (phaseBannerAnimation != null)
            {
                StopCoroutine(phaseBannerAnimation);
                phaseBannerAnimation = null;
            }

            phaseBannerTrack?.RemoveFromClassList(BannerCentered);
            phaseBannerTrack?.RemoveFromClassList(BannerExiting);
        }

        // ------------------------------------------------------------ Result window

        private void PrepareWinResult()
        {
            bool[] earned = sim.EvaluateStars();
            if (test != null)
            {
                PrepareTestWinResult(earned);
                return;
            }
            bool[] isNew = game.Progress.Record(level.Id, earned);

            resultTitle.text = "Puzzle solved!";
            resultSubtitle.text = $"Day {sim.DayNumber} of {level.Days}  |  {sim.TotalTicks} ticks  |  {sim.ItemsUsed} item{(sim.ItemsUsed == 1 ? "" : "s")} used";
            resultStars.Clear();
            for (int i = 0; i < level.Stars.Count; i++)
            {
                bool before = game.Progress.HasStar(level.Id, i) && !isNew[i];
                string note = isNew[i] ? "<color=#6BDE75>new!</color>" : before ? "earned before" : "not this time";
                StarRowView.Add(resultStars, starRowTemplate, level.Stars[i].Description, earned[i] || before, note);
            }

            PrepareResult(won: true);
        }

        /// <summary>A won test run: nothing is saved as progress; the run goes back to the editor as the level's solution.</summary>
        private void PrepareTestWinResult(bool[] earned)
        {
            EditorSession session = game.EditorState;
            session.LastResult = new TestResult
            {
                ContentKey = session.ContentKeyAtTest,
                Days = sim.DayNumber,
                Ticks = sim.TotalTicks,
                Items = sim.ItemsUsed,
                Placements = sim.Placements.ToList()
            };

            resultTitle.text = "Test run solved!";
            resultSubtitle.text = $"Day {sim.DayNumber}  |  {sim.TotalTicks} ticks  |  {sim.ItemsUsed} item{(sim.ItemsUsed == 1 ? "" : "s")} used. The editor keeps this run as the level's solution.";
            resultStars.Clear();
            for (int i = 0; i < level.Stars.Count; i++)
                StarRowView.Add(resultStars, starRowTemplate, level.Stars[i].Description, earned[i], earned[i] ? "met" : "missed");
            PrepareResult(won: true);
        }

        private void PrepareLossResult()
        {
            resultTitle.text = "Out of days";
            resultSubtitle.text = "The animals didn't reach their flags in time.";
            resultStars.Clear();
            PrepareResult(won: false);
        }

        private void PrepareResult(bool won)
        {
            nextPuzzleButton.EnableInClassList(Hidden, !won || test != null || game.Levels.Get(levelIndex + 1) == null);
            playAgainButton.EnableInClassList(Hidden, !won);
            rewindDayButton.EnableInClassList(Hidden, won);
            restartButton.EnableInClassList(Hidden, won);
            ShowPhaseBanner("Level Finished", () => resultOverlay.RemoveFromClassList(Hidden));
        }

        private void HideResult() => resultOverlay?.AddToClassList(Hidden);
    }
}
