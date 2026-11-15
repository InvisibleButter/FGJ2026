using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace FGJ2026.UI
{
    /// <summary>
    /// Keeps the UI pixel perfect at any resolution: one art pixel always becomes a whole number of screen pixels.
    /// UI Toolkit screens draw an art pixel as 3 (or 6) units at the 1920x1080 reference; the uGUI start menu as 4 units
    /// at 1280x720. Instead of scaling freely with the screen, the scale is rounded down to the nearest whole number of
    /// screen pixels per art pixel. Windows too small for that (less than 2 screen pixels per art pixel) keep the
    /// normal free scaling, so nothing gets cut off.
    /// Created automatically; nothing to set up.
    /// </summary>
    public sealed class PixelPerfectUI : MonoBehaviour
    {
        private const float ToolkitUnitsPerArtPixel = 3f;
        private static readonly Vector2 ToolkitReference = new Vector2(1920f, 1080f);
        private const float CanvasUnitsPerArtPixel = 4f;

        private static PixelPerfectUI instance;

        private readonly Dictionary<PanelSettings, (PanelScaleMode mode, float scale)> originals =
            new Dictionary<PanelSettings, (PanelScaleMode, float)>();
        private Vector2Int lastScreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null)
                return;
            var go = new GameObject("Pixel perfect UI") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PixelPerfectUI>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        private void Update()
        {
            if (Screen.width != lastScreen.x || Screen.height != lastScreen.y)
                Apply();
        }

        private void Apply()
        {
            lastScreen = new Vector2Int(Screen.width, Screen.height);

            // UI Toolkit: 3 units per art pixel at the reference size -> m screen pixels per art pixel.
            int toolkitPixels = PixelsPerArtPixel(ToolkitReference, ToolkitUnitsPerArtPixel);
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                PanelSettings settings = document.panelSettings;
                if (settings == null)
                    continue;
                if (!originals.ContainsKey(settings))
                    originals[settings] = (settings.scaleMode, settings.scale);

                if (toolkitPixels >= 2)
                {
                    settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                    settings.scale = toolkitPixels / ToolkitUnitsPerArtPixel;
                }
                else
                {
                    settings.scaleMode = originals[settings].mode;
                    settings.scale = originals[settings].scale;
                }
            }

            // uGUI (start menu): 4 units per art pixel at its reference resolution.
            foreach (CanvasScaler scaler in FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None))
            {
                int canvasPixels = PixelsPerArtPixel(scaler.referenceResolution, CanvasUnitsPerArtPixel);
                if (canvasPixels < 2)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    continue;
                }
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = canvasPixels / CanvasUnitsPerArtPixel;
            }
        }

        /// <summary>Whole screen pixels per art pixel when the reference size fits the screen.</summary>
        private static int PixelsPerArtPixel(Vector2 reference, float unitsPerArtPixel) => Mathf.FloorToInt(Mathf.Min(
            Screen.width / (reference.x / unitsPerArtPixel),
            Screen.height / (reference.y / unitsPerArtPixel)));

        private void OnDestroy()
        {
            // PanelSettings is an asset: put it back as it was (matters in the Editor).
            foreach (var pair in originals)
            {
                if (pair.Key == null)
                    continue;
                pair.Key.scaleMode = pair.Value.mode;
                pair.Key.scale = pair.Value.scale;
            }
            if (instance == this)
                instance = null;
        }
    }
}
