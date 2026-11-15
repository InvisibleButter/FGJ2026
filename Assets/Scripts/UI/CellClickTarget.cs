using UnityEngine;
using UnityEngine.EventSystems;

namespace FGJ2026.UI
{
    /// <summary>Forwards clicks on a board square (left or right button) to its <see cref="BoardView"/>.</summary>
    public sealed class CellClickTarget : MonoBehaviour, IPointerClickHandler
    {
        internal BoardView Board;
        internal Vector2Int Cell;

        public void OnPointerClick(PointerEventData eventData) => Board?.RaiseCellClicked(Cell, eventData.button);
    }
}
