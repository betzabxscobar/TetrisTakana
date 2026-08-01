using UnityEngine;
using UnityEngine.InputSystem;

namespace TetrisTakana
{
    public class GridCursor : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private Vector2Int startPosition;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.yellow;
        [SerializeField] private SpriteRenderer cursorRenderer;

        private Vector2Int currentPosition;
        private Vector2Int selectedPosition;
        private bool hasSelection;

        public Vector2Int CurrentPosition => currentPosition;
        public bool HasSelection => hasSelection;

        private void Start()
        {
            currentPosition = ClampToBoard(startPosition);
            UpdateCursorTransform();
            UpdateCursorColor();
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            Vector2Int direction = ReadMovement();

            if (direction != Vector2Int.zero)
                Move(direction);

            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame)
                SelectOrSwap();

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelSelection();
        }

        public bool Move(Vector2Int direction)
        {
            if (board == null)
                return false;

            Vector2Int destination = currentPosition + direction;

            if (!board.IsInside(destination))
                return false;

            currentPosition = destination;
            UpdateCursorTransform();
            return true;
        }

        public void SelectOrSwap()
        {
            if (board == null)
                return;

            if (!hasSelection)
            {
                if (!board.IsOccupied(currentPosition))
                    return;

                selectedPosition = currentPosition;
                hasSelection = true;
                UpdateCursorColor();
                return;
            }

            if (currentPosition == selectedPosition)
            {
                CancelSelection();
                return;
            }

            if (board.TrySwap(selectedPosition, currentPosition))
                CancelSelection();
        }

        public void CancelSelection()
        {
            hasSelection = false;
            UpdateCursorColor();
        }

        private Vector2Int ReadMovement()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                return Vector2Int.left;

            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                return Vector2Int.right;

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                return Vector2Int.up;

            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                return Vector2Int.down;

            return Vector2Int.zero;
        }

        private Vector2Int ClampToBoard(Vector2Int position)
        {
            if (board == null)
                return Vector2Int.zero;

            return new Vector2Int(
                Mathf.Clamp(position.x, 0, board.Width - 1),
                Mathf.Clamp(position.y, 0, board.Height - 1)
            );
        }

        private void UpdateCursorTransform()
        {
            if (board != null)
                transform.position = board.GridToWorld(currentPosition);
        }

        private void UpdateCursorColor()
        {
            if (cursorRenderer != null)
                cursorRenderer.color = hasSelection ? selectedColor : normalColor;
        }
    }
}
