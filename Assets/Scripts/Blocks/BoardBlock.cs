using UnityEngine;

namespace TetrisTakana
{
    public class BoardBlock : MonoBehaviour
    {
        [SerializeField, Min(0)] private int blockType;

        public Vector2Int GridPosition { get; private set; }
        public Tetromino Tetromino { get; private set; }
        public int BlockType => blockType;
        public bool IsMoving { get; private set; }

        public void SetBlockType(int type)
        {
            blockType = Mathf.Max(0, type);
        }

        public void SetTetromino(Tetromino tetromino)
        {
            Tetromino = tetromino;
        }

        public void SetGridPosition(Vector2Int position)
        {
            GridPosition = position;
            Tetromino = null;
        }

        public void SetMoving(bool moving)
        {
            IsMoving = moving;
        }
    }
}
