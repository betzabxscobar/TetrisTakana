using System;
using System.Collections.Generic;
using UnityEngine;

namespace TetrisTakana
{
    public class Board : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField, Min(1)] private int width = 10;
        [SerializeField, Min(1)] private int height = 20;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;

        [Header("Blocks")]
        [SerializeField] private Transform blocksRoot;

        [Header("Intercambio (modo match-3)")]
        [Tooltip("Permite intercambiar una ficha con una celda vacía.")]
        [SerializeField] private bool allowSwapWithEmptyCell;

        private BoardBlock[,] cells;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Transform BlocksRoot => blocksRoot != null ? blocksRoot : transform;

        public event Action BoardChanged;

        /// <summary>Dos fichas acaban de intercambiarse. Lo usa el modo match-3.</summary>
        public event Action<Vector2Int, Vector2Int> BlocksSwapped;

        private void Awake()
        {
            cells = new BoardBlock[width, height];
            RebuildFromChildren();
        }

        /// <summary>
        /// Cambia el tamaño de la rejilla y vacía la matriz. Lo usa el giro de
        /// 90 grados, donde un tablero de 10x20 pasa a ser de 20x10: quien
        /// llama debe haberse guardado los bloques antes, porque aquí se
        /// pierden las referencias (no se destruye ningún GameObject).
        /// </summary>
        public void SetDimensions(int newWidth, int newHeight)
        {
            width = Mathf.Max(1, newWidth);
            height = Mathf.Max(1, newHeight);
            cells = new BoardBlock[width, height];
            BoardChanged?.Invoke();
        }

        public bool IsInside(Vector2Int position)
        {
            return position.x >= 0 && position.x < width &&
                   position.y >= 0 && position.y < height;
        }

        public bool IsOccupied(Vector2Int position)
        {
            EnsureInitialized();
            return IsInside(position) && cells[position.x, position.y] != null;
        }

        public bool TryGetBlock(Vector2Int position, out BoardBlock block)
        {
            EnsureInitialized();
            block = IsInside(position) ? cells[position.x, position.y] : null;
            return block != null;
        }

        public BoardBlock GetBlock(Vector2Int position)
        {
            EnsureInitialized();
            return IsInside(position) ? cells[position.x, position.y] : null;
        }

        public IEnumerable<BoardBlock> GetAllBlocks()
        {
            EnsureInitialized();

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (cells[x, y] != null)
                    yield return cells[x, y];
        }

        public Vector3 GridToWorld(Vector2Int position)
        {
            Vector3 localPosition = new Vector3(
                (position.x + 0.5f) * cellSize,
                (position.y + 0.5f) * cellSize,
                0f
            );

            return transform.TransformPoint(localPosition);
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);

            return new Vector2Int(
                Mathf.FloorToInt(localPosition.x / cellSize),
                Mathf.FloorToInt(localPosition.y / cellSize)
            );
        }

        /// <summary>Dos celdas se tocan en cruz, sin contar diagonales.</summary>
        public bool AreAdjacent(Vector2Int first, Vector2Int second)
        {
            Vector2Int distance = first - second;
            return Mathf.Abs(distance.x) + Mathf.Abs(distance.y) == 1;
        }

        /// <summary>
        /// Intercambia dos fichas contiguas. Es la jugada del modo match-3; el
        /// Tetris no la usa, pero vive aquí porque toca la matriz de celdas.
        /// </summary>
        public bool TrySwap(Vector2Int first, Vector2Int second)
        {
            EnsureInitialized();

            if (!IsInside(first) || !IsInside(second) || !AreAdjacent(first, second))
                return false;

            BoardBlock firstBlock = cells[first.x, first.y];
            BoardBlock secondBlock = cells[second.x, second.y];

            if (firstBlock == null && secondBlock == null)
                return false;

            if (!allowSwapWithEmptyCell && (firstBlock == null || secondBlock == null))
                return false;

            cells[first.x, first.y] = secondBlock;
            cells[second.x, second.y] = firstBlock;

            MoveBlockToCell(secondBlock, first);
            MoveBlockToCell(firstBlock, second);

            BlocksSwapped?.Invoke(first, second);
            BoardChanged?.Invoke();
            return true;
        }

        public bool TryRegister(BoardBlock block, Vector2Int position)
        {
            EnsureInitialized();

            if (block == null || !IsInside(position) || IsOccupied(position))
                return false;

            cells[position.x, position.y] = block;
            MoveBlockToCell(block, position);
            BoardChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Coloca un bloque en una celda. Con <paramref name="snapTransform"/>
        /// en falso solo se actualiza la matriz, para que quien llame pueda
        /// animar el desplazamiento por su cuenta.
        /// </summary>
        public bool SetBlock(Vector2Int position, BoardBlock block, bool snapTransform = true)
        {
            EnsureInitialized();

            if (!IsInside(position))
                return false;

            cells[position.x, position.y] = block;

            if (snapTransform)
                MoveBlockToCell(block, position);
            else if (block != null)
                block.SetGridPosition(position);

            BoardChanged?.Invoke();
            return true;
        }

        public BoardBlock RemoveBlock(Vector2Int position, bool destroyObject = true)
        {
            EnsureInitialized();

            if (!IsInside(position))
                return null;

            BoardBlock block = cells[position.x, position.y];
            cells[position.x, position.y] = null;

            if (block != null && destroyObject)
                Destroy(block.gameObject);

            if (block != null)
                BoardChanged?.Invoke();

            return block;
        }

        public void ClearBoard(bool destroyObjects = true)
        {
            EnsureInitialized();

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (cells[x, y] != null)
                    RemoveBlock(new Vector2Int(x, y), destroyObjects);
        }

        public bool CanPlaceTetromino(
            Tetromino tetromino,
            Vector2Int anchorPosition,
            int rotation
        )
        {
            EnsureInitialized();

            if (tetromino == null || tetromino.BlockCount == 0)
                return false;

            HashSet<Vector2Int> positions = new HashSet<Vector2Int>();

            for (int i = 0; i < tetromino.BlockCount; i++)
            {
                Vector2Int position = tetromino.GetCellPosition(
                    i,
                    anchorPosition,
                    rotation
                );

                if (!IsInside(position) ||
                    IsOccupied(position) ||
                    !positions.Add(position))
                    return false;
            }

            return true;
        }

        public bool TryLockTetromino(Tetromino tetromino)
        {
            if (tetromino == null ||
                !CanPlaceTetromino(
                    tetromino,
                    tetromino.AnchorPosition,
                    tetromino.Rotation
                ))
                return false;

            for (int i = 0; i < tetromino.BlockCount; i++)
            {
                BoardBlock block = tetromino.GetBlock(i);
                Vector2Int position = tetromino.GetCellPosition(
                    i,
                    tetromino.AnchorPosition,
                    tetromino.Rotation
                );

                block.transform.SetParent(BlocksRoot, true);
                cells[position.x, position.y] = block;
                MoveBlockToCell(block, position);
            }

            tetromino.CompleteLock();
            return true;
        }

        private void RebuildFromChildren()
        {
            Transform searchRoot = BlocksRoot;
            BoardBlock[] blocks = searchRoot.GetComponentsInChildren<BoardBlock>();

            foreach (BoardBlock block in blocks)
            {
                Vector2Int position = WorldToGrid(block.transform.position);

                if (!TryRegister(block, position))
                    Debug.LogWarning(
                        $"No se pudo registrar {block.name} en la celda {position}.",
                        block
                    );
            }
        }

        public void MoveBlockToCell(BoardBlock block, Vector2Int position)
        {
            if (block == null)
                return;

            block.SetGridPosition(position);
            block.transform.position = GridToWorld(position);
        }

        private void EnsureInitialized()
        {
            if (cells == null ||
                cells.GetLength(0) != width ||
                cells.GetLength(1) != height)
                cells = new BoardBlock[width, height];
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);

            for (int x = 0; x <= width; x++)
            {
                Vector3 start = transform.TransformPoint(new Vector3(x * cellSize, 0f));
                Vector3 end = transform.TransformPoint(new Vector3(x * cellSize, height * cellSize));
                Gizmos.DrawLine(start, end);
            }

            for (int y = 0; y <= height; y++)
            {
                Vector3 start = transform.TransformPoint(new Vector3(0f, y * cellSize));
                Vector3 end = transform.TransformPoint(new Vector3(width * cellSize, y * cellSize));
                Gizmos.DrawLine(start, end);
            }
        }
    }
}
