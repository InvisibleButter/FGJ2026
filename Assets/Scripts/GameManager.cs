using System;
using System.Collections.Generic;
using UnityEngine;

namespace FGJ2026
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private PuzzleGridView gridView;
        [SerializeField] private TextAsset[] levels = Array.Empty<TextAsset>();

        private static int? pendingLevelIndex;
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        public static GameManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            pendingLevelIndex = null;
        }

        internal static void SelectStartingLevel(int index)
        {
            pendingLevelIndex = index;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("MainGame needs a single GameManager.", this);
                return;
            }

            int startingLevelIndex = pendingLevelIndex ?? 0;
            pendingLevelIndex = null;
            if (gridView == null)
            {
                Debug.LogError("Assign a PuzzleGridView to the GameManager.", this);
                return;
            }

            var grid = new GridService(gridView);
            RegisterService(grid);
            RegisterService(new LevelService(levels, startingLevelIndex, grid));
            Instance = this;
        }

        public void RegisterService<T>(T service) where T : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            if (!services.TryAdd(typeof(T), service))
                throw new InvalidOperationException($"{typeof(T).Name} is already registered.");
        }

        public T GetService<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out object service))
                return (T)service;

            throw new InvalidOperationException($"{typeof(T).Name} is not registered. Resolve services after GameManager.Awake.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
