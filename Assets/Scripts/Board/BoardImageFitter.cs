using UnityEngine;

namespace TetrisTakana
{
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardImageFitter : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private float padding;

        private SpriteRenderer spriteRenderer;

        private void OnEnable()
        {
            FitImageToBoard();
        }

        private void Update()
        {
            FitImageToBoard();
        }

        private void OnValidate()
        {
            FitImageToBoard();
        }

        [ContextMenu("Ajustar imagen a la grilla")]
        public void FitImageToBoard()
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

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer.sprite == null)
                return;

            float targetWidth = Mathf.Max(
                0.01f,
                board.Width * board.CellSize - padding * 2f
            );

            float targetHeight = Mathf.Max(
                0.01f,
                board.Height * board.CellSize - padding * 2f
            );

            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;

            transform.localPosition = new Vector3(
                board.Width * board.CellSize * 0.5f,
                board.Height * board.CellSize * 0.5f,
                transform.localPosition.z
            );

            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(
                targetWidth / spriteSize.x,
                targetHeight / spriteSize.y,
                1f
            );
        }
    }
}
