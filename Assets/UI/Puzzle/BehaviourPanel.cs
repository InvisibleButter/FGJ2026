using System.Collections.Generic;
using System.Linq;
using FGJ2026.Simulation;
using UnityEngine.UIElements;

namespace FGJ2026.UI
{
    /// <summary>
    /// One animal's behaviour list (BehaviourPanel.uxml) with a block per behaviour (BehaviourBlock.uxml).
    /// Blocks learned today get the pending class (half transparent); used blocks briefly get the active class,
    /// newly confirmed lessons the learned class. The fading is done by USS transitions.
    /// </summary>
    public sealed class BehaviourPanel
    {
        private const string PendingClass = "behaviour-block--pending";
        private const string ActiveClass = "behaviour-block--active";
        private const string LearnedClass = "behaviour-block--learned";

        private readonly VisualTreeAsset blockTemplate;
        private readonly VisualElement blockList;
        private readonly Label status;
        private readonly Label emptyLabel;
        private readonly Dictionary<int, (VisualElement block, Label text)> blocks = new Dictionary<int, (VisualElement, Label)>();
        private readonly List<int> order = new List<int>();
        private readonly string colourName;

        public int AnimalIndex { get; }
        public VisualElement Root { get; }

        public BehaviourPanel(VisualElement parent, VisualTreeAsset panelTemplate, VisualTreeAsset blockTemplate, Animal animal)
        {
            this.blockTemplate = blockTemplate;
            AnimalIndex = animal.Index;

            Root = panelTemplate.Instantiate();
            parent.Add(Root);
            Root.Q("swatch").AddToClassList(ColorClass(animal.Color));
            colourName = animal.Color.ToString().ToLowerInvariant();
            Root.Q("behaviourPanel").AddToClassList("behaviour-panel--" + colourName);
            Root.Q("behaviourPanel").EnableInClassList("behaviour-panel--light", IsLight(animal.Color));
            Root.Q<Label>("animalName").text = animal.Color.ToString();
            status = Root.Q<Label>("status");
            blockList = Root.Q("blockList");
            emptyLabel = Root.Q<Label>("emptyLabel");
            Rebuild(animal);
        }

        public static string ColorClass(AnimalColor color) => "color-" + color.ToString().ToLowerInvariant();

        /// <summary>Light panel colours get dark text.</summary>
        private static bool IsLight(AnimalColor color)
        {
            UnityEngine.Color c = Palette.Of(color).linear;
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b > 0.2f;
        }

        public void Refresh(Animal animal)
        {
            if (animal.OffMap)
                status.text = "left the map";
            else if (animal.OnFlag)
                status.text = "reached its flag";
            else
                status.text = $"facing {Directions.Name(animal.Facing)}";

            if (!animal.Rules.Select(r => r.Id).SequenceEqual(order))
                Rebuild(animal);

            foreach (Rule rule in animal.Rules)
            {
                var (block, text) = blocks[rule.Id];
                block.EnableInClassList(PendingClass, rule.Pending);
                text.text = rule.Pending
                    ? $"{RuleText.Format(rule, onLight: true)}\n<size=75%><i>learned today - works from tomorrow</i></size>"
                    : RuleText.Format(rule, onLight: true);
            }
        }

        /// <summary>Lights a block up for a moment. <paramref name="learned"/>: a lesson just became active.</summary>
        public void Trigger(int ruleId, bool learned = false)
        {
            if (!blocks.TryGetValue(ruleId, out var entry))
                return;

            string className = learned ? LearnedClass : ActiveClass;
            VisualElement block = entry.block;
            block.AddToClassList(className);
            block.schedule.Execute(() => block.RemoveFromClassList(className)).StartingIn(learned ? 1500 : 300);
        }

        private void Rebuild(Animal animal)
        {
            blockList.Clear();
            blocks.Clear();
            order.Clear();

            foreach (Rule rule in animal.Rules)
            {
                TemplateContainer instance = blockTemplate.Instantiate();
                blockList.Add(instance);
                instance.Q("block").AddToClassList("behaviour-block--" + colourName);
                blocks[rule.Id] = (instance.Q("block"), instance.Q<Label>("blockText"));
                order.Add(rule.Id);
            }

            emptyLabel.EnableInClassList("hidden", animal.Rules.Count > 0);
        }
    }
}
