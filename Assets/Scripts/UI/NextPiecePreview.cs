using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Muestra la siguiente pieza dentro de un panel de UI que se coloca en el
    /// espacio libre a la derecha del tablero. El Canvas usa una resolución de
    /// referencia y respeta el área segura, por lo que conserva tamaño, margen
    /// y legibilidad al cambiar la resolución o la relación de aspecto.
    /// </summary>
    [DisallowMultipleComponent]
    public class NextPiecePreview : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private PieceSpawner spawner;
        [SerializeField] private Board board;
        [SerializeField] private Camera targetCamera;

        [Header("Marco")]
        [SerializeField] private Sprite frameSprite;
        [SerializeField, Range(0.1f, 1f)] private float cellFill = 0.94f;
        [SerializeField] private int sortingOrder = 90;

        [Header("Diseño responsivo")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;
        [SerializeField, Min(1f)] private float preferredPanelWidth = 320f;
        [SerializeField] private Vector2 panelWidthRange = new Vector2(220f, 360f);
        [SerializeField, Min(0f)] private float margin = 40f;

        [Header("Ventana interior del marco")]
        [SerializeField] private Vector2 previewAnchorMin = new Vector2(0.202f, 0.187f);
        [SerializeField] private Vector2 previewAnchorMax = new Vector2(0.798f, 0.696f);

        private readonly List<Image> cells = new List<Image>(4);
        private readonly Vector3[] boardLocalCorners = new Vector3[4];

        private GameObject canvasObject;
        private Canvas canvas;
        private CanvasScaler canvasScaler;
        private RectTransform safeAreaRect;
        private RectTransform panelRect;
        private RectTransform previewRect;
        private Image frameImage;
        private Tetromino currentPrefab;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private Vector2 lastPanelSize;
        private Vector2 lastPanelPosition;

        private void Awake()
        {
            spawner ??= FindAnyObjectByType<PieceSpawner>();
            board ??= FindAnyObjectByType<Board>();
            targetCamera ??= Camera.main;

            CreateUi();
            canvasObject.SetActive(isActiveAndEnabled);
        }

        private void OnEnable()
        {
            if (canvasObject != null)
                canvasObject.SetActive(true);

            if (spawner == null)
                return;

            spawner.NextPrefabChanged += Render;
            Render(spawner.NextPrefab);
        }

        private void Start()
        {
            RefreshLayout(true);
        }

        private void LateUpdate()
        {
            RefreshLayout(false);
        }

        private void OnDisable()
        {
            if (spawner != null)
                spawner.NextPrefabChanged -= Render;

            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (canvasObject != null)
                Destroy(canvasObject);
        }

        public void Render(Tetromino prefab)
        {
            currentPrefab = prefab;

            if (prefab == null)
            {
                Hide();
                return;
            }

            EnsureCells(prefab.BlockCount);
            LayoutCells();
        }

        public void Hide()
        {
            foreach (Image cell in cells)
                if (cell != null)
                    cell.gameObject.SetActive(false);
        }

        private void CreateUi()
        {
            if (canvasObject != null)
                return;

            canvasObject = new GameObject(
                "Next Piece Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = referenceResolution;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = matchWidthOrHeight;

            safeAreaRect = CreateRect("Safe Area", canvasObject.transform);
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;

            panelRect = CreateRect("Next Piece Panel", safeAreaRect);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);

            RectTransform frameRect = CreateRect("Frame", panelRect);
            Stretch(frameRect);

            frameImage = frameRect.gameObject.AddComponent<Image>();
            frameImage.sprite = frameSprite;
            frameImage.preserveAspect = true;
            frameImage.raycastTarget = false;

            previewRect = CreateRect("Preview Area", panelRect);
            previewRect.anchorMin = previewAnchorMin;
            previewRect.anchorMax = previewAnchorMax;
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
            previewRect.pivot = new Vector2(0.5f, 0.5f);

            UpdateSafeArea();
            Canvas.ForceUpdateCanvases();
            RefreshLayout(true);
        }

        private void RefreshLayout(bool force)
        {
            if (safeAreaRect == null || panelRect == null)
                return;

            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

            if (force || safeArea != lastSafeArea || screenSize != lastScreenSize)
            {
                lastSafeArea = safeArea;
                lastScreenSize = screenSize;
                UpdateSafeArea();
                Canvas.ForceUpdateCanvases();
            }

            PlacePanelBesideBoard();
        }

        private void UpdateSafeArea()
        {
            if (safeAreaRect == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect area = Screen.safeArea;
            safeAreaRect.anchorMin = new Vector2(
                area.xMin / Screen.width,
                area.yMin / Screen.height);
            safeAreaRect.anchorMax = new Vector2(
                area.xMax / Screen.width,
                area.yMax / Screen.height);
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;
        }

        private void PlacePanelBesideBoard()
        {
            Rect safeRect = safeAreaRect.rect;

            if (safeRect.width <= 0f || safeRect.height <= 0f)
                return;

            float aspect = GetFrameAspect();
            float minWidth = Mathf.Max(1f, Mathf.Min(panelWidthRange.x, panelWidthRange.y));
            float maxWidth = Mathf.Max(minWidth, Mathf.Max(panelWidthRange.x, panelWidthRange.y));
            float maxWidthByHeight = Mathf.Max(1f, (safeRect.height - margin * 2f) * aspect);
            float width = Mathf.Clamp(preferredPanelWidth, minWidth, maxWidth);
            width = Mathf.Min(width, maxWidthByHeight);

            Vector2 position;

            if (TryGetBoardRectInSafeArea(out Rect boardRect))
            {
                float availableRight = safeRect.xMax - margin - (boardRect.xMax + margin);

                if (availableRight >= minWidth)
                {
                    width = Mathf.Min(width, availableRight);

                    float freeLeft = boardRect.xMax + margin;
                    float freeRight = safeRect.xMax - margin;
                    float height = width / aspect;

                    position = new Vector2(
                        (freeLeft + freeRight) * 0.5f,
                        Mathf.Min(boardRect.yMax, safeRect.yMax - margin) - height * 0.5f);
                }
                else
                {
                    float availableTop = safeRect.yMax - margin - (boardRect.yMax + margin);
                    float maxWidthAbove = Mathf.Min(
                        safeRect.width - margin * 2f,
                        availableTop * aspect);

                    if (maxWidthAbove >= minWidth)
                    {
                        // Cuando falta espacio lateral, el panel pasa encima del
                        // tablero sin cubrirlo y conserva su proporción.
                        width = Mathf.Min(width, maxWidthAbove);
                        float height = width / aspect;
                        float centerX = Mathf.Clamp(
                            boardRect.center.x,
                            safeRect.xMin + margin + width * 0.5f,
                            safeRect.xMax - margin - width * 0.5f);

                        position = new Vector2(
                            centerX,
                            boardRect.yMax + margin + height * 0.5f);
                    }
                    else if (availableRight > 1f)
                    {
                        // Si no cabe arriba, se reduce por debajo del tamaño
                        // preferido antes de invadir el área de juego.
                        width = Mathf.Min(width, availableRight);
                        float height = width / aspect;
                        float freeLeft = boardRect.xMax + margin;
                        float freeRight = safeRect.xMax - margin;

                        position = new Vector2(
                            (freeLeft + freeRight) * 0.5f,
                            Mathf.Min(boardRect.yMax, safeRect.yMax - margin) - height * 0.5f);
                    }
                    else
                    {
                        // Último recurso para una pantalla sin espacio externo:
                        // tarjeta compacta dentro del área segura.
                        width = Mathf.Min(
                            width,
                            Mathf.Max(120f, safeRect.width * 0.24f));

                        float height = width / aspect;
                        position = new Vector2(
                            safeRect.xMax - margin - width * 0.5f,
                            safeRect.yMax - margin - height * 0.5f);
                    }
                }
            }
            else
            {
                float height = width / aspect;
                position = new Vector2(
                    safeRect.xMax - margin - width * 0.5f,
                    safeRect.yMax - margin - height * 0.5f);
            }

            Vector2 size = new Vector2(width, width / aspect);
            position.x = Mathf.Clamp(
                position.x,
                safeRect.xMin + margin + size.x * 0.5f,
                safeRect.xMax - margin - size.x * 0.5f);
            position.y = Mathf.Clamp(
                position.y,
                safeRect.yMin + margin + size.y * 0.5f,
                safeRect.yMax - margin - size.y * 0.5f);

            bool changed = !Approximately(size, lastPanelSize) ||
                           !Approximately(position, lastPanelPosition);

            panelRect.sizeDelta = size;
            panelRect.anchoredPosition = position;

            if (!changed)
                return;

            lastPanelSize = size;
            lastPanelPosition = position;
            LayoutCells();
        }

        private bool TryGetBoardRectInSafeArea(out Rect boardRect)
        {
            boardRect = default;

            if (board == null)
                return false;

            targetCamera ??= Camera.main;

            if (targetCamera == null)
                return false;

            float width = board.Width * board.CellSize;
            float height = board.Height * board.CellSize;
            boardLocalCorners[0] = Vector3.zero;
            boardLocalCorners[1] = new Vector3(width, 0f, 0f);
            boardLocalCorners[2] = new Vector3(0f, height, 0f);
            boardLocalCorners[3] = new Vector3(width, height, 0f);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (Vector3 localCorner in boardLocalCorners)
            {
                Vector3 screenPoint = targetCamera.WorldToScreenPoint(
                    board.transform.TransformPoint(localCorner));

                if (screenPoint.z < 0f ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        safeAreaRect,
                        screenPoint,
                        null,
                        out Vector2 localPoint))
                    return false;

                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            boardRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private void LayoutCells()
        {
            if (currentPrefab == null || panelRect == null || previewRect == null)
                return;

            int count = currentPrefab.BlockCount;

            if (count <= 0)
            {
                Hide();
                return;
            }

            EnsureCells(count);

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < count; i++)
            {
                Vector2Int offset = currentPrefab.GetCellOffset(i);
                minX = Mathf.Min(minX, offset.x);
                maxX = Mathf.Max(maxX, offset.x);
                minY = Mathf.Min(minY, offset.y);
                maxY = Mathf.Max(maxY, offset.y);
            }

            Vector2 center = new Vector2(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f);
            int pieceWidth = maxX - minX + 1;
            int pieceHeight = maxY - minY + 1;

            Vector2 panelSize = panelRect.sizeDelta;
            Vector2 previewSize = new Vector2(
                panelSize.x * (previewAnchorMax.x - previewAnchorMin.x),
                panelSize.y * (previewAnchorMax.y - previewAnchorMin.y));
            float cellSize = Mathf.Min(
                previewSize.x / (pieceWidth + 0.8f),
                previewSize.y / (pieceHeight + 0.8f));
            float visualSize = cellSize * cellFill;
            Sprite sprite = GetPreviewSprite(currentPrefab);

            for (int i = 0; i < cells.Count; i++)
            {
                Image cell = cells[i];
                bool visible = i < count && sprite != null;
                cell.gameObject.SetActive(visible);

                if (!visible)
                    continue;

                Vector2Int offset = currentPrefab.GetCellOffset(i);
                RectTransform rect = cell.rectTransform;

                cell.sprite = sprite;
                rect.anchoredPosition = new Vector2(
                    (offset.x - center.x) * cellSize,
                    (offset.y - center.y) * cellSize);
                rect.sizeDelta = new Vector2(visualSize, visualSize);
            }
        }

        private void EnsureCells(int count)
        {
            while (cells.Count < count)
            {
                GameObject instance = new GameObject(
                    $"Preview Cell {cells.Count + 1}",
                    typeof(RectTransform),
                    typeof(Image));
                instance.transform.SetParent(previewRect, false);

                Image image = instance.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;

                RectTransform rect = image.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                cells.Add(image);
            }
        }

        private static Sprite GetPreviewSprite(Tetromino prefab)
        {
            if (prefab.BlockSprite != null)
                return prefab.BlockSprite;

            if (prefab.BlockCount <= 0)
                return null;

            BoardBlock block = prefab.GetBlock(0);
            SpriteRenderer renderer = block != null
                ? block.GetComponent<SpriteRenderer>()
                : null;

            return renderer != null ? renderer.sprite : null;
        }

        private float GetFrameAspect()
        {
            if (frameSprite != null && frameSprite.rect.height > 0f)
                return frameSprite.rect.width / frameSprite.rect.height;

            return 1f;
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject instance = new GameObject(objectName, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool Approximately(Vector2 first, Vector2 second)
        {
            return Mathf.Approximately(first.x, second.x) &&
                   Mathf.Approximately(first.y, second.y);
        }
    }
}
