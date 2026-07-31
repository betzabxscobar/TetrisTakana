using UnityEngine;

namespace TetrisTakana
{
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardImageFitter : MonoBehaviour
    {
        [SerializeField] private Board board;

        private void OnEnable()
        {
            CenterImageOnBoard();
        }

        private void Update()
        {
            CenterImageOnBoard();
        }

        private void OnValidate()
        {
            CenterImageOnBoard();
        }

        [ContextMenu("Centrar imagen en la grilla")]
        public void CenterImageOnBoard()
        {
            if (board == null)
                board = GetComponentInParent<Board>();

            if (board == null)
                return;

            if (transform.parent != board.transform)
            {
                Debug.LogWarning(
                    "BoardImageFitter debe estar en un objeto hijo directo del Board.",
                    this
                );
                return;
            }

            transform.localPosition = new Vector3(
                board.Width * board.CellSize * 0.5f,
                board.Height * board.CellSize * 0.5f,
                transform.localPosition.z
            );
        }
    }
}
