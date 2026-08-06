using System;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Una pieza de cuatro bloques. Las rotaciones se calculan dentro de una
    /// caja cuadrada (2 para la O, 4 para la I, 3 para el resto), que es como
    /// giran las piezas del Tetris clásico: la pieza no se desplaza al rotar.
    /// </summary>
    public class Tetromino : MonoBehaviour
    {
        [SerializeField] private BoardBlock[] blocks = new BoardBlock[4];
        [SerializeField] private Vector2Int[] cellOffsets = new Vector2Int[4];

        [Header("Rotación")]
        [Tooltip("Lado de la caja de giro: 2 para la O, 4 para la I, 3 para las demás.")]
        [SerializeField, Range(2, 4)] private int rotationBoxSize = 3;

        [Header("Apariencia")]
        [SerializeField, Range(0.1f, 1f)] private float cellFill = 0.94f;
        [Tooltip("Sprite de los cuatro bloques. Si se deja vacío se respeta el del prefab.")]
        [SerializeField] private Sprite blockSprite;
        [SerializeField, Min(0)] private int blockType;

        private Board board;
        private bool initialized;
        private bool locked;

        public int BlockCount => Mathf.Min(blocks.Length, cellOffsets.Length);
        public Vector2Int AnchorPosition { get; private set; }
        public int Rotation { get; private set; }
        public bool IsLocked => locked;
        public int RotationBoxSize => rotationBoxSize;
        public Sprite BlockSprite => blockSprite;
        public int BlockType => blockType;

        public event Action<Tetromino> Locked;

        /// <summary>Coloca la pieza en el tablero y la deja lista para jugarse.</summary>
        public bool Initialize(Board targetBoard, Vector2Int anchorPosition)
        {
            if (targetBoard == null || !HasValidConfiguration())
                return false;

            board = targetBoard;
            AnchorPosition = anchorPosition;
            Rotation = 0;
            locked = false;

            transform.SetParent(board.BlocksRoot, false);
            LayoutBlocks();

            if (!board.CanPlaceTetromino(this, AnchorPosition, Rotation))
                return false;

            initialized = true;
            UpdateBlockTransforms();
            return true;
        }

        /// <summary>Mueve la pieza en una direccion si el hueco esta libre.</summary>
        public bool TryMove(Vector2Int direction)
        {
            if (!CanBeControlled())
                return false;

            Vector2Int destination = AnchorPosition + direction;

            if (!board.CanPlaceTetromino(this, destination, Rotation))
                return false;

            AnchorPosition = destination;
            UpdateBlockTransforms();
            return true;
        }

        /// <summary>
        /// Comprueba si la pieza puede bajar una celda más.
        /// </summary>
        public bool CanFall()
        {
            return CanBeControlled() &&
                   board.CanPlaceTetromino(this, AnchorPosition + Vector2Int.down, Rotation);
        }

        /// <summary>Gira la pieza si cabe en la posicion nueva.</summary>
        public bool TryRotate(bool clockwise = true)
        {
            if (!CanBeControlled())
                return false;

            int nextRotation = NormalizeRotation(Rotation + (clockwise ? 1 : -1));

            // Empujones laterales: si el giro choca con la pared o con otra
            // pieza, se intenta desplazar antes de darlo por imposible.
            Vector2Int[] kickOffsets =
            {
                Vector2Int.zero,
                Vector2Int.left,
                Vector2Int.right,
                Vector2Int.left * 2,
                Vector2Int.right * 2,
                Vector2Int.up
            };

            foreach (Vector2Int kickOffset in kickOffsets)
            {
                Vector2Int candidateAnchor = AnchorPosition + kickOffset;

                if (!board.CanPlaceTetromino(this, candidateAnchor, nextRotation))
                    continue;

                AnchorPosition = candidateAnchor;
                Rotation = nextRotation;
                UpdateBlockTransforms();
                return true;
            }

            return false;
        }

        /// <summary>Fija la pieza al tablero; devuelve falso si no cabe donde esta.</summary>
        public bool TryLock()
        {
            return CanBeControlled() && board.TryLockTetromino(this);
        }

        /// <summary>Devuelve el bloque numero index de la pieza.</summary>
        public BoardBlock GetBlock(int index)
        {
            return blocks[index];
        }

        /// <summary>Celda que ocuparia ese bloque con el ancla y la rotacion indicadas.</summary>
        public Vector2Int GetCellPosition(int index, Vector2Int anchorPosition, int rotation)
        {
            return anchorPosition + RotateOffset(cellOffsets[index], rotation, rotationBoxSize);
        }

        /// <summary>Posicion del bloque dentro de la pieza, sin rotar.</summary>
        public Vector2Int GetCellOffset(int index)
        {
            return cellOffsets[index];
        }

        /// <summary>Offsets ya rotados; lo usa la vista de la siguiente pieza.</summary>
        public Vector2Int GetRotatedOffset(int index, int rotation)
        {
            return RotateOffset(cellOffsets[index], rotation, rotationBoxSize);
        }

        /// <summary>Da la pieza por fijada: a partir de aqui ya no acepta ordenes.</summary>
        public void CompleteLock()
        {
            locked = true;
            initialized = false;
            Locked?.Invoke(this);
            Destroy(gameObject);
        }

        /// <summary>Solo se maneja una pieza inicializada, sin fijar y con tablero.</summary>
        private bool CanBeControlled()
        {
            return initialized && !locked && board != null;
        }

        /// <summary>Comprueba que la pieza trae sus bloques y sus offsets bien puestos.</summary>
        private bool HasValidConfiguration()
        {
            if (blocks == null ||
                cellOffsets == null ||
                blocks.Length != 4 ||
                cellOffsets.Length != 4)
            {
                Debug.LogError(
                    $"{name} debe tener exactamente cuatro bloques y cuatro posiciones.",
                    this);
                return false;
            }

            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] == null)
                {
                    Debug.LogError($"{name} tiene un bloque sin asignar.", this);
                    return false;
                }
            }

            return true;
        }

        /// <summary>Reparte los bloques por sus celdas dentro de la pieza.</summary>
        private void LayoutBlocks()
        {
            for (int i = 0; i < BlockCount; i++)
            {
                BoardBlock block = blocks[i];
                block.SetTetromino(this);
                block.SetBlockType(blockType);

                SpriteRenderer renderer = block.GetComponent<SpriteRenderer>();

                if (renderer == null)
                    continue;

                if (blockSprite != null)
                    renderer.sprite = blockSprite;

                if (renderer.sprite == null)
                    continue;

                // Escalar al tamaño de celda hace que la pieza encaje sea cual
                // sea la resolución del sprite o el cellSize del tablero.
                Vector2 spriteSize = renderer.sprite.bounds.size;

                if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                    continue;

                float targetSize = board.CellSize * cellFill;
                block.transform.localScale = new Vector3(
                    targetSize / spriteSize.x,
                    targetSize / spriteSize.y,
                    1f);
            }
        }

        /// <summary>Lleva cada bloque al punto del mundo que le toca.</summary>
        private void UpdateBlockTransforms()
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            for (int i = 0; i < BlockCount; i++)
            {
                Vector2Int position = GetCellPosition(i, AnchorPosition, Rotation);
                blocks[i].transform.position = board.GridToWorld(position);
            }
        }

        /// <summary>
        /// Gira 90° en sentido horario tantas veces como indique la rotación,
        /// dentro de la caja: (x, y) -> (y, lado - 1 - x).
        /// </summary>
        private static Vector2Int RotateOffset(Vector2Int offset, int rotation, int boxSize)
        {
            int steps = NormalizeRotation(rotation);
            int limit = Mathf.Max(1, boxSize) - 1;

            for (int i = 0; i < steps; i++)
                offset = new Vector2Int(offset.y, limit - offset.x);

            return offset;
        }

        /// <summary>Deja la rotacion siempre entre 0 y 3, gire hacia donde gire.</summary>
        private static int NormalizeRotation(int rotation)
        {
            return (rotation % 4 + 4) % 4;
        }
    }
}
