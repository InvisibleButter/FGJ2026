using UnityEngine.UIElements;

namespace FGJ2026.UI
{
    /// <summary>Fills a StarRow.uxml template: star icon, condition text and an optional note.</summary>
    public static class StarRowView
    {
        public static VisualElement Add(VisualElement parent, VisualTreeAsset template, string description, bool earned, string note)
        {
            TemplateContainer row = template.Instantiate();
            row.Q("star").EnableInClassList("star--on", earned);
            row.Q("starRow").EnableInClassList("star-row--off", !earned);
            row.Q<Label>("description").text = description;
            row.Q<Label>("note").text = note ?? string.Empty;
            parent.Add(row);
            return row;
        }
    }
}
