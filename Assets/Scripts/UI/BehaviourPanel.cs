using System.Collections.Generic;
using System.Linq;
using FGJ2026.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FGJ2026.UI
{
    /// <summary>
    /// The behaviour list of one animal: a box with one block per behaviour.
    /// Blocks learned today are half transparent; a block lights up when it is used.
    /// </summary>
    public sealed class BehaviourPanel
    {
        private sealed class Block
        {
            public Image Background;
            public TextMeshProUGUI Text;
            public float Glow;
            public bool Pending;
        }

        private readonly RectTransform blockList;
        private readonly TextMeshProUGUI status;
        private readonly Dictionary<int, Block> blocks = new Dictionary<int, Block>();
        private readonly List<int> order = new List<int>();
        private TextMeshProUGUI emptyLabel;

        public int AnimalIndex { get; }
        public RectTransform Root { get; }

        public BehaviourPanel(Transform parent, Animal animal)
        {
            AnimalIndex = animal.Index;

            Image box = Ui.CreateImage(parent, $"{animal.Color} behaviours", Palette.Panel);
            Root = box.rectTransform;
            box.Vertical(8, 14);

            RectTransform header = Ui.CreateRect("Header", Root);
            header.Horizontal(12);
            header.Size(-1, 40);
            Image swatch = Ui.CreateImage(header, "Colour", Palette.Of(animal.Color), Ui.Circle);
            swatch.Size(36, 36);
            TextMeshProUGUI title = Ui.CreateText(header, RuleText.Colour(animal.Color), 30, Palette.Text, TextAlignmentOptions.MidlineLeft);
            title.Size(-1, 40, 1);

            status = Ui.CreateText(Root, "", 20, Palette.TextDim, TextAlignmentOptions.MidlineLeft);
            status.Size(-1, 26);

            blockList = Ui.CreateRect("Blocks", Root);
            blockList.Vertical(6);
            Rebuild(animal);
        }

        public void Refresh(Animal animal)
        {
            if (animal.OffMap)
                status.text = "left the map";
            else if (animal.OnFlag)
                status.text = $"<color={Palette.Hex(Palette.Good)}>reached its flag</color>";
            else
                status.text = $"facing {Directions.Name(animal.Facing)}";

            List<int> ids = animal.Rules.Select(r => r.Id).ToList();
            if (!ids.SequenceEqual(order))
                Rebuild(animal);

            foreach (Rule rule in animal.Rules)
            {
                Block block = blocks[rule.Id];
                block.Pending = rule.Pending;
                block.Text.text = rule.Pending
                    ? $"{RuleText.Format(rule)}\n<size=75%><i>learned today - works from tomorrow</i></size>"
                    : RuleText.Format(rule);
            }
            Apply();
        }

        /// <summary>Lights a block up. Strength above 1 glows longer (used when a lesson becomes active).</summary>
        public void Trigger(int ruleId, float strength = 1f)
        {
            if (blocks.TryGetValue(ruleId, out Block block))
                block.Glow = Mathf.Max(block.Glow, strength);
        }

        public void Tick(float deltaTime)
        {
            foreach (Block block in blocks.Values)
                block.Glow = Mathf.MoveTowards(block.Glow, 0f, deltaTime * 1.4f);
            Apply();
        }

        private void Apply()
        {
            foreach (Block block in blocks.Values)
            {
                Color color = Color.Lerp(Palette.PanelLight, Palette.Highlight, Mathf.Clamp01(block.Glow) * 0.55f);
                color.a = block.Pending ? 0.45f : 1f;
                block.Background.color = color;
                block.Text.alpha = block.Pending ? 0.6f : 1f;
            }
        }

        private void Rebuild(Animal animal)
        {
            var oldGlow = blocks.ToDictionary(pair => pair.Key, pair => pair.Value.Glow);
            Ui.DestroyChildren(blockList);
            blocks.Clear();
            order.Clear();

            foreach (Rule rule in animal.Rules)
            {
                Image background = Ui.CreateImage(blockList, $"Block {rule}", Palette.PanelLight);
                background.raycastTarget = false;
                var layout = background.Vertical(0, 12, TextAnchor.MiddleLeft);
                layout.padding = new RectOffset(14, 14, 10, 10);
                TextMeshProUGUI text = Ui.CreateText(background.transform, "", 24, Palette.Text, TextAlignmentOptions.MidlineLeft);
                background.gameObject.AddComponent<LayoutElement>().minHeight = 52;

                blocks[rule.Id] = new Block
                {
                    Background = background,
                    Text = text,
                    Glow = oldGlow.TryGetValue(rule.Id, out float glow) ? glow : 0f
                };
                order.Add(rule.Id);
            }

            if (animal.Rules.Count == 0)
            {
                emptyLabel = Ui.CreateText(blockList, "<i>does nothing</i>", 22, Palette.TextDim, TextAlignmentOptions.MidlineLeft);
                emptyLabel.Size(-1, 40);
            }
        }
    }
}
