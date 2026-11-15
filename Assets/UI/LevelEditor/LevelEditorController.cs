using System;
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
    /// Map maker / sandbox. Paint the board, place and rotate animals and flags, give animals behaviours,
    /// set name, size, days, item bag, stars and the place in the game's level order, test-play the level
    /// (optionally with endless days) and save it. Layout in LevelEditor.uxml / .uss; this script only binds
    /// the named elements, reacts to clicks and switches classes. The board is the same tilemap board as in the game.
    /// </summary>
    public sealed class LevelEditorController : MonoBehaviour
    {
        private enum Tool { Select, Grass, Water, Animal, Flag, Erase }
        private enum BehaviourKind { Move, Sing, Hear, See }

        [SerializeField] private UIDocument document;

        [Header("Templates")]
        [SerializeField] private VisualTreeAsset behaviourRowTemplate;
        [SerializeField] private VisualTreeAsset levelListItemTemplate;

        private const string Hidden = "hidden";
        private const string SelectedChoice = "choice--selected";
        private const int EndlessTestDays = 999;
        private static readonly AnimalColor[] Colours = (AnimalColor[])Enum.GetValues(typeof(AnimalColor));
        private static readonly StarKind?[] StarChoices = { null, StarKind.MaxTicks, StarKind.MaxItems, StarKind.MaxDays };

        private GameManager game;
        private LevelDraft draft;
        private string sourceId;
        private int flowPosition;
        private bool endlessDays = true;
        private TestResult matchingResult;
        private string testMessage = "";

        private Tool tool = Tool.Animal;
        private AnimalColor colour = AnimalColor.Red;
        private int selectedAnimal = -1, selectedFlag = -1;
        private BehaviourKind addKind = BehaviourKind.Hear;
        private Stimulus addTarget = Stimulus.OfAnimal(AnimalColor.Blue);
        private bool addGood = true;

        private WorldBoard board;
        private Camera boardCamera;
        private SpriteRenderer selectionMarker;
        private Vector2Int? lastPaintedCell;

        // UI
        private VisualElement root, boardArea, animalInspector, flagInspector, behaviourList, targetSection, openOverlay, openList;
        private Label levelLabel, noSelection, animalTitle, facingLabel, noBehaviours, addPreview, flagTitle,
            validationLabel, testResultLabel, statusLabel, flowHint;
        private TextField nameField;
        private Button testButton, saveButton, infiniteDaysButton, starsFromRunButton;
        private Stepper width, height, days, ticks, goodSweets, badSweets, flow;
        private readonly Stepper[] starLimit = new Stepper[3];
        private readonly Dictionary<Tool, Button> toolButtons = new Dictionary<Tool, Button>();
        private readonly Dictionary<AnimalColor, Button> colourButtons = new Dictionary<AnimalColor, Button>();
        private readonly Dictionary<BehaviourKind, Button> kindButtons = new Dictionary<BehaviourKind, Button>();
        private readonly Dictionary<Stimulus, Button> targetButtons = new Dictionary<Stimulus, Button>();
        private readonly Dictionary<(int slot, StarKind? kind), Button> starButtons = new Dictionary<(int, StarKind?), Button>();
        private Button feelGood, feelBad;

        /// <summary>Binds a Stepper.uxml instance: label, value, minus and plus.</summary>
        private sealed class Stepper
        {
            private readonly VisualElement root;
            private readonly Label value;

            public Stepper(VisualElement root, Action<int> change)
            {
                this.root = root;
                value = root.Q<Label>("stepperValue");
                root.Q<Button>("minusButton").clicked += () => change(-1);
                root.Q<Button>("plusButton").clicked += () => change(+1);
            }

            public void Show(int number) => value.text = number.ToString();
            public void SetVisible(bool visible) => root.EnableInClassList(Hidden, !visible);
        }

        // ------------------------------------------------------------ Lifetime

        private void Start()
        {
            game = GameManager.Instance;
            BindUi();
            RestoreSession();

            boardCamera = Camera.main;
            boardCamera.enabled = false; // shown once it is framed (after the first UI layout)

            selectionMarker = new GameObject("Selection marker").AddComponent<SpriteRenderer>();
            selectionMarker.sprite = PixelSprites.Frame;
            selectionMarker.color = new Color(1f, 0.78f, 0.28f);
            selectionMarker.sortingOrder = 20;

            RebuildBoard();
            RefreshUi();
        }

        private void Update()
        {
            if (draft == null)
                return;
            HandlePointer();
            HandleKeys();
        }

        private void LateUpdate()
        {
            if (board == null || !PanelGeometry.TryGetScreenRect(boardArea, out Rect screenArea))
                return;
            WorldBoard.FrameCamera(boardCamera, board.FramingRect, screenArea);
            boardCamera.enabled = true;
        }

        // ------------------------------------------------------------ Session (survives test runs)

        private void RestoreSession()
        {
            EditorSession session = game.EditorState;
            if (string.IsNullOrEmpty(session.DraftJson))
            {
                NewLevel();
                return;
            }

            draft = LevelDraft.FromJson(session.DraftJson);
            sourceId = session.SourceId;
            flowPosition = session.FlowPosition;
            endlessDays = session.InfiniteDays;
            ApplyTestResult(session);
        }

        private void SaveSession()
        {
            EditorSession session = game.EditorState;
            session.DraftJson = draft.ToJson();
            session.SourceId = sourceId;
            session.FlowPosition = flowPosition;
            session.InfiniteDays = endlessDays;
        }

        /// <summary>A won test run of exactly this level becomes its solution.</summary>
        private void ApplyTestResult(EditorSession session)
        {
            matchingResult = null;
            TestResult result = session.LastResult;
            if (result == null)
            {
                testMessage = "";
                return;
            }
            if (result.ContentKey != draft.ContentKey())
            {
                testMessage = "The last test run was for an older version of this level.";
                return;
            }

            matchingResult = result;
            string run = $"Solved on day {result.Days} in {result.Ticks} ticks with {result.Items} item{(result.Items == 1 ? "" : "s")}.";
            if (result.Days > draft.Data.days)
            {
                testMessage = $"{run} That is more than the level's {draft.Data.days} days.";
                return;
            }
            draft.SetSolution(result.Placements);
            testMessage = $"{run} Saved with the level as its solution.";
        }

        private void NewLevel()
        {
            draft = LevelDraft.CreateNew();
            sourceId = null;
            flowPosition = game.Levels.Count + 1;
            matchingResult = null;
            testMessage = "";
            game.EditorState.LastResult = null;
            ClearSelection();
        }

        private void OpenExisting(int index)
        {
            LevelDefinition level = game.Levels.Get(index);
            string json = level == null ? null : game.Levels.LoadJson(level.Id);
            if (json == null)
            {
                SetStatus("That level could not be read.", false);
                return;
            }

            draft = LevelDraft.FromJson(json);
            sourceId = level.Id;
            flowPosition = index + 1;
            matchingResult = null;
            testMessage = draft.HasSolution ? "This level has a stored solution." : "";
            game.EditorState.LastResult = null;
            ClearSelection();
            SetStatus($"Opened \"{level.Name}\".", true);
        }

        // ------------------------------------------------------------ UI binding

        private void BindUi()
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            boardArea = root.Q("boardArea");
            levelLabel = root.Q<Label>("levelLabel");
            root.Q<Button>("openButton").clicked += ShowOpenWindow;
            testButton = root.Q<Button>("testButton");
            testButton.clicked += StartTestRun;
            saveButton = root.Q<Button>("saveButton");
            saveButton.clicked += Save;
            root.Q<Button>("exitButton").clicked += () =>
            {
                SaveSession();
                game.OpenMenu();
            };

            foreach (Tool t in Enum.GetValues(typeof(Tool)))
            {
                Tool captured = t;
                toolButtons[t] = root.Q<Button>("tool" + t);
                toolButtons[t].clicked += () =>
                {
                    tool = captured;
                    RefreshUi();
                };
            }

            foreach (AnimalColor c in Colours)
            {
                AnimalColor captured = c;
                colourButtons[c] = root.Q<Button>("color" + c);
                colourButtons[c].clicked += () => PickColour(captured);

                Stimulus target = Stimulus.OfAnimal(c);
                targetButtons[target] = root.Q<Button>("target" + c);
                targetButtons[target].clicked += () => PickTarget(target);
            }
            foreach (ItemType item in new[] { ItemType.GoodSweet, ItemType.BadSweet })
            {
                Stimulus target = Stimulus.OfItem(item);
                targetButtons[target] = root.Q<Button>("target" + item);
                targetButtons[target].clicked += () => PickTarget(target);
            }

            nameField = root.Q<TextField>("nameField");
            nameField.RegisterValueChangedCallback(e =>
            {
                draft.SetName(e.newValue);
                RefreshUi();
            });

            width = new Stepper(root.Q("widthStepper"), d => EditMap(() => draft.Resize(draft.Width + d, draft.Height)));
            height = new Stepper(root.Q("heightStepper"), d => EditMap(() => draft.Resize(draft.Width, draft.Height + d)));
            days = new Stepper(root.Q("daysStepper"), d => EditSettings(() => draft.SetDays(draft.Data.days + d)));
            ticks = new Stepper(root.Q("ticksStepper"), d => EditMap(() => draft.SetTicksPerDay(draft.Data.ticksPerDay + d)));
            goodSweets = new Stepper(root.Q("goodSweetStepper"), d => EditMap(() => draft.SetItemCount(ItemType.GoodSweet, draft.GetItemCount(ItemType.GoodSweet) + d)));
            badSweets = new Stepper(root.Q("badSweetStepper"), d => EditMap(() => draft.SetItemCount(ItemType.BadSweet, draft.GetItemCount(ItemType.BadSweet) + d)));
            flow = new Stepper(root.Q("flowStepper"), d => EditSettings(() => flowPosition = Mathf.Clamp(flowPosition + d, 1, MaxFlowPosition)));

            for (int slot = 1; slot <= 2; slot++)
            {
                int capturedSlot = slot;
                starLimit[slot] = new Stepper(root.Q($"star{slot}Stepper"), d => ChangeStarLimit(capturedSlot, d));
                foreach (StarKind? kind in StarChoices)
                {
                    StarKind? capturedKind = kind;
                    Button button = root.Q<Button>($"star{slot}{(kind.HasValue ? kind.Value.ToString() : "None")}");
                    button.clicked += () => PickStarKind(capturedSlot, capturedKind);
                    starButtons[(slot, kind)] = button;
                }
            }

            noSelection = root.Q<Label>("noSelection");
            animalInspector = root.Q("animalInspector");
            flagInspector = root.Q("flagInspector");
            animalTitle = root.Q<Label>("animalTitle");
            flagTitle = root.Q<Label>("flagTitle");
            facingLabel = root.Q<Label>("facingLabel");
            behaviourList = root.Q("behaviourList");
            noBehaviours = root.Q<Label>("noBehaviours");
            targetSection = root.Q("targetSection");
            addPreview = root.Q<Label>("addPreview");
            root.Q<Button>("rotateLeftButton").clicked += () => RotateSelected(false);
            root.Q<Button>("rotateRightButton").clicked += () => RotateSelected(true);
            root.Q<Button>("addBehaviourButton").clicked += AddBehaviour;
            root.Q<Button>("deleteAnimalButton").clicked += RemoveSelected;
            root.Q<Button>("deleteFlagButton").clicked += RemoveSelected;

            foreach (BehaviourKind kind in Enum.GetValues(typeof(BehaviourKind)))
            {
                BehaviourKind captured = kind;
                kindButtons[kind] = root.Q<Button>("kind" + kind);
                kindButtons[kind].clicked += () =>
                {
                    addKind = captured;
                    if (addKind == BehaviourKind.Hear && addTarget.Kind != StimulusKind.Animal)
                        addTarget = Stimulus.OfAnimal(AnimalColor.Blue);
                    RefreshUi();
                };
            }
            feelGood = root.Q<Button>("feelGood");
            feelBad = root.Q<Button>("feelBad");
            feelGood.clicked += () => { addGood = true; RefreshUi(); };
            feelBad.clicked += () => { addGood = false; RefreshUi(); };

            flowHint = root.Q<Label>("flowHint");
            validationLabel = root.Q<Label>("validationLabel");
            testResultLabel = root.Q<Label>("testResultLabel");
            statusLabel = root.Q<Label>("statusLabel");
            infiniteDaysButton = root.Q<Button>("infiniteDaysButton");
            infiniteDaysButton.clicked += () => { endlessDays = !endlessDays; RefreshUi(); };
            starsFromRunButton = root.Q<Button>("starsFromRunButton");
            starsFromRunButton.clicked += UseRunForStars;

            openOverlay = root.Q("openOverlay");
            openList = root.Q("openList");
            root.Q<Button>("newLevelButton").clicked += () =>
            {
                NewLevel();
                HideOpenWindow();
                SetStatus("New level.", true);
                RebuildBoard();
                RefreshUi();
            };
            root.Q<Button>("closeOpenButton").clicked += HideOpenWindow;
        }

        // ------------------------------------------------------------ Board

        private void RebuildBoard()
        {
            board?.Destroy();
            board = null;
            LevelDefinition preview = draft.BuildPreview();
            if (preview == null)
                return;
            board = new WorldBoard(preview, Vector3.zero, true);
            board.Refresh(new PuzzleSimulation(preview));
            UpdateSelectionMarker();
        }

        private void HandlePointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || board == null || !boardCamera.enabled || !openOverlay.ClassListContains(Hidden))
                return;

            Vector2 position = mouse.position.ReadValue();
            bool overUi = PanelGeometry.IsPointerOverUi(root, position);
            Vector2Int? cell = !overUi && board.TryGetCell(boardCamera.ScreenToWorldPoint(position), out Vector2Int c) ? c : (Vector2Int?)null;
            board.SetHover(cell);

            if (!mouse.leftButton.isPressed)
                lastPaintedCell = null;
            if (!cell.HasValue)
                return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                RemoveAt(cell.Value);
                return;
            }

            bool paints = tool == Tool.Grass || tool == Tool.Water || tool == Tool.Erase;
            if (mouse.leftButton.wasPressedThisFrame || (paints && mouse.leftButton.isPressed && lastPaintedCell != cell))
            {
                lastPaintedCell = cell;
                UseTool(cell.Value);
            }
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || IsTyping())
                return;

            if (keyboard.rKey.wasPressedThisFrame)
                RotateSelected(!keyboard.shiftKey.isPressed);
            if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                RemoveSelected();
        }

        private bool IsTyping()
        {
            Focusable focused = root.panel?.focusController?.focusedElement;
            return focused is VisualElement element && (element is TextField || element.GetFirstAncestorOfType<TextField>() != null);
        }

        private void UseTool(Vector2Int cell)
        {
            int animal = draft.AnimalIndexAt(cell);
            int flag = draft.FlagIndexAt(cell);

            switch (tool)
            {
                case Tool.Select:
                    Select(animal, flag);
                    break;
                case Tool.Grass:
                    EditMap(() => draft.SetBlocked(cell, false));
                    break;
                case Tool.Water:
                    if (animal >= 0 || flag >= 0)
                        SetStatus("Remove the animal or flag first; water can't go under them.", false);
                    else
                        EditMap(() => draft.SetBlocked(cell, true));
                    break;
                case Tool.Animal:
                    if (animal >= 0)
                        Select(animal, -1);
                    else if (draft.IsFree(cell))
                        EditMap(() => Select(draft.AddAnimal(cell, colour, Directions.North, "move"), -1));
                    break;
                case Tool.Flag:
                    if (flag >= 0)
                        Select(-1, flag);
                    else if (draft.IsFree(cell))
                        EditMap(() => Select(-1, draft.AddFlag(cell, colour)));
                    break;
                case Tool.Erase:
                    RemoveAt(cell);
                    break;
            }
        }

        private void RemoveAt(Vector2Int cell)
        {
            if (draft.AnimalIndexAt(cell) >= 0 || draft.FlagIndexAt(cell) >= 0)
                EditMap(() =>
                {
                    draft.RemoveAt(cell);
                    ClearSelection();
                });
            else if (draft.IsBlocked(cell))
                EditMap(() => draft.SetBlocked(cell, false));
        }

        // ------------------------------------------------------------ Selection

        private void Select(int animal, int flag)
        {
            selectedAnimal = animal;
            selectedFlag = animal >= 0 ? -1 : flag;
            if (selectedAnimal >= 0 && Names.TryParseColor(draft.Animal(selectedAnimal).color, out AnimalColor c))
                colour = c;
            else if (selectedFlag >= 0 && Names.TryParseColor(draft.Flag(selectedFlag).color, out c))
                colour = c;
            UpdateSelectionMarker();
            RefreshUi();
        }

        private void ClearSelection()
        {
            selectedAnimal = -1;
            selectedFlag = -1;
            UpdateSelectionMarker();
        }

        private void UpdateSelectionMarker()
        {
            if (selectionMarker == null)
                return;
            CellEntry entry = (CellEntry)draft.Animal(selectedAnimal) ?? draft.Flag(selectedFlag);
            selectionMarker.gameObject.SetActive(entry != null && board != null);
            if (entry != null && board != null)
                selectionMarker.transform.position = board.CellCenter(new Vector2Int(entry.x, entry.y));
        }

        private void RotateSelected(bool clockwise)
        {
            if (selectedAnimal >= 0)
                EditMap(() => draft.RotateAnimal(selectedAnimal, clockwise));
        }

        private void RemoveSelected()
        {
            CellEntry entry = (CellEntry)draft.Animal(selectedAnimal) ?? draft.Flag(selectedFlag);
            if (entry != null)
                RemoveAt(new Vector2Int(entry.x, entry.y));
        }

        private void PickColour(AnimalColor picked)
        {
            colour = picked;
            if (selectedAnimal >= 0)
                EditMap(() => draft.SetAnimalColor(selectedAnimal, picked));
            else if (selectedFlag >= 0)
                EditMap(() => draft.SetFlagColor(selectedFlag, picked));
            else
                RefreshUi();
        }

        // ------------------------------------------------------------ Behaviours

        private void PickTarget(Stimulus target)
        {
            if (addKind == BehaviourKind.Hear && target.Kind != StimulusKind.Animal)
                return; // only animals can be heard
            addTarget = target;
            RefreshUi();
        }

        private string BehaviourText()
        {
            switch (addKind)
            {
                case BehaviourKind.Move: return "move";
                case BehaviourKind.Sing: return "sing";
                default:
                    string target = addTarget.Kind == StimulusKind.Item
                        ? (addTarget.Item == ItemType.GoodSweet ? "goodSweet" : "badSweet")
                        : addTarget.Color.ToString().ToLowerInvariant();
                    return $"{(addKind == BehaviourKind.Hear ? "hear" : "see")} {target} {(addGood ? "good" : "bad")}";
            }
        }

        private void AddBehaviour()
        {
            string error = null;
            EditMap(() => draft.AddBehaviour(selectedAnimal, BehaviourText(), out error));
            if (error != null)
                SetStatus(error, false);
        }

        // ------------------------------------------------------------ Stars

        private void PickStarKind(int slot, StarKind? kind)
        {
            EditSettings(() => draft.SetStar(slot, kind, kind.HasValue ? DefaultStarValue(kind.Value) : 0));
        }

        private void ChangeStarLimit(int slot, int delta)
        {
            StarEntry star = draft.GetStar(slot);
            StarKind? kind = star == null ? null : LevelDraft.ParseStarKind(star.type);
            if (!kind.HasValue)
                return;
            int minimum = kind == StarKind.MaxItems ? 0 : 1;
            EditSettings(() => draft.SetStar(slot, kind, Mathf.Max(minimum, star.value + delta)));
        }

        private int DefaultStarValue(StarKind kind)
        {
            switch (kind)
            {
                case StarKind.MaxTicks: return matchingResult?.Ticks ?? draft.Data.ticksPerDay * draft.Data.days;
                case StarKind.MaxItems: return matchingResult?.Items ?? draft.Data.items.Sum(i => i.count);
                case StarKind.MaxDays: return matchingResult?.Days ?? draft.Data.days;
                default: return 0;
            }
        }

        private void UseRunForStars()
        {
            if (matchingResult == null)
                return;
            EditSettings(() =>
            {
                draft.SetStar(1, StarKind.MaxTicks, matchingResult.Ticks);
                draft.SetStar(2, StarKind.MaxItems, matchingResult.Items);
            });
            SetStatus("Star limits set from the test run.", true);
        }

        // ------------------------------------------------------------ Test run, save, open

        private void StartTestRun()
        {
            if (!draft.Validate(out _, out string error))
            {
                SetStatus($"Can't test yet: {error}", false);
                return;
            }

            LevelFile data = LevelDraft.Clone(draft.Data);
            if (endlessDays)
                data.days = EndlessTestDays;
            data.solution = new PlacementEntry[0];
            LevelDefinition.TryParse(JsonUtility.ToJson(data), "test", out LevelDefinition testLevel, out _);

            SaveSession();
            game.EditorState.ContentKeyAtTest = draft.ContentKey();
            game.EditorState.LastResult = null;
            game.StartTestRun(testLevel, endlessDays);
        }

        private void Save()
        {
            if (!LevelLibrary.CanSave)
            {
                SetStatus("Levels can only be saved in the Unity Editor.", false);
                return;
            }
            if (string.IsNullOrWhiteSpace(draft.Data.name))
            {
                SetStatus("Give the level a name first.", false);
                return;
            }
            if (!draft.Validate(out _, out string error))
            {
                SetStatus($"Can't save yet: {error}", false);
                return;
            }

            if (string.IsNullOrEmpty(draft.Data.id))
                draft.SetId(UniqueId(LevelDraft.MakeId(draft.Data.name)));

            if (!LevelLibrary.Save(draft.Data, game.Levels.FileNameOf(sourceId), flowPosition - 1, out string path, out error))
            {
                SetStatus(error, false);
                return;
            }

            game.Levels.Reload();
            sourceId = draft.Data.id;
            flowPosition = game.Levels.IndexOf(sourceId) + 1;
            SaveSession();
            SetStatus($"Saved to {path}: puzzle {flowPosition} of {game.Levels.Count}." +
                      (draft.HasSolution ? "" : " No solution stored yet: win a test run to add one."), true);
            RefreshUi();
        }

        private string UniqueId(string id)
        {
            string candidate = id;
            for (int n = 2; game.Levels.IndexOf(candidate) >= 0; n++)
                candidate = $"{id}-{n}";
            return candidate;
        }

        private int MaxFlowPosition => game.Levels.Count + (sourceId == null || game.Levels.IndexOf(sourceId) < 0 ? 1 : 0);

        private void ShowOpenWindow()
        {
            openList.Clear();
            for (int i = 0; i < game.Levels.Count; i++)
            {
                int index = i;
                TemplateContainer item = levelListItemTemplate.Instantiate();
                var button = item.Q<Button>("levelItem");
                button.text = $"{i + 1}.  {game.Levels.Get(i).Name}";
                button.clicked += () =>
                {
                    OpenExisting(index);
                    HideOpenWindow();
                    RebuildBoard();
                    RefreshUi();
                };
                openList.Add(item);
            }
            openOverlay.RemoveFromClassList(Hidden);
        }

        private void HideOpenWindow() => openOverlay.AddToClassList(Hidden);

        // ------------------------------------------------------------ Edits

        /// <summary>A change to the map or rules: redraw the board, and the old test run no longer fits.</summary>
        private void EditMap(Action edit)
        {
            string before = draft.ContentKey();
            edit();
            if (draft.ContentKey() != before)
            {
                matchingResult = null;
                testMessage = draft.HasSolution ? testMessage : "";
                if (selectedAnimal >= draft.Data.animals.Length) selectedAnimal = -1;
                if (selectedFlag >= draft.Data.flags.Length) selectedFlag = -1;
                RebuildBoard();
            }
            RefreshUi();
        }

        /// <summary>A change that doesn't affect play (name, stars, order, days).</summary>
        private void EditSettings(Action edit)
        {
            edit();
            RefreshUi();
        }

        private void SetStatus(string text, bool ok)
        {
            statusLabel.text = text;
            statusLabel.EnableInClassList("message--ok", ok);
            statusLabel.EnableInClassList("message--error", !ok);
        }

        // ------------------------------------------------------------ Refresh

        private void RefreshUi()
        {
            LevelFile data = draft.Data;
            levelLabel.text = sourceId != null
                ? $"Editing puzzle {Mathf.Max(1, game.Levels.IndexOf(sourceId) + 1)}: {data.name}"
                : $"New level: {data.name} (not saved yet)";

            foreach (var pair in toolButtons)
                pair.Value.EnableInClassList(SelectedChoice, pair.Key == tool);
            foreach (var pair in colourButtons)
                pair.Value.EnableInClassList(SelectedChoice, pair.Key == colour);

            nameField.SetValueWithoutNotify(data.name);
            width.Show(data.width);
            height.Show(data.height);
            days.Show(data.days);
            ticks.Show(data.ticksPerDay);
            goodSweets.Show(draft.GetItemCount(ItemType.GoodSweet));
            badSweets.Show(draft.GetItemCount(ItemType.BadSweet));

            for (int slot = 1; slot <= 2; slot++)
            {
                StarEntry star = draft.GetStar(slot);
                StarKind? kind = star == null ? null : LevelDraft.ParseStarKind(star.type);
                foreach (StarKind? choice in StarChoices)
                    starButtons[(slot, choice)].EnableInClassList(SelectedChoice, choice == kind);
                starLimit[slot].SetVisible(kind.HasValue);
                if (star != null)
                    starLimit[slot].Show(star.value);
            }

            flowPosition = Mathf.Clamp(flowPosition, 1, MaxFlowPosition);
            flow.Show(flowPosition);
            flowHint.text = FlowHint();

            RefreshSelection();

            bool valid = draft.Validate(out _, out string error);
            validationLabel.text = valid ? "Ready to test and save." : error;
            validationLabel.EnableInClassList("message--ok", valid);
            validationLabel.EnableInClassList("message--error", !valid);
            testButton.SetEnabled(valid);
            saveButton.SetEnabled(valid && LevelLibrary.CanSave);

            infiniteDaysButton.text = endlessDays ? "Endless days: on" : $"Endless days: off ({data.days} days)";
            infiniteDaysButton.EnableInClassList(SelectedChoice, endlessDays);
            testResultLabel.text = testMessage;
            starsFromRunButton.EnableInClassList(Hidden, matchingResult == null);
        }

        private string FlowHint()
        {
            List<LevelDefinition> others = game.Levels.Levels.Where(l => l.Id != sourceId).ToList();
            int index = flowPosition - 1;
            string before = index > 0 && index - 1 < others.Count ? $"after \"{others[index - 1].Name}\"" : null;
            string after = index < others.Count ? $"before \"{others[index].Name}\"" : null;
            if (before != null && after != null)
                return $"Played {before}, {after}.";
            return before != null ? $"Played last, {before}." : after != null ? $"Played first, {after}." : "The only puzzle.";
        }

        private void RefreshSelection()
        {
            AnimalEntry animal = draft.Animal(selectedAnimal);
            FlagEntry flag = draft.Flag(selectedFlag);
            noSelection.EnableInClassList(Hidden, animal != null || flag != null);
            animalInspector.EnableInClassList(Hidden, animal == null);
            flagInspector.EnableInClassList(Hidden, flag == null);
            UpdateSelectionMarker();

            if (flag != null && Names.TryParseColor(flag.color, out AnimalColor flagColour))
                flagTitle.text = $"{RuleText.Colour(flagColour)} flag";

            if (animal == null)
                return;

            Names.TryParseColor(animal.color, out AnimalColor animalColour);
            animalTitle.text = $"{RuleText.Colour(animalColour)} animal";
            facingLabel.text = $"Facing {animal.facing}";

            behaviourList.Clear();
            for (int i = 0; i < animal.behaviours.Length; i++)
            {
                int index = i;
                TemplateContainer row = behaviourRowTemplate.Instantiate();
                row.Q<Label>("behaviourText").text = Rule.TryParse(animal.behaviours[i], out Rule rule, out _)
                    ? RuleText.Format(rule)
                    : animal.behaviours[i];
                row.Q<Button>("removeButton").clicked += () => EditMap(() => draft.RemoveBehaviour(selectedAnimal, index));
                behaviourList.Add(row);
            }
            noBehaviours.EnableInClassList(Hidden, animal.behaviours.Length > 0);

            foreach (var pair in kindButtons)
                pair.Value.EnableInClassList(SelectedChoice, pair.Key == addKind);
            bool needsTarget = addKind == BehaviourKind.Hear || addKind == BehaviourKind.See;
            targetSection.EnableInClassList(Hidden, !needsTarget);
            foreach (var pair in targetButtons)
            {
                pair.Value.EnableInClassList(SelectedChoice, pair.Key.Equals(addTarget));
                pair.Value.SetEnabled(addKind != BehaviourKind.Hear || pair.Key.Kind == StimulusKind.Animal);
            }
            feelGood.EnableInClassList(SelectedChoice, addGood);
            feelBad.EnableInClassList(SelectedChoice, !addGood);

            addPreview.text = Rule.TryParse(BehaviourText(), out Rule preview, out _) ? $"Adds: {RuleText.Format(preview)}" : "";
        }
    }
}
