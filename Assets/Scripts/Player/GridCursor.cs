using UnityEngine;
using UnityEngine.InputSystem;

namespace TetrisTakana.Match3
{
    /// <summary>El cursor del jugador: se mueve por la rejilla y manda intercambiar fichas vecinas.</summary>
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
        private Vector2Int lastBoardSize;
        private Vector2Int repeatDirection;
        private float nextRepeatTime;
        private bool hasSelection;

        public Vector2Int CurrentPosition => currentPosition;
        public bool HasSelection => hasSelection;
        public Board Board => board;

        [Tooltip("Tamaño del cursor en celdas; algo mas de 1 para enmarcar la ficha.")]
        [SerializeField, Min(0.1f)] private float sizeInCells = 1.15f;

        [Header("Repeticion de teclado")]
        [Tooltip("Espera antes de que una tecla mantenida empiece a repetir.")]
        [SerializeField, Min(0f)] private float initialRepeatDelay = 0.27f;
        [Tooltip("Cada cuanto avanza una celda mientras la tecla siga pulsada.")]
        [SerializeField, Min(0.01f)] private float repeatInterval = 0.1f;

        /// <summary>Coloca el cursor en su celda de partida y lo escala a la celda.</summary>
        private void Start()
        {
            currentPosition = ClampToBoard(startPosition);

            if (board != null)
                lastBoardSize = new Vector2Int(board.Width, board.Height);

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

        /// <summary>Lee el teclado: mover, seleccionar e intercambiar.</summary>
        private void Update()
        {
            SyncWithBoard();

            if (Keyboard.current == null)
                return;

            // Con la tarjeta de pausa delante o la partida terminada, el
            // cursor no debe moverse ni intercambiar nada.
            if (game != null && !game.AcceptsInput)
                return;

            Vector2Int direction = ReadMovement();

            if (direction != Vector2Int.zero)
                Move(direction);

            // Seleccionar e intercambiar viven en F, pegada al WASD: con la
            // barra o el Enter habia que soltar la mano del bloque de
            // movimiento en cada jugada. La barra sigue valiendo como alias.
            if (Keyboard.current.fKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame)
                SelectOrSwap();

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelSelection();
        }

        /// <summary>
        /// El giro del reloj intercambia el alto y el ancho del tablero y lo
        /// recoloca en el mundo. Sin esto el cursor se queda anclado a una
        /// celda que ya no existe: todos los movimientos caen fuera de la
        /// rejilla y se rechazan, dejandolo bloqueado el resto de la partida.
        /// </summary>
        private void SyncWithBoard()
        {
            if (board == null)
                return;

            Vector2Int size = new Vector2Int(board.Width, board.Height);

            if (size != lastBoardSize)
            {
                lastBoardSize = size;
                currentPosition = ClampToBoard(currentPosition);
                CancelSelection();
                FitToCell();
            }

            UpdateCursorTransform();
        }

        /// <summary>Mueve el cursor una celda si no se sale del tablero.</summary>
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

        /// <summary>Coge la ficha de debajo, o la intercambia con la que ya estaba cogida.</summary>
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

        /// <summary>Suelta la ficha que tuviera cogida.</summary>
        public void CancelSelection()
        {
            hasSelection = false;
            UpdateCursorColor();
        }

        /// <summary>
        /// La direccion que toca este fotograma. Mantener la tecla arrastra el
        /// cursor: cruzar el tablero a toques sueltos eran veinte pulsaciones,
        /// y con el reloj a punto de girar no da tiempo.
        /// </summary>
        private Vector2Int ReadMovement()
        {
            Vector2Int held = ReadHeldDirection();

            if (held == Vector2Int.zero)
            {
                repeatDirection = Vector2Int.zero;
                return Vector2Int.zero;
            }

            // Al cambiar de direccion se mueve en el acto y vuelve a esperar,
            // asi un toque corto sigue siendo una sola celda.
            if (held != repeatDirection)
            {
                repeatDirection = held;
                nextRepeatTime = Time.unscaledTime + initialRepeatDelay;
                return held;
            }

            if (Time.unscaledTime < nextRepeatTime)
                return Vector2Int.zero;

            nextRepeatTime = Time.unscaledTime + repeatInterval;
            return held;
        }

        /// <summary>
        /// Lo pulsado ahora mismo manda sobre lo que ya venia mantenido: si no,
        /// un toque rapido en otra direccion se lo comeria la tecla anterior.
        /// </summary>
        private Vector2Int ReadHeldDirection()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                return Vector2Int.left;

            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                return Vector2Int.right;

            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
                return Vector2Int.up;

            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
                return Vector2Int.down;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                return Vector2Int.left;

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                return Vector2Int.right;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                return Vector2Int.up;

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                return Vector2Int.down;

            return Vector2Int.zero;
        }

        /// <summary>Mete una celda dentro de los limites del tablero.</summary>
        private Vector2Int ClampToBoard(Vector2Int position)
        {
            if (board == null)
                return Vector2Int.zero;

            return new Vector2Int(
                Mathf.Clamp(position.x, 0, board.Width - 1),
                Mathf.Clamp(position.y, 0, board.Height - 1)
            );
        }

        /// <summary>Lleva el cursor al punto del mundo de su celda.</summary>
        private void UpdateCursorTransform()
        {
            if (board != null)
                transform.position = board.GridToWorld(currentPosition);
        }

        /// <summary>Tiñe el cursor segun tenga o no una ficha cogida.</summary>
        private void UpdateCursorColor()
        {
            if (cursorRenderer != null)
                cursorRenderer.color = hasSelection ? selectedColor : normalColor;
        }
    }
}
