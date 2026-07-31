using System;
using UnityEngine;

namespace TetrisTakana
{
    public class Tetromino : MonoBehaviour
    {
        [SerializeField] private BoardBlock[] blocks = new BoardBlock[4];
        [SerializeField] private Vector2Int[] cellOffsets = new Vector2Int[4];
        [SerializeField, Range(0.1f, 1f)] private float cellFill = 0.92f;

        private Board board;
        private bool initialized;
        private bool locked;

        public int BlockCount => Mathf.Min(blocks.Length, cellOffsets.Length);
        public Vector2Int AnchorPosition { get; private set; }
        public int Rotation { get; private set; }
        public bool IsLocked => locked;

        public event Action<Tetromino> Locked;

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

        public bool TryRotate(bool clockwise = true)
        {
            if (!CanBeControlled())
                return false;

            int nextRotation = NormalizeRotation(Rotation + (clockwise ? 1 : -1));
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

                if (!board.CanPlaceTetromino(
                        this,
                        candidateAnchor,
                        nextRotation
                    ))
                    continue;

                AnchorPosition = candidateAnchor;
                Rotation = nextRotation;
                UpdateBlockTransforms();
                return true;
            }

            return false;
        }

        public bool TryLock()
        {
            return CanBeControlled() && board.TryLockTetromino(this);
        }

        public BoardBlock GetBlock(int index)
        {
            return blocks[index];
        }

        public Vector2Int GetCellPosition(
            int index,
            Vector2Int anchorPosition,
            int rotation
        )
        {
            return anchorPosition + RotateOffset(cellOffsets[index], rotation);
        }

        public Vector2Int GetCellOffset(int index)
        {
            return cellOffsets[index];
        }

        public void CompleteLock()
        {
            locked = true;
            initialized = false;
            Locked?.Invoke(this);
            Destroy(gameObject);
        }

        private bool CanBeControlled()
        {
            return initialized && !locked && board != null;
        }

        private bool HasValidConfiguration()
        {
            if (blocks == null ||
                cellOffsets == null ||
                blocks.Length != 4 ||
                cellOffsets.Length != 4)
            {
                Debug.LogError(
                    $"{name} debe tener exactamente cuatro bloques y cuatro posiciones.",
                    this
                );
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

        private void LayoutBlocks()
        {
            for (int i = 0; i < BlockCount; i++)
            {
                BoardBlock block = blocks[i];
                block.SetTetromino(this);

                SpriteRenderer renderer = block.GetComponent<SpriteRenderer>();

                if (renderer == null || renderer.sprite == null)
                    continue;

                Vector2 spriteSize = renderer.sprite.bounds.size;
                float targetSize = board.CellSize * cellFill;

                block.transform.localScale = new Vector3(
                    targetSize / spriteSize.x,
                    targetSize / spriteSize.y,
                    1f
                );
            }
        }

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

        private static Vector2Int RotateOffset(Vector2Int offset, int rotation)
        {
            switch (NormalizeRotation(rotation))
            {
                case 1:
                    return new Vector2Int(offset.y, -offset.x);
                case 2:
                    return new Vector2Int(-offset.x, -offset.y);
                case 3:
                    return new Vector2Int(-offset.y, offset.x);
                default:
                    return offset;
            }
        }

        private static int NormalizeRotation(int rotation)
        {
            return (rotation % 4 + 4) % 4;
        }
    }
}
