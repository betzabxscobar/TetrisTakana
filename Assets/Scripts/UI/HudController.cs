using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Presenta puntos, líneas y nivel dentro de un panel responsivo situado a
    /// la izquierda del tablero. También muestra los mensajes de pausa y fin
    /// de partida en una capa superior del HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public class HudController : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private TetrisGame game;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private DifficultySystem difficulty;
        [SerializeField] private Board board;
        [SerializeField] private Camera targetCamera;

        [Header("Marco de estadísticas")]
        [SerializeField] private Sprite statsFrameSprite;
        [SerializeField] private Text scoreValueLabel;
        [SerializeField] private Text linesValueLabel;
        [SerializeField] private Text levelValueLabel;
        [SerializeField] private Text messageLabel;

        [Header("Estilo")]
        [SerializeField, Min(8)] private int valueFontSize = 52;
        [SerializeField, Min(8)] private int messageFontSize = 52;
        [SerializeField] private Color scoreColor = new Color(0.184f, 0.635f, 0.863f);
        [SerializeField] private Color linesColor = new Color(0.282f, 0.655f, 0.161f);
        [SerializeField] private Color levelColor = new Color(0.984f, 0.404f, 0f);
        [SerializeField] private Color messageColor = Color.white;

        [Header("Diseño responsivo")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;
        [SerializeField, Min(1f)] private float preferredPanelWidth = 300f;
        [SerializeField] private Vector2 panelWidthRange = new Vector2(240f, 340f);
        [SerializeField, Min(0f)] private float margin = 40f;
        [SerializeField] private int sortingOrder = 100;

        [Header("Casillas dentro del marco")]
        [SerializeField] private Vector2 scoreAnchorMin = new Vector2(0.20f, 0.67f);
        [SerializeField] private Vector2 scoreAnchorMax = new Vector2(0.80f, 0.82f);
        [SerializeField] private Vector2 linesAnchorMin = new Vector2(0.20f, 0.41f);
        [SerializeField] private Vector2 linesAnchorMax = new Vector2(0.80f, 0.51f);
        [SerializeField] private Vector2 levelAnchorMin = new Vector2(0.20f, 0.10f);
        [SerializeField] private Vector2 levelAnchorMax = new Vector2(0.81f, 0.25f);

        private readonly Vector3[] boardLocalCorners = new Vector3[4];

        private GameObject canvasObject;
        private RectTransform safeAreaRect;
        private RectTransform statsPanelRect;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private Vector2 lastPanelSize;
        private Vector2 lastPanelPosition;

        private void Awake()
        {
            game ??= FindAnyObjectByType<TetrisGame>();
            scoreManager ??= FindAnyObjectByType<ScoreManager>();
            difficulty ??= FindAnyObjectByType<DifficultySystem>();
            board ??= FindAnyObjectByType<Board>();
            targetCamera ??= Camera.main;

            if (scoreValueLabel == null ||
                linesValueLabel == null ||
                levelValueLabel == null ||
                messageLabel == null)
                CreateDefaultHud();

            if (canvasObject != null)
                canvasObject.SetActive(isActiveAndEnabled);
        }

        private void OnEnable()
        {
            if (canvasObject != null)
                canvasObject.SetActive(true);

            if (scoreManager != null)
            {
                scoreManager.ScoreChanged += HandleScoreChanged;
                scoreManager.LinesChanged += HandleLinesChanged;
            }

            if (difficulty != null)
                difficulty.LevelChanged += HandleLevelChanged;

            if (game != null)
                game.StateChanged += HandleStateChanged;

            HandleStateChanged(game != null ? game.State : TetrisGame.GameState.Ready);
        }

        private void LateUpdate()
        {
            RefreshResponsiveLayout(false);
        }

        private void OnDisable()
        {
            if (scoreManager != null)
            {
                scoreManager.ScoreChanged -= HandleScoreChanged;
                scoreManager.LinesChanged -= HandleLinesChanged;
            }

            if (difficulty != null)
                difficulty.LevelChanged -= HandleLevelChanged;

            if (game != null)
                game.StateChanged -= HandleStateChanged;

            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (canvasObject != null)
                Destroy(canvasObject);
        }

        private void HandleScoreChanged(int score, int gained) => RefreshValues();

        private void HandleLinesChanged(int lines) => RefreshValues();

        private void HandleLevelChanged(int level) => RefreshValues();

        private void HandleStateChanged(TetrisGame.GameState state)
        {
            if (messageLabel != null)
            {
                switch (state)
                {
                    case TetrisGame.GameState.Paused:
                        messageLabel.text = "PAUSA\n\nEsc para continuar";
                        break;

                    case TetrisGame.GameState.GameOver:
                        int score = scoreManager != null ? scoreManager.Score : 0;
                        messageLabel.text =
                            $"FIN DE LA PARTIDA\n\nPuntaje {score}\n\nEnter para jugar de nuevo";
                        break;

                    default:
                        messageLabel.text = string.Empty;
                        break;
                }
            }

            RefreshValues();
        }

        private void RefreshValues()
        {
            int score = scoreManager != null ? scoreManager.Score : 0;
            int lines = scoreManager != null ? scoreManager.TotalLines : 0;
            int level = difficulty != null ? difficulty.Level : 1;

            if (scoreValueLabel != null)
                scoreValueLabel.text = score.ToString("D6");

            if (linesValueLabel != null)
                linesValueLabel.text = lines.ToString("D3");

            if (levelValueLabel != null)
                levelValueLabel.text = level.ToString("D2");
        }

        private void CreateDefaultHud()
        {
            canvasObject = new GameObject(
                "Tetris HUD Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = matchWidthOrHeight;

            if (scoreValueLabel == null ||
                linesValueLabel == null ||
                levelValueLabel == null)
                CreateStatsPanel(canvasObject.transform);

            if (messageLabel == null)
            {
                messageLabel = CreateText(
                    canvasObject.transform,
                    "Mensaje",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    messageColor,
                    messageFontSize,
                    false);

                RectTransform messageRect = messageLabel.rectTransform;
                messageRect.anchoredPosition = Vector2.zero;
                messageRect.sizeDelta = new Vector2(900f, 400f);
                messageLabel.text = string.Empty;
            }

            UpdateSafeArea();
            Canvas.ForceUpdateCanvases();
            RefreshResponsiveLayout(true);
        }

        private void CreateStatsPanel(Transform canvasTransform)
        {
            safeAreaRect = CreateRect("Safe Area", canvasTransform);
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;

            statsPanelRect = CreateRect("Stats Panel", safeAreaRect);
            statsPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            statsPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            statsPanelRect.pivot = new Vector2(0.5f, 0.5f);

            RectTransform frameRect = CreateRect("Frame", statsPanelRect);
            Stretch(frameRect);

            Image frame = frameRect.gameObject.AddComponent<Image>();
            frame.sprite = statsFrameSprite;
            frame.preserveAspect = true;
            frame.raycastTarget = false;

            if (scoreValueLabel == null)
                scoreValueLabel = CreateValueLabel(
                    "Puntos",
                    scoreAnchorMin,
                    scoreAnchorMax,
                    scoreColor);

            if (linesValueLabel == null)
                linesValueLabel = CreateValueLabel(
                    "Lineas",
                    linesAnchorMin,
                    linesAnchorMax,
                    linesColor);

            if (levelValueLabel == null)
                levelValueLabel = CreateValueLabel(
                    "Nivel",
                    levelAnchorMin,
                    levelAnchorMax,
                    levelColor);
        }

        private Text CreateValueLabel(
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            Text label = CreateText(
                statsPanelRect,
                objectName,
                anchorMin,
                anchorMax,
                color,
                valueFontSize,
                true);

            label.text = "0";
            return label;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            int fontSize,
            bool bestFit)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Text),
                typeof(Shadow));
            textObject.transform.SetParent(parent, false);

            Text label = textObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = bestFit;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = fontSize;
            label.supportRichText = false;
            label.raycastTarget = false;

            Shadow shadow = textObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            return label;
        }

        private void RefreshResponsiveLayout(bool force)
        {
            if (safeAreaRect == null || statsPanelRect == null)
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
                float availableLeft = boardRect.xMin - margin - (safeRect.xMin + margin);

                if (availableLeft >= minWidth)
                {
                    width = Mathf.Min(width, availableLeft);
                    float freeLeft = safeRect.xMin + margin;
                    float freeRight = boardRect.xMin - margin;
                    float height = width / aspect;

                    position = new Vector2(
                        (freeLeft + freeRight) * 0.5f,
                        Mathf.Min(boardRect.yMax, safeRect.yMax - margin) - height * 0.5f);
                }
                else
                {
                    float availableBottom = boardRect.yMin - margin - (safeRect.yMin + margin);
                    float maxWidthBelow = Mathf.Min(
                        safeRect.width - margin * 2f,
                        availableBottom * aspect);
                    float availableTop = safeRect.yMax - margin - (boardRect.yMax + margin);
                    float maxWidthAbove = Mathf.Min(
                        safeRect.width - margin * 2f,
                        availableTop * aspect);

                    if (maxWidthBelow >= minWidth)
                    {
                        // El panel de siguiente pieza usa la zona derecha/superior;
                        // las estadísticas prefieren la zona inferior cuando falta
                        // espacio lateral para que ambos HUD no compitan.
                        width = Mathf.Min(width, maxWidthBelow);
                        float height = width / aspect;
                        float centerX = Mathf.Clamp(
                            boardRect.center.x,
                            safeRect.xMin + margin + width * 0.5f,
                            safeRect.xMax - margin - width * 0.5f);

                        position = new Vector2(
                            centerX,
                            boardRect.yMin - margin - height * 0.5f);
                    }
                    else if (maxWidthAbove >= minWidth)
                    {
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
                    else if (availableLeft > 1f)
                    {
                        width = Mathf.Min(width, availableLeft);
                        float height = width / aspect;
                        float freeLeft = safeRect.xMin + margin;
                        float freeRight = boardRect.xMin - margin;

                        position = new Vector2(
                            (freeLeft + freeRight) * 0.5f,
                            Mathf.Min(boardRect.yMax, safeRect.yMax - margin) - height * 0.5f);
                    }
                    else
                    {
                        width = Mathf.Min(
                            width,
                            Mathf.Max(120f, safeRect.width * 0.24f));

                        float height = width / aspect;
                        position = new Vector2(
                            safeRect.xMin + margin + width * 0.5f,
                            safeRect.yMax - margin - height * 0.5f);
                    }
                }
            }
            else
            {
                float height = width / aspect;
                position = new Vector2(
                    safeRect.xMin + margin + width * 0.5f,
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

            if (Approximately(size, lastPanelSize) &&
                Approximately(position, lastPanelPosition))
                return;

            lastPanelSize = size;
            lastPanelPosition = position;
            statsPanelRect.sizeDelta = size;
            statsPanelRect.anchoredPosition = position;
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

        private float GetFrameAspect()
        {
            if (statsFrameSprite != null && statsFrameSprite.rect.height > 0f)
                return statsFrameSprite.rect.width / statsFrameSprite.rect.height;

            return 0.56f;
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
