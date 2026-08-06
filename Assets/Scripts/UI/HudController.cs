using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Presenta puntos, líneas y nivel dentro de un panel responsivo situado a
    /// la izquierda del tablero. Las tarjetas de pausa y de fin de partida se
    /// gestionan aparte, cada una en su propio controlador.
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

        [Header("Estilo")]
        [SerializeField, Min(8)] private int valueFontSize = 52;
        [SerializeField] private Color scoreColor = new Color(0.184f, 0.635f, 0.863f);
        [SerializeField] private Color linesColor = new Color(0.282f, 0.655f, 0.161f);
        [SerializeField] private Color levelColor = new Color(0.984f, 0.404f, 0f);

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
        private GameOverCardController gameOverCardController;
        private PauseCardController pauseCardController;
        private bool ownsGameOverCardController;
        private bool ownsPauseCardController;
        private RectTransform safeAreaRect;
        private RectTransform statsPanelRect;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private Vector2 lastPanelSize;
        private Vector2 lastPanelPosition;

        /// <summary>Busca la partida y monta el HUD del modo Tetris.</summary>
        private void Awake()
        {
            game ??= FindAnyObjectByType<TetrisGame>();
            scoreManager ??= FindAnyObjectByType<ScoreManager>();
            difficulty ??= FindAnyObjectByType<DifficultySystem>();
            board ??= FindAnyObjectByType<Board>();
            targetCamera ??= Camera.main;

            if (scoreValueLabel == null ||
                linesValueLabel == null ||
                levelValueLabel == null)
                CreateDefaultHud();

            if (canvasObject != null)
                canvasObject.SetActive(isActiveAndEnabled);

            EnsureGameOverCard();
            EnsurePauseCard();
        }

        /// <summary>Enciende las tarjetas propias y se suscribe a marcador y estado.</summary>
        private void OnEnable()
        {
            if (ownsGameOverCardController &&
                gameOverCardController != null &&
                !gameOverCardController.enabled)
                gameOverCardController.enabled = true;

            if (ownsPauseCardController &&
                pauseCardController != null &&
                !pauseCardController.enabled)
                pauseCardController.enabled = true;

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

        /// <summary>Reajusta el HUD cuando cambia la pantalla.</summary>
        private void LateUpdate()
        {
            RefreshResponsiveLayout(false);
        }

        /// <summary>Se da de baja de todos los eventos.</summary>
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

            if (ownsGameOverCardController && gameOverCardController != null)
                gameOverCardController.enabled = false;

            if (ownsPauseCardController && pauseCardController != null)
                pauseCardController.enabled = false;

            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        /// <summary>Destruye el canvas que creo este componente.</summary>
        private void OnDestroy()
        {
            if (canvasObject != null)
                Destroy(canvasObject);
        }

        private void HandleScoreChanged(int score, int gained) => RefreshValues();

        private void HandleLinesChanged(int lines) => RefreshValues();

        private void HandleLevelChanged(int level) => RefreshValues();

        /// <summary>Refresca los numeros al cambiar de estado la partida.</summary>
        private void HandleStateChanged(TetrisGame.GameState state)
        {
            // La pausa y el fin de partida los pintan sus propias tarjetas; el
            // HUD solo refresca los marcadores.
            RefreshValues();
        }

        /// <summary>Escribe puntuacion, lineas y nivel en pantalla.</summary>
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

        /// <summary>Monta el canvas y el panel de estadisticas.</summary>
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

            UpdateSafeArea();
            Canvas.ForceUpdateCanvases();
            RefreshResponsiveLayout(true);
        }

        /// <summary>Se asegura de que existe la tarjeta de fin de partida.</summary>
        private void EnsureGameOverCard()
        {
            gameOverCardController = GetComponent<GameOverCardController>();

            if (gameOverCardController == null)
            {
                gameOverCardController = gameObject.AddComponent<GameOverCardController>();
                ownsGameOverCardController = true;
            }

            gameOverCardController.Configure(game, scoreManager);
        }

        /// <summary>Se asegura de que existe la tarjeta de pausa.</summary>
        private void EnsurePauseCard()
        {
            // Si la escena ya trae el componente, conserva el arte que tenga
            // asignado en el inspector en lugar de crear uno pelado.
            pauseCardController = GetComponent<PauseCardController>();

            if (pauseCardController == null)
            {
                pauseCardController = gameObject.AddComponent<PauseCardController>();
                ownsPauseCardController = true;
            }

            pauseCardController.Configure(game, scoreManager, difficulty);
        }

        /// <summary>Crea el panel con puntuacion, lineas y nivel.</summary>
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

        /// <summary>Crea una de las cifras del panel con su rotulo.</summary>
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

        /// <summary>Crea un texto con fuente, tamaño y color indicados.</summary>
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

        /// <summary>Rehace el encuadre solo si cambio la pantalla o la zona segura.</summary>
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

        /// <summary>Ajusta el HUD a la zona segura del dispositivo.</summary>
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

        /// <summary>Coloca el panel en el hueco libre al lado del tablero.</summary>
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

        /// <summary>Calcula que trozo de pantalla ocupa el tablero.</summary>
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

        /// <summary>Proporcion del marco del panel.</summary>
        private float GetFrameAspect()
        {
            if (statsFrameSprite != null && statsFrameSprite.rect.height > 0f)
                return statsFrameSprite.rect.width / statsFrameSprite.rect.height;

            return 0.56f;
        }

        /// <summary>Crea un objeto de interfaz vacio colgado de otro.</summary>
        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject instance = new GameObject(objectName, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<RectTransform>();
        }

        /// <summary>Estira un objeto para que ocupe todo su padre.</summary>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Compara dos vectores con el margen de error de los flotantes.</summary>
        private static bool Approximately(Vector2 first, Vector2 second)
        {
            return Mathf.Approximately(first.x, second.x) &&
                   Mathf.Approximately(first.y, second.y);
        }
    }
}
