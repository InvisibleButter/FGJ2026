using UnityEngine;
using UnityEngine.UIElements;

namespace FGJ2026.UI
{
    /// <summary>
    /// The tear-off calendar in the puzzle screen (Kalender_0..5 from Animations.png). Shows the day number on the page;
    /// when a new day starts, the page is ripped off (12 fps, like the Kalendar_rip clip) and the next number appears.
    /// The frames have different sizes; they are lined up on their pivots, as the Animator would.
    /// </summary>
    public sealed class CalendarView
    {
        private const int Scale = 6;                  // screen units per art pixel
        private const float AnchorX = 31.5f, AnchorY = 7.5f; // pivot position in the 48x32 frame space (from top-left)
        private const int CropX = 12, CropY = 6;      // the element only covers the used part of that space (28x22)
        private const long FrameMilliseconds = 83;    // 12 fps
        private const int NumberFromFrame = 3;        // while the page lifts, no number; then the new one shows underneath

        private readonly VisualElement frame;
        private readonly Label dayLabel;
        private readonly Sprite[] frames;
        private IVisualElementScheduledItem animation;
        private int shownDay = -1;

        public CalendarView(VisualElement root, Sprite[] frames)
        {
            frame = root.Q("calendarFrame");
            dayLabel = root.Q<Label>("calendarDay");
            this.frames = frames != null && frames.Length > 0 ? frames : null;
            SetFrame(0);
        }

        /// <summary>Shows a day. Moving on to a later day plays the rip animation; going back (rewind, restart) just switches.</summary>
        public void Show(int day)
        {
            if (day == shownDay)
                return;

            bool rip = shownDay > 0 && day > shownDay && frames != null && frames.Length > 1;
            shownDay = day;
            animation?.Pause();

            if (!rip)
            {
                SetFrame(0);
                dayLabel.text = day.ToString();
                dayLabel.visible = true;
                return;
            }

            int index = 0;
            animation = frame.schedule.Execute(() =>
            {
                index++;
                if (index >= frames.Length)
                {
                    SetFrame(0);
                    dayLabel.text = day.ToString();
                    dayLabel.visible = true;
                    animation.Pause();
                    return;
                }
                SetFrame(index);
                dayLabel.visible = index >= NumberFromFrame;
                dayLabel.text = day.ToString();
            }).Every(FrameMilliseconds);
        }

        private void SetFrame(int index)
        {
            if (frames == null || frames[index] == null)
                return;
            Sprite sprite = frames[index];
            Rect rect = sprite.rect;
            frame.style.backgroundImage = new StyleBackground(sprite);
            frame.style.width = rect.width * Scale;
            frame.style.height = rect.height * Scale;
            frame.style.left = (Mathf.Round(AnchorX - sprite.pivot.x) - CropX) * Scale;
            frame.style.top = (Mathf.Round(AnchorY - (rect.height - sprite.pivot.y)) - CropY) * Scale;
        }
    }
}
