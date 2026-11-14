using System;
using System.Collections.Generic;
using FGJ2026;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CandyPlacementService : MonoBehaviour, IService
{
    [SerializeField] private Camera boardCamera;
    [SerializeField] private Button redCandyButton, greenCandyButton;
    [SerializeField] private GameObject candyHolder;

    private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    private PuzzleManager puzzleManager;
    private GridService gridService;
    private EventSystem pointerEventSystem;
    private PointerEventData pointer;
    private CandyType? selectedCandy;
    private PuzzleMap selectionMap;
    private bool _isActive;

    private void Awake()
    {
        redCandyButton.onClick.AddListener(() => SelectCandy(CandyType.Red));
        greenCandyButton.onClick.AddListener(() => SelectCandy(CandyType.Green));
    }

    internal void Configure(PuzzleManager manager, GridService grid)
    {
        puzzleManager = manager;
        gridService = grid;
    }

    public void Initialize()
    {
        IsInitialized = true;
    }

    public void SelectCandy(CandyType type)
    {
        if (!_isActive)
        {
            return;
        }

        selectedCandy = type;
        selectionMap = gridService.Map;
    }

    public void ToggleUI(bool active)
    {
        greenCandyButton.interactable = puzzleManager.GetRemainingCandy(CandyType.Green) > 0;
        redCandyButton.interactable = puzzleManager.GetRemainingCandy(CandyType.Red) > 0;
        
        candyHolder.SetActive(active);
        _isActive = active;
    }

    private void Update()
    {
        if (!_isActive)
        {
            return;
        }
        
        var mouse = Mouse.current;
        if (!IsInitialized || puzzleManager.CurrentPhase != Phase.Planning || mouse == null)
            return;

        if (selectionMap != gridService.Map || (selectedCandy.HasValue && puzzleManager.GetRemainingCandy(selectedCandy.Value) == 0))
        {
            selectedCandy = null;
            return;
        }

        bool place = mouse.leftButton.wasPressedThisFrame && selectedCandy.HasValue;
        bool remove = mouse.rightButton.wasPressedThisFrame;
        if (!place && !remove)
            return;

        Vector2 position = mouse.position.ReadValue();
        if (IsOverUI(position) || !gridService.TryGetCellAtScreenPosition(boardCamera, position, out Vector2Int cell))
            return;

        if (remove)
            gridService.TryRemoveCandy(cell);
        else
            gridService.TryPlaceCandy(cell, selectedCandy.Value);
    }

    private bool IsOverUI(Vector2 position)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        if (pointerEventSystem != eventSystem)
        {
            pointerEventSystem = eventSystem;
            pointer = new PointerEventData(eventSystem);
        }

        pointer.position = position;
        pointerHits.Clear();
        eventSystem.RaycastAll(pointer, pointerHits);
        return pointerHits.Count > 0;
    }

    public void DeInitialize()
    {
        IsInitialized = false;
        selectedCandy = null;
    }

    public bool IsInitialized { get; set; }
}