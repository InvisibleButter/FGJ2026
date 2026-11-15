using System;
using System.Collections.Generic;
using FGJ2026.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FGJ2026.UI
{
    /// <summary>
    /// Draws the puzzle board with placeholder shapes: squares, song lines, flags, items and animals.
    /// It only displays a <see cref="PuzzleSimulation"/>; all rules live in the simulation.
    /// </summary>
    public sealed class BoardView
    {
        private sealed class AnimalWidget
        {
            public RectTransform Root;
            public RectTransform Body;
        }

        private readonly LevelDefinition level;
        private readonly float cellSize;
        private readonly Image[,] cells;
        private readonly RectTransform itemLayer;
        private readonly Dictionary<int, AnimalWidget> animals = new Dictionary<int, AnimalWidget>();
        private readonly Dictionary<Vector2Int, (ItemType type, Image image)> items = new Dictionary<Vector2Int, (ItemType, Image)>();
        private readonly Dictionary<Vector2Int, Image> placeHints = new Dictionary<Vector2Int, Image>();

        public RectTransform Root { get; }
        public float CellSize => cellSize;

        /// <summary>Raised with the square and the mouse button when the player clicks a square.</summary>
        public event Action<Vector2Int, PointerEventData.InputButton> CellClicked;

        public BoardView(Transform parent, LevelDefinition level, Vector2 maxSize, bool interactive, float maxCellSize = 120f)
        {
            this.level = level;
            cellSize = Mathf.Floor(Mathf.Min(maxSize.x / level.Width, maxSize.y / level.Height, maxCellSize));

            Root = Ui.CreateRect("Board", parent);
            Root.Pin(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cellSize * level.Width, cellSize * level.Height));

            Image frame = Ui.CreateImage(Root, "Frame", Palette.PanelLight);
            frame.rectTransform.Stretch(-10, -10, -10, -10);
            frame.raycastTarget = false;

            cells = new Image[level.Width, level.Height];
            for (int x = 0; x < level.Width; x++)
            {
                for (int y = 0; y < level.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    Image image = Ui.CreateImage(Root, $"Square {x},{y}", BaseColor(cell));
                    PlaceInCell(image.rectTransform, cell, 1f, -2f);
                    image.raycastTarget = interactive;
                    if (interactive)
                    {
                        var target = image.gameObject.AddComponent<CellClickTarget>();
                        target.Board = this;
                        target.Cell = cell;
                    }
                    cells[x, y] = image;

                    Image hint = Ui.CreateImage(image.transform, "Place hint", new Color(1f, 1f, 1f, 0.22f), Ui.Circle);
                    hint.rectTransform.Pin(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * cellSize * 0.22f);
                    hint.raycastTarget = false;
                    hint.enabled = false;
                    placeHints[cell] = hint;
                }
            }

            foreach (FlagSetup flag in level.Flags)
                CreateFlag(flag);

            itemLayer = Ui.CreateRect("Items", Root).Stretch();
            RectTransform animalLayer = Ui.CreateRect("Animals", Root).Stretch();
            for (int i = 0; i < level.Animals.Count; i++)
                animals[i] = CreateAnimal(animalLayer, level.Animals[i].Color);
        }

        internal void RaiseCellClicked(Vector2Int cell, PointerEventData.InputButton button) => CellClicked?.Invoke(cell, button);

        /// <param name="showPlaceHints">Mark the squares where the selected item could go.</param>
        public void Refresh(PuzzleSimulation sim, bool showPlaceHints = false)
        {
            // Squares, tinted by the songs that cross them.
            var tint = new Dictionary<Vector2Int, Color>();
            foreach (SongLine song in sim.GetSongs())
            {
                Color songColor = Palette.Of(song.Singer.Color);
                foreach (Vector2Int cell in song.Cells)
                {
                    Color current = tint.TryGetValue(cell, out Color existing) ? existing : BaseColor(cell);
                    tint[cell] = Color.Lerp(current, songColor, 0.38f);
                }
            }
            for (int x = 0; x < level.Width; x++)
            {
                for (int y = 0; y < level.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    cells[x, y].color = tint.TryGetValue(cell, out Color color) ? color : BaseColor(cell);
                    placeHints[cell].enabled = showPlaceHints && sim.CanPlaceAt(cell);
                }
            }

            // Items
            var stale = new List<Vector2Int>();
            foreach (var pair in items)
            {
                if (!sim.Items.TryGetValue(pair.Key, out ItemType type) || type != pair.Value.type)
                    stale.Add(pair.Key);
            }
            foreach (Vector2Int cell in stale)
            {
                UnityEngine.Object.Destroy(items[cell].image.gameObject);
                items.Remove(cell);
            }
            foreach (var pair in sim.Items)
            {
                if (!items.ContainsKey(pair.Key))
                    items[pair.Key] = (pair.Value, CreateItem(pair.Key, pair.Value));
            }

            // Animals
            foreach (Animal animal in sim.Animals)
            {
                AnimalWidget widget = animals[animal.Index];
                widget.Root.gameObject.SetActive(!animal.OffMap);
                PlaceInCell(widget.Root, animal.Cell, 1f, 0f);
                widget.Body.localEulerAngles = new Vector3(0f, 0f, Directions.Angle(animal.Facing));
            }
        }

        private Color BaseColor(Vector2Int cell)
        {
            if (level.IsBlocked(cell))
                return Palette.Blocked;
            return (cell.x + cell.y) % 2 == 0 ? Palette.Cell : Palette.CellAlt;
        }

        private Vector2 CellCenter(Vector2Int cell) =>
            new Vector2((cell.x + 0.5f - level.Width * 0.5f) * cellSize, (cell.y + 0.5f - level.Height * 0.5f) * cellSize);

        private void PlaceInCell(RectTransform rect, Vector2Int cell, float scale, float padding)
        {
            rect.Pin(new Vector2(0.5f, 0.5f), CellCenter(cell), Vector2.one * (cellSize * scale + padding));
        }

        private void CreateFlag(FlagSetup flag)
        {
            Color color = Palette.Of(flag.Color);
            RectTransform root = Ui.CreateRect($"{flag.Color} flag", Root);
            PlaceInCell(root, flag.Cell, 1f, -2f);

            float thickness = Mathf.Max(3f, cellSize * 0.07f);
            void Bar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
            {
                Image bar = Ui.CreateImage(root, name, color);
                bar.raycastTarget = false;
                bar.rectTransform.anchorMin = anchorMin;
                bar.rectTransform.anchorMax = anchorMax;
                bar.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                bar.rectTransform.anchoredPosition = Vector2.zero;
                bar.rectTransform.sizeDelta = size;
            }
            Bar("Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, thickness * 2));
            Bar("Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, thickness * 2));
            Bar("Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(thickness * 2, 0));
            Bar("Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(thickness * 2, 0));

            Image pole = Ui.CreateImage(root, "Pole", Palette.TextDark);
            pole.raycastTarget = false;
            pole.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(-cellSize * 0.18f, 0f), new Vector2(Mathf.Max(2f, cellSize * 0.04f), cellSize * 0.56f));
            Image pennant = Ui.CreateImage(root, "Pennant", color, Ui.Triangle);
            pennant.raycastTarget = false;
            pennant.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(cellSize * 0.0f, cellSize * 0.12f), new Vector2(cellSize * 0.36f, cellSize * 0.3f));
        }

        private Image CreateItem(Vector2Int cell, ItemType type)
        {
            Image image = Ui.CreateImage(itemLayer, $"{type} {cell}", Palette.Of(type), Ui.Circle);
            image.raycastTarget = false;
            PlaceInCell(image.rectTransform, cell, 0.42f, 0f);

            TextMeshProUGUI mark = Ui.CreateText(image.transform, type == ItemType.GoodSweet ? "+" : "-", cellSize * 0.32f,
                type == ItemType.GoodSweet ? Palette.TextDark : Palette.Text);
            mark.rectTransform.Stretch();
            mark.fontStyle = FontStyles.Bold;
            return image;
        }

        private AnimalWidget CreateAnimal(Transform layer, AnimalColor color)
        {
            RectTransform root = Ui.CreateRect($"{color} animal", layer);
            RectTransform body = Ui.CreateRect("Body", root);
            body.Pin(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * cellSize);

            Image shadow = Ui.CreateImage(body, "Outline", Palette.TextDark, Ui.Circle);
            shadow.raycastTarget = false;
            shadow.rectTransform.Pin(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * cellSize * 0.76f);

            Image fill = Ui.CreateImage(body, "Fill", Palette.Of(color), Ui.Circle);
            fill.raycastTarget = false;
            fill.rectTransform.Pin(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * cellSize * 0.68f);

            // The "nose" shows where the animal faces (body rotation 0 = east).
            Image nose = Ui.CreateImage(body, "Facing", Palette.TextDark, Ui.Triangle);
            nose.raycastTarget = false;
            nose.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(cellSize * 0.4f, 0f), Vector2.one * cellSize * 0.22f);

            Image eye = Ui.CreateImage(body, "Eye", Color.white, Ui.Circle);
            eye.raycastTarget = false;
            eye.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(cellSize * 0.14f, 0f), Vector2.one * cellSize * 0.16f);

            return new AnimalWidget { Root = root, Body = body };
        }
    }
}
