using System;
using UnityEngine;

namespace FGJ2026
{
    [RequireComponent(typeof(GameManager))]
    public sealed class PuzzleGameLoop : MonoBehaviour
    {
        public enum Phase
        {
            Uninitialized,
            Planning,
            Running,
            AttemptFailed,
            LevelCleared,
            CampaignComplete,
            LoadFailed
        }

        public enum AttemptResult
        {
            ReachedGoal,
            Blocked,
            LeftBoard,
            StepLimit
        }

        private LevelService levels;
        private GridService grid;
        private int attemptId;

        public Phase CurrentPhase { get; private set; }
        public int CurrentLevelIndex => levels?.CurrentLevelIndex ?? -1;
        public int LevelCount => levels?.LevelCount ?? 0;
        public PuzzleMap CurrentMap => levels?.CurrentMap;
        public int AttemptCount { get; private set; }
        public AttemptResult? LastResult { get; private set; }
        public bool CanPlan => CurrentPhase == Phase.Planning || CurrentPhase == Phase.AttemptFailed;

        public event Action<PuzzleMap> LevelLoaded;
        // The movement manager passes this ID back when it reports the result.
        public event Action<int> AttemptStarted;
        public event Action<AttemptResult> AttemptFinished;
        public event Action CampaignCompleted;

        private void Awake()
        {
            var manager = GameManager.Instance;
            if (manager == null || manager.gameObject != gameObject)
                return;

            levels = manager.GetService<LevelService>();
            grid = manager.GetService<GridService>();
        }

        private void Start()
        {
            if (levels == null || !TryLoadLevel(levels.StartingLevelIndex))
                CurrentPhase = Phase.LoadFailed;
        }

        public bool TryStartAttempt()
        {
            if (!CanPlan)
                return false;

            CurrentPhase = Phase.Running;
            grid.CanPlaceItems = false;
            LastResult = null;
            AttemptCount++;
            attemptId++;
            AttemptStarted?.Invoke(attemptId);
            return true;
        }

        public bool TryFinishAttempt(int finishedAttemptId, AttemptResult result)
        {
            if (CurrentPhase != Phase.Running || finishedAttemptId != attemptId)
                return false;

            CurrentPhase = result == AttemptResult.ReachedGoal ? Phase.LevelCleared : Phase.AttemptFailed;
            grid.CanPlaceItems = CanPlan;
            LastResult = result;
            AttemptFinished?.Invoke(result);
            return true;
        }

        public bool TryAdvanceLevel()
        {
            if (CurrentPhase != Phase.LevelCleared)
                return false;

            int nextIndex = CurrentLevelIndex + 1;
            if (nextIndex < LevelCount)
                return TryLoadLevel(nextIndex);

            CurrentPhase = Phase.CampaignComplete;
            CampaignCompleted?.Invoke();
            return true;
        }

        private bool TryLoadLevel(int index)
        {
            if (!levels.TryLoadLevel(index))
                return false;

            AttemptCount = 0;
            LastResult = null;
            CurrentPhase = Phase.Planning;
            grid.CanPlaceItems = true;
            LevelLoaded?.Invoke(CurrentMap);
            return true;
        }
    }
}
