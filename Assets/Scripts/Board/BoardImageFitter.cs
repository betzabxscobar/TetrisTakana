using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Centra el marco/fondo sobre la grilla y, opcionalmente, lo escala para
    /// que cubra el tablero completo aunque cambien las dimensiones o el
    /// tamaño de celda.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardImageFitter : MonoBehaviour
    {
        [SerializeField] private Board board;
        [Tooltip("Escala el sprite para que cubra la grilla más el margen indicado.")]
        [SerializeField] private bool matchBoardSize = true;
        [SerializeField] private Vector2 padding = new Vector2(0.4f, 0.4f);

        private SpriteRenderer spriteRenderer;
        private int lastWidth;
        private int lastHeight;
        private float lastCellSize;

        private void OnEnable()
        {
            Fit();
        }

        private void OnValidate()
        {
            Fit();
        }

        private void Update()
        {
            if (board == null)
                return;

            if (board.Width == lastWidth &&
                board.Height == lastHeight &&
                Mathf.Approximately(board.CellSize, lastCellSize))
                return;

            Fit();
        }

        [ContextMenu("Ajustar imagen al tablero")]
        public void Fit()
        {
            if (board == null)
                board = GetComponentInParent<Board>();

            if (board == null)
                return;

            if (transform.parent != board.transform)
            {
                Debug.LogWarning(
                    "BoardImageFitter debe estar en un objeto hijo directo del Board.",
                    this);
                return;
            }

            lastWidth = board.Width;
            lastHeight = board.Height;
            lastCellSize = board.CellSize;

            float boardWidth = board.Width * board.CellSize;
            float boardHeight = board.Height * board.CellSize;

            transform.localPosition = new Vector3(
                boardWidth * 0.5f,
                boardHeight * 0.5f,
                transform.localPosition.z);

            if (!matchBoardSize)
                return;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;

            if (sprite == null)
                return;

            Vector2 spriteSize = sprite.bounds.size;

            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                return;

            transform.localScale = new Vector3(
                (boardWidth + padding.x * 2f) / spriteSize.x,
                (boardHeight + padding.y * 2f) / spriteSize.y,
                1f);
        }
    }
}
