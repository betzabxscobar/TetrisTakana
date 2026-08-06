using UnityEngine;

namespace TetrisTakana
{
    /// <summary>Una ficha del tablero: sabe de que tipo es, en que celda esta y si se esta moviendo.</summary>
    public class BoardBlock : MonoBehaviour
    {
        [SerializeField, Min(0)] private int blockType;

        public Vector2Int GridPosition { get; private set; }
        public Tetromino Tetromino { get; private set; }
        public int BlockType => blockType;
        public bool IsMoving { get; private set; }

        /// <summary>Fija el tipo de ficha, que es lo que decide su color.</summary>
        public void SetBlockType(int type)
        {
            blockType = Mathf.Max(0, type);
        }

        /// <summary>Ata la ficha a la pieza que la lleva; con null queda suelta en el tablero.</summary>
        public void SetTetromino(Tetromino tetromino)
        {
            Tetromino = tetromino;
        }

        /// <summary>Apunta en que celda de la rejilla esta la ficha.</summary>
        public void SetGridPosition(Vector2Int position)
        {
            GridPosition = position;
            Tetromino = null;
        }

        /// <summary>Marca si la ficha esta animandose ahora mismo.</summary>
        public void SetMoving(bool moving)
        {
            IsMoving = moving;
        }
    }
}
