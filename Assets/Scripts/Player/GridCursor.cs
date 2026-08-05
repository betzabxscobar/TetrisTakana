using UnityEngine;
using UnityEngine.InputSystem;

namespace TetrisTakana.Match3
{
    public class GridCursor : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private Vector2Int startPosition;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.yellow;
        [SerializeField] private SpriteRenderer cursorRenderer;
        [SerializeField] private SwapSystem swapSystem;
        [SerializeField] private MatchSystem matchSystem;
        [Tooltip("Si se asigna, el cursor se queda quieto en pausa y al perder.")]
        [SerializeField] private BoardGame game;

        private Vector2Int currentPosition;
        private Vector2Int selectedPosition;
        private bool hasSelection;

        public Vector2Int CurrentPosition => currentPosition;
        public bool HasSelection => hasSelection;
        public Board Board => board;

        [Tooltip("Tamaño del cursor en celdas; algo mas de 1 para enmarcar la ficha.")]
        [SerializeField, Min(0.1f)] private float sizeInCells = 1.15f;

        private void Start()
        {
            currentPosition = ClampToBoard(startPosition);
            FitToCell();
            UpdateCursorTransform();
            UpdateCursorColor();
        }

        /// <summary>
        /// Escala el cursor a la celda. Sin esto depende de los pixeles por
        /// unidad del sprite y acaba siendo un trazo de dos o tres pixeles.
        /// </summary>
        private void FitToCell()
        {
            if (board == null || cursorRenderer == null || cursorRenderer.sprite == null)
                return;

            Vector2 size = cursorRenderer.sprite.bounds.size;

            if (size.x <= 0f || size.y <= 0f)
                return;

            float scale = board.CellSize * sizeInCells / Mathf.Max(size.x, size.y);
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            // Con la tarjeta de pausa delante o la partida terminada, el
            // cursor no debe moverse ni intercambiar nada.
            if (game != null && !game.AcceptsInput)
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
            if (board == null ||
                (swapSystem != null && !swapSystem.CanSwap) ||
                (matchSystem != null && matchSystem.IsResolving))
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

            bool swapped = swapSystem != null
                ? swapSystem.TrySwap(selectedPosition, currentPosition)
                : board.TrySwap(selectedPosition, currentPosition);

            if (swapped)
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
