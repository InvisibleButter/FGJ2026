using System.Collections.Generic;
using FGJ2026.Simulation;
using FGJ2026.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FGJ2026.Board
{
    /// <summary>
    /// The puzzle board in the game world, built with Unity's Tilemap system.
    /// One square = one tile = one unit = 16 pixels.
    ///
    /// Tilemap layers, bottom to top:
    ///   Underlay (water under the whole board and a margin) -> Obstacles (optional, on blocked squares) -> Grass (rule tile) -> Grass details (tufts) ->
    ///   Grid (faint lines between squares) -> Songs (tinted lines) -> Place hints (dots)
    /// Then sprites: flags, sweets, facing arrows, animals, hover cursor. Animals and flags use their colour's art from
    /// <see cref="BoardArt.colorVariants"/>; colours without art get a coloured spot / flag frame instead.
    ///
    /// It only shows a <see cref="PuzzleSimulation"/>; all rules live in the simulation.
    /// </summary>
    public sealed class WorldBoard
    {
        public const int PixelsPerUnit = 16;

        // Sorting orders (all on the Default sorting layer).
        private const int UnderlayOrder = -7, WaterOrder = -6, GrassOrder = -5, DetailOrder = -4, GridOrder = -3, SongOrder = -2, HintOrder = -1;
        private const int FlagFrameOrder = 0, FlagOrder = 1, ItemOrder = 2, ArrowOrder = 3, SpotOrder = 4, AnimalOrder = 5, CursorOrder = 10;

        private const float MoveDuration = 0.3f; // seconds an animal takes to slide to the next square

        private const string Idle = "Animal_Idle", Move = "Animal_Move", Eat = "Animal_Eat",
            Sing = "Animal_Sing", Love = "Animal_Love", Sad = "Animal_Sad";

        private sealed class AnimalView
        {
            public GameObject Root;
            public GameObject Visuals;
            public Animator Animator;
            public AudioSource AudioSource;
            public BoardArt.ColorVariant Variant;
            public Vector2Int Facing;
            public Transform Arrow;
            public Vector3 Position;
            public Vector3 Target;
            public string State;
            public bool Sings;
            public bool PlanningSinging;
        }

        private readonly LevelDefinition level;
        private readonly BoardArt art;
        private readonly SoundService sound;
        private readonly Tilemap songs, hints;
        private readonly Tile whiteTile, dotTile;
        private readonly Tile[] noteTiles;
        private readonly Transform objects;
        private readonly Dictionary<int, AnimalView> animals = new Dictionary<int, AnimalView>();
        private readonly Dictionary<Vector2Int, (ItemType type, GameObject view)> items = new Dictionary<Vector2Int, (ItemType, GameObject)>();
        private readonly SpriteRenderer cursor;

        private PuzzleSimulation lastSim;
        private PuzzleSimulation previousSim;
        private bool lastShowHints;

        public GameObject Root { get; }
        public Grid Grid { get; }
        public Vector2Int? HoveredCell { get; private set; }

        /// <summary>The board in world units, with one square of margin (room for facing arrows at the edge).</summary>
        public Rect FramingRect => new Rect(Root.transform.position.x - 1f, Root.transform.position.y - 1f, level.Width + 2f, level.Height + 2f);

        public WorldBoard(LevelDefinition level, Vector3 origin, bool interactive, SoundService sound = null)
        {
            this.level = level;
            this.sound = sound;
            art = BoardArt.Load();

            Root = new GameObject($"Board - {level.Name}");
            Root.transform.position = origin;
            Grid = Root.AddComponent<Grid>();
            Grid.cellSize = Vector3.one;

            whiteTile = MakeTile(PixelSprites.White);
            dotTile = MakeTile(PixelSprites.Dot);
            var notes = new List<Tile>();
            if (art != null && art.songNotes != null)
                foreach (Sprite note in art.songNotes)
                    if (note != null)
                        notes.Add(MakeTile(note));
            noteTiles = notes.ToArray();

            Tilemap underlay = CreateLayer("Underlay", UnderlayOrder);
            Tilemap water = CreateLayer("Obstacles", WaterOrder);
            Tilemap grass = CreateLayer("Grass", GrassOrder);
            Tilemap details = CreateLayer("Grass details", DetailOrder);
            Tilemap grid = CreateLayer("Grid", GridOrder);
            songs = CreateLayer("Songs", SongOrder);
            hints = CreateLayer("Place hints", HintOrder);
            PaintUnderlay(underlay);
            PaintGround(water, grass, details, grid);

            objects = new GameObject("Objects").transform;
            objects.SetParent(Root.transform, false);

            foreach (FlagSetup flag in level.Flags)
                CreateFlag(flag);
            for (int i = 0; i < level.Animals.Count; i++)
                animals[i] = CreateAnimal(level.Animals[i]);

            if (interactive)
            {
                cursor = CreateSprite("Hover cursor", objects, PixelSprites.Frame, CursorOrder, art != null ? art.hoverCursor : null);
                cursor.gameObject.SetActive(false);
            }
        }

        public void Destroy() => Object.Destroy(Root);

        // ------------------------------------------------------------ Queries

        public bool TryGetCell(Vector3 worldPosition, out Vector2Int cell)
        {
            cell = (Vector2Int)Grid.WorldToCell(worldPosition);
            return level.Contains(cell);
        }

        public Vector3 CellCenter(Vector2Int cell) => Root.transform.position + new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        // ------------------------------------------------------------ Updates

        /// <summary>Shows the simulation's current state. Call after every change.</summary>
        public void Refresh(PuzzleSimulation sim, bool showPlaceHints = false)
        {
            lastSim = sim;
            lastShowHints = showPlaceHints;

            RefreshSongs(sim);
            RefreshHints(sim, showPlaceHints);
            RefreshItems(sim);

            foreach (Animal animal in sim.Animals)
            {
                AnimalView view = animals[animal.Index];
                view.Visuals.SetActive(!animal.OffMap);
                view.Arrow.gameObject.SetActive(!animal.OffMap && !animal.OnFlag);
                view.Arrow.localEulerAngles = new Vector3(0f, 0f, Directions.Angle(animal.Facing));
                Face(view, animal.Facing);

                Vector3 target = CellCenter(animal.Cell);
                if ((target - view.Position).sqrMagnitude > 2.1f)
                    view.Position = target; // a new day: jump back to the start square
                view.Target = target;

                if (sim.Phase == PuzzlePhase.Preparation)
                    Play(view, view.PlanningSinging ? Sing : Idle);
                else if (animal.OnFlag)
                    Play(view, Love);
            }
            ApplyPositions();
            UpdateCursor();
        }

        public void CapturePreviousSim(PuzzleSimulation sim) => previousSim = sim.CreateSnapshot();

        public Animal PickIdleAnimal()
        {
            if (lastSim == null)
                return null;

            Animal selected = null;
            int count = 0;
            foreach (Animal animal in lastSim.Animals)
            {
                AnimalView view = animals[animal.Index];
                if (view.State == Idle && !animal.OnlySings && view.Visuals.activeInHierarchy && Random.Range(0, ++count) == 0)
                    selected = animal;
            }
            return selected;
        }

        public AudioSource PlayAnimalSound(Animal animal, AnimalSoundCue cue)
        {
            AudioSource source = animals[animal.Index].AudioSource;
            return sound != null && sound.TryPlayAnimalSound(source, animal.Color, cue) ? source : null;
        }

        public AudioSource StartPlanningSong(Animal animal)
        {
            AudioSource source = PlayAnimalSound(animal, AnimalSoundCue.Sing);
            if (source == null)
                return null;

            AnimalView view = animals[animal.Index];
            view.PlanningSinging = true;
            Play(view, Sing);
            return source;
        }

        public void StopPlanningSong(Animal animal) => StopPlanningSong(animals[animal.Index]);

        public void StopPlanningSongs()
        {
            foreach (AnimalView view in animals.Values)
                StopPlanningSong(view);
        }

        private static void StopPlanningSong(AnimalView view)
        {
            if (!view.PlanningSinging)
                return;

            view.PlanningSinging = false;
            view.AudioSource.Stop();
            Play(view, Idle);
        }

        public void StopAnimalSounds()
        {
            foreach (AnimalView view in animals.Values)
            {
                if (view.AudioSource != null)
                    view.AudioSource.Stop();
            }
        }

        /// <summary>Plays the animations for what the animals did in a tick.</summary>
        public void PlayTick(TickReport report, PuzzleSimulation sim)
        {
            foreach (Animal animal in sim.Animals)
            {
                AnimalView view = animals[animal.Index];
                report.Actions.TryGetValue(animal.Index, out AnimalAction action);
                Face(view, animal.Facing);
                switch (action)
                {
                    case AnimalAction.Move:
                    case AnimalAction.LeaveMap: Play(view, Move); break;
                    case AnimalAction.Eat: Play(view, Eat); break;
                    case AnimalAction.ReachFlag: Play(view, Love); break;
                    case AnimalAction.Wait:
                        Play(view, Sad);
                        if (previousSim?.Animals[animal.Index].LastAction != AnimalAction.Wait)
                            PlayAnimalSound(animal, AnimalSoundCue.Sad);
                        break;
                    default: Play(view, view.Sings && !animal.OnFlag ? Sing : animal.OnFlag ? Love : Idle); break;
                }
            }
        }

        /// <summary>Slides the animals towards their squares. Call every frame.</summary>
        public void Tick(float deltaTime)
        {
            float step = deltaTime / MoveDuration;
            foreach (AnimalView view in animals.Values)
                view.Position = Vector3.MoveTowards(view.Position, view.Target, step);
            ApplyPositions();
        }

        public void SetHover(Vector2Int? cell)
        {
            if (HoveredCell == cell)
                return;
            HoveredCell = cell;
            UpdateCursor();
        }

        // ------------------------------------------------------------ Camera

        /// <summary>
        /// Points an orthographic camera at a world rectangle so that it fills <paramref name="screenArea"/>
        /// (in screen pixels) at the largest whole-number zoom: every art pixel becomes an exact block of
        /// screen pixels. Returns the zoom (screen pixels per art pixel).
        /// </summary>
        public static int FrameCamera(Camera camera, Rect worldRect, Rect screenArea)
        {
            int zoom = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(
                screenArea.width / (worldRect.width * PixelsPerUnit),
                screenArea.height / (worldRect.height * PixelsPerUnit))));
            float screenPixelsPerUnit = PixelsPerUnit * zoom;

            camera.orthographic = true;
            camera.orthographicSize = camera.pixelHeight / (2f * screenPixelsPerUnit);

            var screenCenter = new Vector2(camera.pixelWidth, camera.pixelHeight) * 0.5f;
            Vector2 position = worldRect.center - (screenArea.center - screenCenter) / screenPixelsPerUnit;

            // Snap so that whole world units land exactly on screen pixel edges.
            position.x = (Mathf.Round(position.x * screenPixelsPerUnit - screenCenter.x) + screenCenter.x) / screenPixelsPerUnit;
            position.y = (Mathf.Round(position.y * screenPixelsPerUnit - screenCenter.y) + screenCenter.y) / screenPixelsPerUnit;
            camera.transform.position = new Vector3(position.x, position.y, -10f);
            return zoom;
        }

        // ------------------------------------------------------------ Building

        private Tilemap CreateLayer(string name, int order)
        {
            var layer = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(Root.transform, false);
            layer.GetComponent<TilemapRenderer>().sortingOrder = order;
            return layer.GetComponent<Tilemap>();
        }

        private static Tile MakeTile(Sprite sprite)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.flags = TileFlags.None; // allow per-tile colours
            tile.transform = Matrix4x4.Translate(PixelOffset(sprite)); // odd-sized sprites back on the pixel grid
            tile.hideFlags = HideFlags.DontSave;
            return tile;
        }

        /// <summary>Fills the board and a one-square margin (the framed area) with the underlay tile.</summary>
        private void PaintUnderlay(Tilemap underlay)
        {
            for (int x = -1; x <= level.Width; x++)
            {
                for (int y = -1; y <= level.Height; y++)
                {
                    var position = new Vector3Int(x, y, 0);
                    if (art != null && art.underlay != null)
                    {
                        underlay.SetTile(position, art.underlay);
                    }
                    else
                    {
                        underlay.SetTile(position, whiteTile);
                        underlay.SetColor(position, Palette.Blocked);
                    }
                }
            }
        }

        private void PaintGround(Tilemap water, Tilemap grass, Tilemap details, Tilemap grid)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (AnimalSetup animal in level.Animals)
                occupied.Add(animal.Cell);
            foreach (FlagSetup flag in level.Flags)
                occupied.Add(flag.Cell);

            TileBase[] decorations = art != null && art.grassDetails != null ? art.grassDetails : new TileBase[0];
            float density = art != null ? art.grassDetailDensity : 0f;
            var random = new System.Random(StableHash(level.Id)); // same decorations every time for a level
            Tile gridTile = MakeTile(PixelSprites.GridLines);
            Tile gridLeft = MakeTile(PixelSprites.GridLeft);
            Tile gridBottom = MakeTile(PixelSprites.GridBottom);

            for (int x = 0; x < level.Width; x++)
            {
                for (int y = 0; y < level.Height; y++)
                {
                    var position = new Vector3Int(x, y, 0);
                    var cell = new Vector2Int(x, y);

                    if (level.IsBlocked(cell))
                    {
                        // A hole in the grass: the underlay shows through, plus an optional obstacle tile.
                        if (art != null && art.water != null)
                            water.SetTile(position, art.water);
                        continue;
                    }

                    if (art != null && art.grass != null)
                    {
                        grass.SetTile(position, art.grass);
                    }
                    else
                    {
                        grass.SetTile(position, whiteTile);
                        grass.SetColor(position, Palette.Cell);
                    }

                    // Faint grid so squares are easy to count. Lines only between two grass squares.
                    Tile line = x > 0 && y > 0 ? gridTile : x > 0 ? gridLeft : y > 0 ? gridBottom : null;
                    if (line != null)
                    {
                        grid.SetTile(position, line);
                        grid.SetColor(position, new Color(0f, 0f, 0f, 0.12f));
                    }

                    double roll = random.NextDouble();
                    int pick = random.Next(Mathf.Max(1, decorations.Length));
                    if (decorations.Length > 0 && !occupied.Contains(cell) && roll < density && decorations[pick] != null)
                        details.SetTile(position, decorations[pick]);
                }
            }
        }

        private void CreateFlag(FlagSetup flag)
        {
            Vector3 center = CellCenter(flag.Cell);
            BoardArt.ColorVariant variant = art != null ? art.VariantFor(flag.Color) : null;

            // Colours without their own flag art get a frame in the animal's colour to say whose flag it is.
            if (variant?.flag == null)
            {
                SpriteRenderer frame = CreateSprite($"{flag.Color} flag frame", objects, PixelSprites.Frame, FlagFrameOrder);
                frame.transform.position = center;
                frame.color = Palette.Of(flag.Color);
            }

            RuntimeAnimatorController controller = variant?.flag != null ? variant.flag : art != null ? art.flag : null;
            if (controller != null)
            {
                SpriteRenderer animated = CreateSprite($"{flag.Color} flag", objects, null, FlagOrder, controller);
                animated.transform.position = center;
            }
        }

        private AnimalView CreateAnimal(AnimalSetup setup)
        {
            var root = new GameObject($"{setup.Color} animal");
            root.transform.SetParent(objects, false);

            var visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform, false);
            SpriteRenderer spot = CreateSprite("Colour spot", visuals.transform, PixelSprites.Shadow, SpotOrder);
            spot.color = Palette.Of(setup.Color);
            BoardArt.ColorVariant variant = art != null ? art.VariantFor(setup.Color) : null;
            RuntimeAnimatorController controller = variant?.animal != null ? variant.AnimalFacing(setup.Facing) : art != null ? art.animal : null;

            AudioSource audioSource = null;
            if (sound != null)
            {
                audioSource = root.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 0f;
            }

            // Colours without their own art get a coloured spot under the animal.
            if (variant?.animal == null)
            {
                SpriteRenderer spot = CreateSprite("Colour spot", root.transform, PixelSprites.Shadow, SpotOrder);
                spot.color = Palette.Of(setup.Color);
            }

            Animator animator = null;
            if (controller != null)
            {
                SpriteRenderer body = CreateSprite("Body", root.transform, null, AnimalOrder, controller);
                animator = body.GetComponent<Animator>();
            }
            else
            {
                SpriteRenderer body = CreateSprite("Body", visuals.transform, PixelSprites.Disc, AnimalOrder);
                body.color = Palette.Of(setup.Color);
            }

            // The arrow sits on the square in front of the animal and turns with it.
            SpriteRenderer arrow = CreateSprite($"{setup.Color} facing", objects, PixelSprites.Dot, ArrowOrder,
                art != null ? art.facingDirection : null);

            Vector3 start = CellCenter(setup.Cell);
            var view = new AnimalView
            {
                Root = root,
                Visuals = visuals,
                Animator = animator,
                AudioSource = audioSource,
                Variant = variant,
                Facing = setup.Facing,
                Arrow = arrow.transform,
                Position = start,
                Target = start,
                Sings = false
            };
            foreach (Rule rule in setup.Rules)
                view.Sings |= rule.Kind == RuleKind.Sing;
            Play(view, Idle);
            return view;
        }

        private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, int order,
            RuntimeAnimatorController controller = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            if (controller != null)
                go.AddComponent<Animator>().runtimeAnimatorController = controller;
            return renderer;
        }

        // ------------------------------------------------------------ Refresh helpers

        private void RefreshSongs(PuzzleSimulation sim)
        {
            songs.ClearAllTiles();
            var colours = new Dictionary<Vector2Int, (Color sum, int count)>();
            foreach (SongLine song in sim.GetSongs())
            {
                Color colour = Palette.Of(song.Singer.Color);
                foreach (Vector2Int cell in song.Cells)
                {
                    colours.TryGetValue(cell, out var entry);
                    colours[cell] = (entry.sum + colour, entry.count + 1);
                }
            }
            foreach (var pair in colours)
            {
                var position = (Vector3Int)pair.Key;
                Color colour = pair.Value.sum / pair.Value.count;
                if (noteTiles.Length > 0)
                {
                    // A note per square, alternating, in the singer's colour.
                    songs.SetTile(position, noteTiles[(position.x + position.y) % noteTiles.Length]);
                    colour.a = 1f;
                }
                else
                {
                    songs.SetTile(position, whiteTile);
                    colour.a = 0.35f;
                }
                songs.SetColor(position, colour);
            }
        }

        private void RefreshHints(PuzzleSimulation sim, bool show)
        {
            hints.ClearAllTiles();
            if (!show)
                return;
            for (int x = 0; x < level.Width; x++)
            {
                for (int y = 0; y < level.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!sim.CanPlaceAt(cell))
                        continue;
                    hints.SetTile((Vector3Int)cell, dotTile);
                    hints.SetColor((Vector3Int)cell, new Color(1f, 1f, 1f, 0.55f));
                }
            }
        }

        private void RefreshItems(PuzzleSimulation sim)
        {
            var stale = new List<Vector2Int>();
            foreach (var pair in items)
            {
                if (!sim.Items.TryGetValue(pair.Key, out ItemType type) || type != pair.Value.type)
                    stale.Add(pair.Key);
            }
            foreach (Vector2Int cell in stale)
            {
                Object.Destroy(items[cell].view);
                items.Remove(cell);
            }

            foreach (var pair in sim.Items)
            {
                if (items.ContainsKey(pair.Key))
                    continue;

                Sprite sprite = art == null ? null : pair.Value == ItemType.GoodSweet ? art.goodSweet : art.badSweet;
                SpriteRenderer item = CreateSprite($"{pair.Value} {pair.Key}", objects, sprite != null ? sprite : PixelSprites.Disc, ItemOrder);
                if (sprite == null)
                {
                    item.color = Palette.Of(pair.Value);
                    item.transform.localScale = Vector3.one * 0.5f;
                }
                item.transform.position = CellCenter(pair.Key) + PixelOffset(item.sprite);
                items[pair.Key] = (pair.Value, item.gameObject);
            }
        }

        private void ApplyPositions()
        {
            foreach (var pair in animals)
            {
                AnimalView view = pair.Value;
                Vector3 snapped = Snap(view.Position);
                view.Root.transform.position = snapped;

                Vector3 facing = Quaternion.Euler(view.Arrow.localEulerAngles) * Vector3.right;
                view.Arrow.position = snapped + new Vector3(Mathf.Round(facing.x), Mathf.Round(facing.y), 0f);
            }
        }

        private void UpdateCursor()
        {
            if (cursor == null)
                return;

            cursor.gameObject.SetActive(HoveredCell.HasValue);
            if (!HoveredCell.HasValue)
                return;

            Vector2Int cell = HoveredCell.Value;
            cursor.transform.position = CellCenter(cell);

            // While placing items, dim the cursor over squares where nothing can be done.
            bool usable = lastSim == null || !lastShowHints || lastSim.CanPlaceAt(cell) || lastSim.IsPlacedToday(cell);
            cursor.color = usable ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        /// <summary>Swaps to the animations for the animal's facing direction, keeping the current state and frame.</summary>
        private static void Face(AnimalView view, Vector2Int facing)
        {
            if (view.Facing == facing)
                return;
            view.Facing = facing;
            if (view.Animator == null || view.Variant?.animal == null)
                return;

            RuntimeAnimatorController controller = view.Variant.AnimalFacing(facing);
            if (controller == null || view.Animator.runtimeAnimatorController == controller)
                return;

            bool playing = view.Animator.gameObject.activeInHierarchy && view.State != null;
            float time = playing ? view.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f;
            view.Animator.runtimeAnimatorController = controller;
            if (playing)
                view.Animator.Play(view.State, 0, time);
        }

        private static void Play(AnimalView view, string state)
        {
            if (view.Animator == null || view.State == state)
                return;
            if (!view.Animator.gameObject.activeInHierarchy)
            {
                view.State = null; // hidden (off the map); starts fresh when shown again
                return;
            }
            view.State = state;
            view.Animator.Play(state, 0, 0f);
        }

        // ------------------------------------------------------------ Pixel helpers

        /// <summary>Rounds a position to whole art pixels.</summary>
        private static Vector3 Snap(Vector3 position) => new Vector3(
            Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit,
            Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit,
            position.z);

        /// <summary>
        /// Sprites with an odd size (e.g. 15 px wide) have a pivot on half a pixel; this offset puts their
        /// pixels back on the pixel grid.
        /// </summary>
        private static Vector3 PixelOffset(Sprite sprite)
        {
            if (sprite == null)
                return Vector3.zero;
            float fx = sprite.pivot.x - Mathf.Floor(sprite.pivot.x);
            float fy = sprite.pivot.y - Mathf.Floor(sprite.pivot.y);
            return new Vector3(fx, fy, 0f) / sprite.pixelsPerUnit;
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text ?? string.Empty)
                    hash = hash * 31 + c;
                return hash;
            }
        }
    }
}
