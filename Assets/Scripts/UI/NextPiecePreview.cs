using System.Collections.Generic;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Dibuja la pieza que viene a continuación, centrada sobre un ancla al
    /// lado del tablero. Reutiliza siempre los mismos cuatro sprites.
    /// </summary>
    public class NextPiecePreview : MonoBehaviour
    {
        [SerializeField] private PieceSpawner spawner;
        [SerializeField] private Board board;
        [Tooltip("Punto donde se centra la pieza. Si se deja vacío se usa este objeto.")]
        [SerializeField] private Transform anchor;
        [SerializeField, Range(0.1f, 1f)] private float cellFill = 0.94f;
        [SerializeField, Min(0.01f)] private float cellSizeOverride;
        [SerializeField] private int sortingOrder = 15;

        private readonly List<SpriteRenderer> cells = new List<SpriteRenderer>();

        private float CellSize => cellSizeOverride > 0.01f
            ? cellSizeOverride
            : (board != null ? board.CellSize : 0.5f);

        private Transform Anchor => anchor != null ? anchor : transform;

        private void Awake()
        {
            spawner ??= FindAnyObjectByType<PieceSpawner>();
            board ??= FindAnyObjectByType<Board>();
        }

        private void OnEnable()
        {
            if (spawner == null)
                return;

            spawner.NextPrefabChanged += Render;
            Render(spawner.NextPrefab);
        }

        private void OnDisable()
        {
            if (spawner != null)
                spawner.NextPrefabChanged -= Render;
        }

        public void Render(Tetromino prefab)
        {
            if (prefab == null)
            {
                Hide();
                return;
            }

            int count = prefab.BlockCount;
            EnsureCells(count);

            // Centrar la pieza dentro del recuadro: se mide su caja real, no
            // la de rotación, para que la I no quede descolgada.
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < count; i++)
            {
                Vector2Int offset = prefab.GetCellOffset(i);
                minX = Mathf.Min(minX, offset.x);
                maxX = Mathf.Max(maxX, offset.x);
                minY = Mathf.Min(minY, offset.y);
                maxY = Mathf.Max(maxY, offset.y);
            }

            Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            float cellSize = CellSize;
            Sprite sprite = prefab.BlockSprite;

            for (int i = 0; i < cells.Count; i++)
            {
                SpriteRenderer cell = cells[i];

                if (i >= count)
                {
                    cell.gameObject.SetActive(false);
                    continue;
                }

                Vector2Int offset = prefab.GetCellOffset(i);
                cell.gameObject.SetActive(true);
                cell.sprite = sprite;
                cell.transform.position = Anchor.position + new Vector3(
                    (offset.x - center.x) * cellSize,
                    (offset.y - center.y) * cellSize,
                    0f);

                FitToCell(cell, cellSize);
            }
        }

        public void Hide()
        {
            foreach (SpriteRenderer cell in cells)
                cell.gameObject.SetActive(false);
        }

        private void FitToCell(SpriteRenderer cell, float cellSize)
        {
            if (cell.sprite == null)
                return;

            Vector2 size = cell.sprite.bounds.size;

            if (size.x <= 0f || size.y <= 0f)
                return;

            float target = cellSize * cellFill;
            cell.transform.localScale = new Vector3(target / size.x, target / size.y, 1f);
        }

        private void EnsureCells(int count)
        {
            while (cells.Count < count)
            {
                GameObject instance = new GameObject($"Preview Cell {cells.Count + 1}");
                instance.transform.SetParent(Anchor, false);

                SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = sortingOrder;
                cells.Add(renderer);
            }
        }
    }
}
