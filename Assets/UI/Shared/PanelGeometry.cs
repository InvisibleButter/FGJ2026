using UnityEngine;
using UnityEngine.UIElements;

namespace FGJ2026.UI
{
    /// <summary>Converts between UI Toolkit panel space and screen pixels, for screens that show the world board.</summary>
    public static class PanelGeometry
    {
        /// <summary>
        /// True when the mouse (screen pixels, origin bottom left) is over a UI element that takes input.
        /// Containers that should let clicks through to the board use picking-mode="Ignore".
        /// </summary>
        public static bool IsPointerOverUi(VisualElement root, Vector2 screenPosition)
        {
            IPanel panel = root.panel;
            if (panel == null)
                return false;
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return panel.Pick(panelPosition) != null;
        }

        /// <summary>The element's rectangle in screen pixels (origin bottom left), once the UI has been laid out.</summary>
        public static bool TryGetScreenRect(VisualElement element, out Rect rect)
        {
            rect = default;
            Rect bounds = element.worldBound;
            if (element.panel == null || float.IsNaN(bounds.width) || bounds.width < 1f)
                return false;

            Vector2 origin = RuntimePanelUtils.ScreenToPanel(element.panel, Vector2.zero);
            Vector2 unit = RuntimePanelUtils.ScreenToPanel(element.panel, Vector2.one * 100f) - origin;
            float perPixelX = unit.x / 100f, perPixelY = unit.y / 100f;

            float xMin = (bounds.xMin - origin.x) / perPixelX;
            float yTop = (bounds.yMin - origin.y) / perPixelY;
            float width = bounds.width / perPixelX;
            float height = bounds.height / perPixelY;
            rect = new Rect(xMin, Screen.height - yTop - height, width, height);
            return true;
        }
    }
}
