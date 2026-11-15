using System.Collections.Generic;
using FGJ2026.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace FGJ2026.UI
{
    /// <summary>
    /// A frog on a uGUI Image (menu decoration). It plays one of the board's animal animations in its colour and
    /// direction (from <see cref="BoardArt"/>) and, while singing, lets music notes rise in its colour.
    /// An Animator can't drive an Image, so a hidden SpriteRenderer runs the animation and the Image copies its sprite.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class MenuFrog : MonoBehaviour
    {
        public enum Facing { North, East, South, West }

        [SerializeField] private AnimalColor color = AnimalColor.Green;
        [SerializeField] private Facing facing = Facing.East;
        [Tooltip("Animator state: Animal_Idle, Animal_Move, Animal_Eat, Animal_Sing, Animal_Love or Animal_Sad.")]
        [SerializeField] private string state = "Animal_Sing";
        [SerializeField] private bool notes = true;
        [SerializeField, Min(0.2f)] private float noteInterval = 0.9f;
        [SerializeField] private float noteSize = 32f;
        [SerializeField] private float noteRise = 70f;
        [SerializeField, Min(0.2f)] private float noteLifetime = 1.8f;

        private sealed class Note
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Start;
            public float Age;
        }

        private readonly List<Note> activeNotes = new List<Note>();
        private Image image;
        private SpriteRenderer source;
        private Animator animator;
        private Sprite[] noteSprites;
        private float noteTimer;
        private int noteIndex;

        private void Awake()
        {
            image = GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            BoardArt art = BoardArt.Load();
            var go = new GameObject("Animation source") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            source = go.AddComponent<SpriteRenderer>();
            source.enabled = false; // only drives the Image
            animator = go.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            BoardArt.ColorVariant variant = art != null ? art.VariantFor(color) : null;
            animator.runtimeAnimatorController = variant != null ? variant.AnimalFacing(Direction()) : art != null ? art.animal : null;
            noteSprites = art != null && art.songNotes != null ? art.songNotes : new Sprite[0];
            noteTimer = Random.value * noteInterval;
        }

        private void Start()
        {
            if (animator.runtimeAnimatorController != null)
                animator.Play(state, 0, Random.value); // frogs out of step with each other
        }

        private void LateUpdate()
        {
            if (source.sprite != null && image.sprite != source.sprite)
                image.sprite = source.sprite;

            if (notes && noteSprites.Length > 0)
            {
                noteTimer -= Time.deltaTime;
                if (noteTimer <= 0f)
                {
                    noteTimer += noteInterval;
                    SpawnNote();
                }
            }
            MoveNotes(Time.deltaTime);
        }

        private Vector2Int Direction() => facing switch
        {
            Facing.North => Directions.North,
            Facing.East => Directions.East,
            Facing.South => Directions.South,
            _ => Directions.West
        };

        private void SpawnNote()
        {
            Sprite sprite = noteSprites[noteIndex++ % noteSprites.Length];
            if (sprite == null)
                return;

            var go = new GameObject("Note", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { hideFlags = HideFlags.DontSave };
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform.parent, false);
            rect.SetSiblingIndex(transform.GetSiblingIndex());
            rect.sizeDelta = Vector2.one * noteSize;
            var note = go.GetComponent<Image>();
            note.sprite = sprite;
            note.preserveAspect = true;
            note.raycastTarget = false;
            note.color = Palette.Of(color);

            Vector2Int dir = Direction();
            var self = (RectTransform)transform;
            Vector2 start = self.anchoredPosition + new Vector2(dir.x, Mathf.Max(0, dir.y)) * self.rect.width * 0.55f + Vector2.up * self.rect.height * 0.3f;
            rect.anchoredPosition = start;
            activeNotes.Add(new Note { Rect = rect, Image = note, Start = start, Age = 0f });
        }

        private void MoveNotes(float deltaTime)
        {
            Vector2Int dir = Direction();
            for (int i = activeNotes.Count - 1; i >= 0; i--)
            {
                Note note = activeNotes[i];
                note.Age += deltaTime;
                float t = note.Age / noteLifetime;
                if (t >= 1f || note.Rect == null)
                {
                    if (note.Rect != null)
                        Destroy(note.Rect.gameObject);
                    activeNotes.RemoveAt(i);
                    continue;
                }
                float drift = dir.x * 30f * t + Mathf.Sin(note.Age * 5f) * 8f;
                note.Rect.anchoredPosition = note.Start + new Vector2(drift, noteRise * t);
                Color c = note.Image.color;
                c.a = t < 0.15f ? t / 0.15f : 1f - Mathf.InverseLerp(0.6f, 1f, t);
                note.Image.color = c;
            }
        }

        private void OnDisable()
        {
            foreach (Note note in activeNotes)
                if (note.Rect != null)
                    Destroy(note.Rect.gameObject);
            activeNotes.Clear();
        }
    }
}
