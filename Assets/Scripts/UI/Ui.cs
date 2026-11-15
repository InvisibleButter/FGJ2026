using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FGJ2026.UI
{
    /// <summary>
    /// Small helpers to build placeholder uGUI from code. Everything here is meant to be replaced
    /// by real art later; the screens only depend on these helpers, not on scene setup.
    /// </summary>
    public static class Ui
    {
        public const int UiLayer = 5;
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        private static Sprite square, circle, star, triangle;

        public static Sprite Square => square ? square : square = MakeSprite(4, _ => true, 1);
        public static Sprite Circle => circle ? circle : circle = MakeSprite(64, p => p.magnitude <= 0.47f);
        public static Sprite Star => star ? star : star = MakeSprite(64, p => InPolygon(p, StarPoints()));
        /// <summary>Points to the right (east).</summary>
        public static Sprite Triangle => triangle ? triangle : triangle = MakeSprite(64, p => InPolygon(p, new[]
        {
            new Vector2(-0.42f, -0.45f), new Vector2(0.46f, 0f), new Vector2(-0.42f, 0.45f)
        }));

        // ------------------------------------------------------------ Scene objects

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = UiLayer;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // ------------------------------------------------------------ Elements

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image CreateImage(Transform parent, string name, Color color, Sprite sprite = null)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : Square;
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = CreateRect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.richText = true;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        public static Button CreateButton(Transform parent, string text, Action onClick, Color color, float fontSize = 30)
        {
            Image background = CreateImage(parent, text + " Button", color);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            TextMeshProUGUI label = CreateText(background.transform, text, fontSize, Palette.Text);
            label.rectTransform.Stretch(10, 4, 10, 4);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        public static void SetText(this Button button, string text) =>
            button.GetComponentInChildren<TextMeshProUGUI>().text = text;

        // ------------------------------------------------------------ Layout

        public static RectTransform Stretch(this RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>Anchors the rect to one point of its parent (e.g. (0,1) = top left) and sets pivot, position and size.</summary>
        public static RectTransform Pin(this RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static LayoutElement Size(this Component component, float width = -1, float height = -1,
            float flexibleWidth = -1, float flexibleHeight = -1)
        {
            if (!component.TryGetComponent(out LayoutElement element))
                element = component.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = flexibleHeight;
            return element;
        }

        public static VerticalLayoutGroup Vertical(this Component component, float spacing, int padding = 0,
            TextAnchor alignment = TextAnchor.UpperLeft, bool fitHeight = false)
        {
            var layout = component.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            if (fitHeight)
                component.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return layout;
        }

        public static HorizontalLayoutGroup Horizontal(this Component component, float spacing, int padding = 0,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var layout = component.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static void DestroyChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        // ------------------------------------------------------------ Procedural sprites

        private static Sprite MakeSprite(int size, Func<Vector2, bool> inside, int samples = 4)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < samples; sy++)
                    for (int sx = 0; sx < samples; sx++)
                    {
                        var point = new Vector2((x + (sx + 0.5f) / samples) / size - 0.5f, (y + (sy + 0.5f) / samples) / size - 0.5f);
                        if (inside(point))
                            hits++;
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (samples * samples)));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Vector2[] starPoints;

        private static Vector2[] StarPoints()
        {
            if (starPoints != null)
                return starPoints;
            starPoints = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? 0.48f : 0.2f;
                starPoints[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius + new Vector2(0f, -0.02f);
            }
            return starPoints;
        }

        private static bool InPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                    point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
