using System;
using UnityEngine;

namespace FGJ2026
{
    public sealed class PuzzleGameLoop : MonoBehaviour, IService
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
        
        private int attemptId;

        public Phase CurrentPhase { get; private set; }
      
        public int AttemptCount { get; private set; }
        public AttemptResult? LastResult { get; private set; }
        public bool CanPlan => CurrentPhase == Phase.Planning || CurrentPhase == Phase.AttemptFailed;

        private GridService _gridService;
        private LevelService _levels;
        
        public bool TryStartAttempt()
        {
            if (!CanPlan)
                return false;

            CurrentPhase = Phase.Running;
            _gridService.CanPlaceItems = false;
            LastResult = null;
            AttemptCount++;
            attemptId++;
            return true;
        }

        public bool TryFinishAttempt(int finishedAttemptId, AttemptResult result)
        {
            if (CurrentPhase != Phase.Running || finishedAttemptId != attemptId)
                return false;

            CurrentPhase = result == AttemptResult.ReachedGoal ? Phase.LevelCleared : Phase.AttemptFailed;
            _gridService.CanPlaceItems = CanPlan;
            LastResult = result;
            return true;
        }

        public bool TryAdvanceLevel()
        {
            if (CurrentPhase != Phase.LevelCleared)
                return false;

            GameManager.Instance.SelectStartingLevel(GameManager.Instance.CurrentLevelIndex + 1);
            CurrentPhase = Phase.CampaignComplete;
            return true;
        }

        private bool TryLoadLevel(int index)
        {
            if (!_levels.TryLoadLevel(index))
                return false;

            AttemptCount = 0;
            LastResult = null;
            CurrentPhase = Phase.Planning;
            _gridService.CanPlaceItems = true;
            return true;
        }

        public void Initialize()
        {
            _levels = ServiceLocator.Instance.GetService<LevelService>();
            _gridService = ServiceLocator.Instance.GetService<GridService>();
            if (_levels == null || !TryLoadLevel(GameManager.Instance.CurrentLevelIndex))
            {
                CurrentPhase = Phase.LoadFailed;
            }

            IsInitialized = true;
        }

        public void DeInitialize()
        {
        }

        public bool IsInitialized { get; set; }
    }
}
