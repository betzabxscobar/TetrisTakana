using UnityEngine;

namespace TetrisTakana
{
    public class BoardBlock : MonoBehaviour
    {
        public Vector2Int GridPosition { get; private set; }
        public Tetromino Tetromino { get; private set; }

        public void SetTetromino(Tetromino tetromino)
        {
            Tetromino = tetromino;
        }

        public void SetGridPosition(Vector2Int position)
        {
            GridPosition = position;
            Tetromino = null;
        }
    }
}
