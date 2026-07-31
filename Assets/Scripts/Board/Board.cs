using System;
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
        [SerializeField] private bool allowSwapWithEmptyCell;

        private BoardBlock[,] cells;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;

        public event Action<Vector2Int, Vector2Int> BlocksSwapped;

        private void Awake()
        {
            cells = new BoardBlock[width, height];
            RebuildFromChildren();
        }

        public bool IsInside(Vector2Int position)
        {
            return position.x >= 0 && position.x < width &&
                   position.y >= 0 && position.y < height;
        }

        public bool IsOccupied(Vector2Int position)
        {
            return IsInside(position) && cells[position.x, position.y] != null;
        }

        public bool TryGetBlock(Vector2Int position, out BoardBlock block)
        {
            block = IsInside(position) ? cells[position.x, position.y] : null;
            return block != null;
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

        public bool AreAdjacent(Vector2Int first, Vector2Int second)
        {
            Vector2Int distance = first - second;
            return Mathf.Abs(distance.x) + Mathf.Abs(distance.y) == 1;
        }

        public bool TrySwap(Vector2Int first, Vector2Int second)
        {
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
            return true;
        }

        public bool TryRegister(BoardBlock block, Vector2Int position)
        {
            if (block == null || !IsInside(position) || IsOccupied(position))
                return false;

            cells[position.x, position.y] = block;
            MoveBlockToCell(block, position);
            return true;
        }

        private void RebuildFromChildren()
        {
            Transform searchRoot = blocksRoot != null ? blocksRoot : transform;
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

        private void MoveBlockToCell(BoardBlock block, Vector2Int position)
        {
            if (block == null)
                return;

            block.SetGridPosition(position);
            block.transform.position = GridToWorld(position);
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
