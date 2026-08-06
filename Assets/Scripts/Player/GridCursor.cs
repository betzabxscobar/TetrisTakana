using UnityEngine;
using UnityEngine.InputSystem;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// El cursor del jugador: dos selectores que enmarcan una pareja de celdas
    /// vecinas y las intercambian de un solo golpe de tecla.
    /// </summary>
    public class GridCursor : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private Vector2Int startPosition;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private SpriteRenderer cursorRenderer;
        [Tooltip("El segundo selector. Vacio: se fabrica al arrancar copiando al primero.")]
        [SerializeField] private SpriteRenderer partnerRenderer;
        [SerializeField] private SwapSystem swapSystem;
        [SerializeField] private MatchSystem matchSystem;
        [Tooltip("Si se asigna, el cursor se queda quieto en pausa y al perder.")]
        [SerializeField] private BoardGame game;

        private Vector2Int currentPosition;
        private Vector2Int pairDirection = Vector2Int.right;
        private Vector2Int lastBoardSize;
        private Vector2Int repeatDirection;
        private float nextRepeatTime;

        public Vector2Int CurrentPosition => currentPosition;

        /// <summary>La otra celda de la pareja, la que enmarca el segundo selector.</summary>
        public Vector2Int PartnerPosition => currentPosition + pairDirection;

        public Board Board => board;

        [Tooltip("Tamaño de cada selector en celdas; algo mas de 1 para enmarcar la ficha.")]
        [SerializeField, Min(0.1f)] private float sizeInCells = 1.15f;

        [Tooltip("La tecla R pone la pareja en vertical y la devuelve a horizontal.")]
        [SerializeField] private bool allowRotatePair = true;

        [Header("Repeticion de teclado")]
        [Tooltip("Espera antes de que una tecla mantenida empiece a repetir.")]
        [SerializeField, Min(0f)] private float initialRepeatDelay = 0.27f;
        [Tooltip("Cada cuanto avanza una celda mientras la tecla siga pulsada.")]
        [SerializeField, Min(0.01f)] private float repeatInterval = 0.1f;

        /// <summary>Coloca la pareja en su celda de partida y la escala a la rejilla.</summary>
        private void Start()
        {
            EnsurePartnerRenderer();
            currentPosition = ClampToBoard(startPosition);

            if (board != null)
                lastBoardSize = new Vector2Int(board.Width, board.Height);

            FitToCell();
            UpdateCursorTransform();
            UpdateCursorColor();
        }

        /// <summary>
        /// Fabrica el segundo selector si no viene puesto desde la escena. Se
        /// crea a mano y no clonando este GameObject porque el clon arrastraria
        /// tambien este componente, y dos GridCursor leyendo el mismo teclado se
        /// pisarian el uno al otro.
        /// </summary>
        private void EnsurePartnerRenderer()
        {
            if (partnerRenderer != null || cursorRenderer == null)
                return;

            GameObject instance = new GameObject("GridCursorPartner");

            // Colgado del cursor: asi se apaga y se destruye con el, y su
            // escala local de 1 hereda la que FitToCell le da al padre.
            instance.transform.SetParent(transform, false);

            partnerRenderer = instance.AddComponent<SpriteRenderer>();
            partnerRenderer.sprite = cursorRenderer.sprite;
            partnerRenderer.sharedMaterial = cursorRenderer.sharedMaterial;
            partnerRenderer.sortingLayerID = cursorRenderer.sortingLayerID;
            partnerRenderer.sortingOrder = cursorRenderer.sortingOrder;
            partnerRenderer.color = cursorRenderer.color;
        }

        /// <summary>
        /// Escala los selectores a la celda. Sin esto dependen de los pixeles
        /// por unidad del sprite y acaban siendo un trazo de dos o tres pixeles.
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

            // El segundo selector solo hereda la escala si cuelga del cursor;
            // si viene puesto desde la escena como objeto suelto, se le da aqui.
            if (partnerRenderer != null && partnerRenderer.transform.parent != transform)
                partnerRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Lee el teclado: mover, girar la pareja e intercambiar.</summary>
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

            if (allowRotatePair && Keyboard.current.rKey.wasPressedThisFrame)
                RotatePair();

            // El intercambio vive en F, pegada al WASD: con la barra o el Enter
            // habia que soltar la mano del bloque de movimiento en cada jugada.
            // La barra sigue valiendo como alias.
            if (Keyboard.current.fKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame)
                Swap();
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
                FitToCell();
            }

            UpdateCursorTransform();
        }

        /// <summary>Mueve la pareja una celda si ninguno de los dos se sale del tablero.</summary>
        public bool Move(Vector2Int direction)
        {
            if (board == null)
                return false;

            Vector2Int destination = currentPosition + direction;

            if (!board.IsInside(destination) ||
                !board.IsInside(destination + pairDirection))
                return false;

            currentPosition = destination;
            UpdateCursorTransform();
            return true;
        }

        /// <summary>
        /// Pone la pareja en vertical y la devuelve a horizontal. Horizontal es
        /// lo normal, pero sin esto se perderian los intercambios en columna,
        /// que antes se hacian eligiendo las dos celdas a mano.
        /// </summary>
        public void RotatePair()
        {
            Vector2Int rotated = pairDirection == Vector2Int.right
                ? Vector2Int.up
                : Vector2Int.right;

            if (board == null)
            {
                pairDirection = rotated;
                return;
            }

            // Girar contra el borde sacaria al segundo selector del tablero;
            // se arrastra la pareja hacia dentro en vez de rechazar el giro.
            pairDirection = rotated;
            currentPosition = ClampToBoard(currentPosition);
            UpdateCursorTransform();
        }

        /// <summary>Intercambia las dos fichas enmarcadas, sin pasos intermedios.</summary>
        public void Swap()
        {
            if (board == null ||
                (swapSystem != null && !swapSystem.CanSwap) ||
                (matchSystem != null && matchSystem.IsResolving))
                return;

            Vector2Int partner = PartnerPosition;

            if (!board.IsInside(currentPosition) || !board.IsInside(partner))
                return;

            if (swapSystem != null)
                swapSystem.TrySwap(currentPosition, partner);
            else
                board.TrySwap(currentPosition, partner);
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

        /// <summary>
        /// Mete la pareja dentro de los limites: el tope lo marca el segundo
        /// selector, que va una celda por delante en la direccion de la pareja.
        /// </summary>
        private Vector2Int ClampToBoard(Vector2Int position)
        {
            if (board == null)
                return Vector2Int.zero;

            int maxX = Mathf.Max(0, board.Width - 1 - Mathf.Max(0, pairDirection.x));
            int maxY = Mathf.Max(0, board.Height - 1 - Mathf.Max(0, pairDirection.y));

            return new Vector2Int(
                Mathf.Clamp(position.x, 0, maxX),
                Mathf.Clamp(position.y, 0, maxY)
            );
        }

        /// <summary>Lleva cada selector al punto del mundo de su celda.</summary>
        private void UpdateCursorTransform()
        {
            if (board == null)
                return;

            transform.position = board.GridToWorld(currentPosition);

            // En coordenadas de mundo y no con un desplazamiento local: mientras
            // el reloj de arena gira, el tablero esta rotado y solo GridToWorld
            // sabe donde cae de verdad la celda de al lado.
            if (partnerRenderer != null)
                partnerRenderer.transform.position = board.GridToWorld(PartnerPosition);
        }

        /// <summary>Tiñe los dos selectores por igual.</summary>
        private void UpdateCursorColor()
        {
            if (cursorRenderer != null)
                cursorRenderer.color = normalColor;

            if (partnerRenderer != null)
                partnerRenderer.color = normalColor;
        }
    }
}
