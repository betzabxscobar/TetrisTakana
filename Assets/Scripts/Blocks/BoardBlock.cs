using UnityEngine;

namespace TetrisTakana
{
    public class BoardBlock : MonoBehaviour
    {
        public Vector2Int GridPosition { get; private set; }

        public void SetGridPosition(Vector2Int position)
        {
            GridPosition = position;
        }
    }
}
