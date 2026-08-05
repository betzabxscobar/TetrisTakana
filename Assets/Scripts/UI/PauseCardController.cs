using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Tarjeta modal de pausa. Se construye en tiempo de ejecucion, igual que la
    /// de fin de partida, y aparece cuando <see cref="TetrisGame"/> entra en
    /// <see cref="TetrisGame.GameState.Paused"/>, es decir al pulsar Esc o P.
    /// Congela el tiempo, baja la musica y muestra el marcador en curso.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseCardController : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private BoardGame game;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private DifficultySystem difficulty;
        [Header("Datos (modo match-3)")]
        [SerializeField] private Match3.ScoreManager match3Score;
        [SerializeField] private Match3.DifficultySystem match3Difficulty;
        [SerializeField] private string menuScene = "Menu";

        [Header("Arte")]
        [Tooltip("Fondo de la tarjeta. Si queda vacio se dibuja un panel liso.")]
        [SerializeField] private Sprite cardSprite;
        [Tooltip("Icono de pausa que corona la tarjeta.")]
        [SerializeField] private Sprite iconSprite;
        [Tooltip("Sonido que suena al pulsar los botones de la tarjeta.")]
        [SerializeField] private AudioClip clickSfx;

        [Header("Diseno")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [Tooltip("Alto de la tarjeta; el ancho sale del aspecto del sprite.")]
        [SerializeField, Min(120f)] private float cardHeight = 560f;
        [SerializeField] private Vector2 fallbackCardSize = new Vector2(746f, 560f);
        [Tooltip("Margen interior en tanto por uno, para no pisar el marco del arte.")]
        [SerializeField] private Vector2 contentInset = new Vector2(0.11f, 0.13f);
        [SerializeField, Min(0f)] private float safeAreaMargin = 48f;
        [SerializeField] private int sortingOrder = 220;

        [Header("Animacion")]
        [SerializeField, Min(0.05f)] private float entranceDuration = 0.42f;
        [SerializeField, Min(0f)] private float hiddenExtraScale = 0.72f;
        [SerializeField, Min(0f)] private float iconPulseAmount = 0.09f;
        [SerializeField, Min(0.1f)] private float iconPulsePeriod = 1.6f;

        [Header("Colores")]
        [SerializeField] private Color overlayColor = new Color(0.01f, 0.015f, 0.04f, 0.72f);
        [SerializeField] private Color cardFallbackColor = new Color(0.26f, 0.16f, 0.08f, 0.98f);
        [SerializeField] private Color titleColor = new Color(1f, 0.93f, 0.78f, 1f);
        [SerializeField] private Color chipColor = new Color(0.08f, 0.06f, 0.04f, 0.55f);
        [SerializeField] private Color scoreColor = new Color(0.184f, 0.635f, 0.863f, 1f);
        [SerializeField] private Color linesColor = new Color(0.282f, 0.655f, 0.161f, 1f);
        [SerializeField] private Color levelColor = new Color(0.984f, 0.404f, 0f, 1f);
        [SerializeField] private Color resumeButtonColor = new Color(0.20f, 0.62f, 0.28f, 1f);
        [SerializeField] private Color restartButtonColor = new Color(0.50f, 0.18f, 0.92f, 1f);
        [SerializeField] private Color menuButtonColor = new Color(0.62f, 0.28f, 0.12f, 1f);

        [Header("Audio")]
        [Tooltip("Volumen al que baja la musica mientras la partida esta en pausa.")]
        [SerializeField, Range(0f, 1f)] private float pausedMusicVolume = 0.25f;

        private GameObject canvasObject;
        private GameObject overlayObject;
        private GameObject createdEventSystemObject;
        private RectTransform safeAreaRect;
        private RectTransform cardRect;
        private RectTransform iconRect;
        private CanvasGroup overlayGroup;
        private Text scoreValueLabel;
        private Text linesValueLabel;
        private Text levelValueLabel;
        private Button resumeButton;
        private Button restartButton;
        private Button menuButton;
        private Coroutine entranceRoutine;
        private Coroutine pulseRoutine;
        private Sprite roundedSprite;
        private Texture2D roundedTexture;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private Vector2 cardSize;
        private float cardScale = 1f;
        private float previousMusicVolume = -1f;
        private bool timeScaleHeld;
        private bool subscribed;

        private void Awake()
        {
            ResolveReferences();
            CreateInterface();
        }

        private void OnEnable()
        {
            if (canvasObject != null)
                canvasObject.SetActive(true);

            Subscribe();
            RefreshLayout(true);
            HandleStateChanged(game != null ? game.State : TetrisGame.GameState.Ready);
        }

        private void LateUpdate()
        {
            RefreshLayout(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
            HideCard();

            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // Si el objeto muere con la partida en pausa, el juego se quedaria
            // congelado y sin musica en la siguiente escena.
            ReleaseTimeScale();
            RestoreMusic();

            if (resumeButton != null)
                resumeButton.onClick.RemoveListener(Resume);

            if (restartButton != null)
                restartButton.onClick.RemoveListener(RestartGame);

            if (menuButton != null)
                menuButton.onClick.RemoveListener(ReturnToMenu);

            if (canvasObject != null)
                Destroy(canvasObject);

            if (createdEventSystemObject != null)
                Destroy(createdEventSystemObject);

            if (roundedSprite != null)
                Destroy(roundedSprite);

            if (roundedTexture != null)
                Destroy(roundedTexture);
        }

        /// <summary>Permite al HUD entregar sus referencias ya resueltas.</summary>
        public void Configure(
            BoardGame targetGame,
            ScoreManager targetScoreManager,
            DifficultySystem targetDifficulty)
        {
            if (subscribed)
                Unsubscribe();

            game = targetGame != null ? targetGame : game;

            TetrisGame tetris = game as TetrisGame;
            scoreManager = targetScoreManager != null
                ? targetScoreManager
                : tetris != null ? tetris.Score : scoreManager;
            difficulty = targetDifficulty != null
                ? targetDifficulty
                : tetris != null ? tetris.Difficulty : difficulty;

            if (isActiveAndEnabled)
            {
                Subscribe();
                HandleStateChanged(game != null
                    ? game.State
                    : TetrisGame.GameState.Ready);
            }
        }

        private void ResolveReferences()
        {
            game ??= FindAnyObjectByType<BoardGame>();

            TetrisGame tetris = game as TetrisGame;
            scoreManager ??= tetris != null ? tetris.Score : FindAnyObjectByType<ScoreManager>();
            difficulty ??= tetris != null
                ? tetris.Difficulty
                : FindAnyObjectByType<DifficultySystem>();
            match3Score ??= FindAnyObjectByType<Match3.ScoreManager>();
            match3Difficulty ??= FindAnyObjectByType<Match3.DifficultySystem>();
        }

        private void Subscribe()
        {
            if (subscribed || game == null)
                return;

            game.StateChanged += HandleStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (game != null)
                game.StateChanged -= HandleStateChanged;

            subscribed = false;
        }

        private void HandleStateChanged(TetrisGame.GameState state)
        {
            if (state == TetrisGame.GameState.Paused)
                ShowCard();
            else
                HideCard();
        }

        // --- Mostrar y ocultar ------------------------------------------

        private void ShowCard()
        {
            if (overlayObject == null || cardRect == null || overlayGroup == null)
                return;

            StopEntranceAnimation();
            StopPulse();
            overlayObject.SetActive(true);
            RefreshLayout(true);
            RefreshValues();

            if (resumeButton != null)
                resumeButton.interactable = true;

            if (restartButton != null)
                restartButton.interactable = true;

            if (menuButton != null)
                menuButton.interactable = true;

            HoldTimeScale();
            DuckMusic();

            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = true;
            entranceRoutine = StartCoroutine(AnimateEntrance());
            pulseRoutine = StartCoroutine(AnimateIconPulse());
        }

        private void HideCard()
        {
            StopEntranceAnimation();
            StopPulse();
            ReleaseTimeScale();
            RestoreMusic();

            if (overlayGroup != null)
            {
                overlayGroup.alpha = 0f;
                overlayGroup.interactable = false;
                overlayGroup.blocksRaycasts = false;
            }

            if (EventSystem.current != null && IsOwnSelection())
                EventSystem.current.SetSelectedGameObject(null);

            if (overlayObject != null)
                overlayObject.SetActive(false);
        }

        private bool IsOwnSelection()
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;

            return selected != null &&
                   (selected == resumeButton?.gameObject ||
                    selected == restartButton?.gameObject ||
                    selected == menuButton?.gameObject);
        }

        private IEnumerator AnimateEntrance()
        {
            float elapsed = 0f;

            while (elapsed < entranceDuration)
            {
                // Tiempo sin escalar: la pausa deja Time.timeScale en cero.
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / entranceDuration);
                float easedScale = EaseOutBack(progress);
                float easedAlpha = 1f - Mathf.Pow(1f - progress, 3f);

                cardRect.localScale = Vector3.one * cardScale *
                    Mathf.LerpUnclamped(hiddenExtraScale, 1f, easedScale);
                overlayGroup.alpha = easedAlpha;
                yield return null;
            }

            cardRect.localScale = Vector3.one * cardScale;
            overlayGroup.alpha = 1f;
            overlayGroup.interactable = true;
            overlayGroup.blocksRaycasts = true;
            entranceRoutine = null;
        }

        private IEnumerator AnimateIconPulse()
        {
            if (iconRect == null || iconPulseAmount <= 0f)
                yield break;

            Vector3 baseScale = Vector3.one;

            while (true)
            {
                float wave = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / iconPulsePeriod);
                iconRect.localScale = baseScale * (1f + wave * iconPulseAmount);
                yield return null;
            }
        }

        private void StopEntranceAnimation()
        {
            if (entranceRoutine == null)
                return;

            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        private void StopPulse()
        {
            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
                pulseRoutine = null;
            }

            if (iconRect != null)
                iconRect.localScale = Vector3.one;
        }

        private void RefreshValues()
        {
            // Cada modo lleva su propio marcador; se muestra el que exista.
            int score = scoreManager != null
                ? scoreManager.Score
                : match3Score != null ? match3Score.Score : 0;
            int lines = scoreManager != null ? scoreManager.TotalLines : 0;
            int level = difficulty != null
                ? difficulty.Level
                : match3Difficulty != null ? match3Difficulty.Level : 1;

            if (scoreValueLabel != null)
                scoreValueLabel.text = score.ToString("N0");

            if (linesValueLabel != null)
                linesValueLabel.text = lines.ToString("D3");

            if (levelValueLabel != null)
                levelValueLabel.text = level.ToString("D2");
        }

        // --- Tiempo y audio ---------------------------------------------

        private void HoldTimeScale()
        {
            if (timeScaleHeld)
                return;

            timeScaleHeld = true;
            Time.timeScale = 0f;
        }

        private void ReleaseTimeScale()
        {
            if (!timeScaleHeld)
                return;

            timeScaleHeld = false;
            Time.timeScale = 1f;
        }

        private void DuckMusic()
        {
            AudioManager audio = AudioManager.Instance;

            if (audio == null || previousMusicVolume >= 0f)
                return;

            previousMusicVolume = audio.MusicVolume;
            audio.MusicVolume = pausedMusicVolume;
        }

        private void RestoreMusic()
        {
            if (previousMusicVolume < 0f)
                return;

            AudioManager audio = AudioManager.Instance;

            if (audio != null)
                audio.MusicVolume = previousMusicVolume;

            previousMusicVolume = -1f;
        }

        private void PlayClick()
        {
            if (clickSfx == null)
                return;

            AudioManager audio = AudioManager.Instance;

            if (audio != null)
                audio.PlaySfx(clickSfx);
        }

        // --- Acciones de los botones ------------------------------------

        private void Resume()
        {
            if (game == null || game.State != TetrisGame.GameState.Paused)
                return;

            PlayClick();

            // TogglePause dispara StateChanged y de ahi sale el HideCard, que
            // ya devuelve el timeScale y el volumen a su sitio.
            game.TogglePause();
        }

        private void RestartGame()
        {
            if (game == null || game.State != TetrisGame.GameState.Paused)
                return;

            PlayClick();
            SetButtonsInteractable(false);
            ReleaseTimeScale();
            RestoreMusic();
            game.StartGame();
        }

        private void ReturnToMenu()
        {
            if (game != null && game.State != TetrisGame.GameState.Paused)
                return;

            PlayClick();
            SetButtonsInteractable(false);

            // Sin esto el menu se cargaria con el tiempo congelado.
            ReleaseTimeScale();
            RestoreMusic();
            SceneManager.LoadScene(menuScene);
        }

        private void SetButtonsInteractable(bool value)
        {
            if (resumeButton != null)
                resumeButton.interactable = value;

            if (restartButton != null)
                restartButton.interactable = value;

            if (menuButton != null)
                menuButton.interactable = value;
        }

        // --- Construccion de la interfaz --------------------------------

        private void CreateInterface()
        {
            if (canvasObject != null)
                return;

            roundedSprite = CreateRoundedSprite();
            cardSize = ResolveCardSize();

            canvasObject = new GameObject(
                "Pause Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.SetActive(false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform overlayRect = CreateRect("Pause Overlay", canvasObject.transform);
            Stretch(overlayRect);
            overlayObject = overlayRect.gameObject;

            Image overlay = overlayObject.AddComponent<Image>();
            overlay.color = overlayColor;
            overlay.raycastTarget = true;

            overlayGroup = overlayObject.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;

            safeAreaRect = CreateRect("Safe Area", overlayRect);
            Stretch(safeAreaRect);

            CreateCard(safeAreaRect);
            EnsureEventSystem();
        }

        private Vector2 ResolveCardSize()
        {
            if (cardSprite == null || cardSprite.rect.height <= 0f)
                return fallbackCardSize;

            float aspect = cardSprite.rect.width / cardSprite.rect.height;
            return new Vector2(cardHeight * aspect, cardHeight);
        }

        private void CreateCard(Transform parent)
        {
            cardRect = CreateRect("Pause Card", parent);
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = cardSize;

            RectTransform shadowRect = CreateRect("Card Shadow", cardRect);
            Stretch(shadowRect);
            shadowRect.offsetMin = new Vector2(-18f, -26f);
            shadowRect.offsetMax = new Vector2(18f, 6f);
            Image shadow = AddPanelImage(shadowRect.gameObject, new Color(0f, 0f, 0f, 0.55f));
            shadow.raycastTarget = false;

            RectTransform panelRect = CreateRect("Card Background", cardRect);
            Stretch(panelRect);
            Image panel = panelRect.gameObject.AddComponent<Image>();
            panel.raycastTarget = false;

            if (cardSprite != null)
            {
                panel.sprite = cardSprite;
                panel.color = Color.white;
                panel.preserveAspect = true;
            }
            else
            {
                panel.sprite = roundedSprite;
                panel.type = Image.Type.Sliced;
                panel.color = cardFallbackColor;
            }

            // Todo el contenido vive dentro del marco dibujado en el arte.
            RectTransform contentRect = CreateRect("Content", panelRect);
            contentRect.anchorMin = new Vector2(contentInset.x, contentInset.y);
            contentRect.anchorMax = new Vector2(1f - contentInset.x, 1f - contentInset.y);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            CreateHeader(contentRect);
            CreateStatsRow(contentRect);
            CreateButtonsRow(contentRect);

            CreateText(
                contentRect,
                "Keyboard Hint",
                "Esc o P para continuar",
                20,
                new Color(1f, 0.94f, 0.82f, 0.85f),
                new Vector2(0.5f, 0.02f),
                new Vector2(0.5f, 0.02f),
                new Vector2(0.5f, 0.5f),
                new Vector2(340f, 30f));
        }

        private void CreateHeader(RectTransform parent)
        {
            RectTransform headerRect = CreateRect("Header", parent);
            headerRect.anchorMin = new Vector2(0.5f, 0.86f);
            headerRect.anchorMax = new Vector2(0.5f, 0.86f);
            headerRect.pivot = new Vector2(0.5f, 0.5f);
            headerRect.sizeDelta = new Vector2(cardSize.x * 0.7f, 96f);

            iconRect = CreateRect("Pause Icon", headerRect);
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(48f, 0f);
            iconRect.sizeDelta = new Vector2(72f, 72f);

            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            if (iconSprite != null)
            {
                icon.sprite = iconSprite;
            }
            else
            {
                // Sin arte, dos barras hacen de simbolo de pausa.
                icon.enabled = false;
                CreateBar(iconRect, -16f);
                CreateBar(iconRect, 16f);
            }

            Text title = CreateText(
                headerRect,
                "Title",
                "PAUSA",
                62,
                titleColor,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(cardSize.x * 0.55f, 88f));
            title.rectTransform.anchoredPosition = new Vector2(28f, 0f);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 28;
            title.resizeTextMaxSize = 62;
        }

        private void CreateBar(RectTransform parent, float offsetX)
        {
            RectTransform barRect = CreateRect("Bar", parent);
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.anchoredPosition = new Vector2(offsetX, 0f);
            barRect.sizeDelta = new Vector2(20f, 64f);

            Image bar = AddPanelImage(barRect.gameObject, titleColor);
            bar.raycastTarget = false;
        }

        private void CreateStatsRow(RectTransform parent)
        {
            scoreValueLabel = CreateStatChip(parent, "Puntos", "PUNTOS", 0.18f, scoreColor);
            linesValueLabel = CreateStatChip(parent, "Lineas", "LINEAS", 0.5f, linesColor);
            levelValueLabel = CreateStatChip(parent, "Nivel", "NIVEL", 0.82f, levelColor);
        }

        private Text CreateStatChip(
            RectTransform parent,
            string objectName,
            string caption,
            float horizontalAnchor,
            Color valueColor)
        {
            RectTransform chipRect = CreateRect(objectName, parent);
            chipRect.anchorMin = new Vector2(horizontalAnchor, 0.52f);
            chipRect.anchorMax = new Vector2(horizontalAnchor, 0.52f);
            chipRect.pivot = new Vector2(0.5f, 0.5f);
            chipRect.sizeDelta = new Vector2(cardSize.x * 0.26f, 130f);

            Image chip = AddPanelImage(chipRect.gameObject, chipColor);
            chip.raycastTarget = false;

            Outline outline = chipRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(valueColor.r, valueColor.g, valueColor.b, 0.5f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = true;

            CreateText(
                chipRect,
                "Caption",
                caption,
                22,
                new Color(1f, 0.94f, 0.82f, 0.9f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(200f, 34f)).rectTransform.anchoredPosition = new Vector2(0f, -14f);

            Text value = CreateText(
                chipRect,
                "Value",
                "0",
                46,
                valueColor,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(200f, 66f));
            value.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 18;
            value.resizeTextMaxSize = 46;

            return value;
        }

        private void CreateButtonsRow(RectTransform parent)
        {
            resumeButton = CreateActionButton(
                parent,
                "Resume Button",
                "CONTINUAR",
                0.18f,
                resumeButtonColor,
                Resume);

            restartButton = CreateActionButton(
                parent,
                "Restart Button",
                "REINICIAR",
                0.5f,
                restartButtonColor,
                RestartGame);

            menuButton = CreateActionButton(
                parent,
                "Menu Button",
                "MENU",
                0.82f,
                menuButtonColor,
                ReturnToMenu);
        }

        private Button CreateActionButton(
            RectTransform parent,
            string objectName,
            string labelText,
            float horizontalAnchor,
            Color color,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform buttonRect = CreateRect(objectName, parent);
            buttonRect.anchorMin = new Vector2(horizontalAnchor, 0.16f);
            buttonRect.anchorMax = new Vector2(horizontalAnchor, 0.16f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(cardSize.x * 0.26f, 78f);

            // El ColorBlock del Button aplica el tinte; la imagen base debe ser
            // blanca para que el color configurado no se multiplique dos veces.
            Image buttonImage = AddPanelImage(buttonRect.gameObject, Color.white);
            buttonImage.raycastTarget = true;

            Shadow shadow = buttonRect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(0f, -6f);
            shadow.useGraphicAlpha = true;

            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.22f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            Text label = CreateText(
                buttonRect,
                "Label",
                labelText,
                24,
                Color.white,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(cardSize.x * 0.24f, 62f));
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 24;

            return button;
        }

        private Image AddPanelImage(GameObject target, Color color)
        {
            Image image = target.AddComponent<Image>();
            image.color = color;

            if (roundedSprite != null)
            {
                image.sprite = roundedSprite;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        private Sprite CreateRoundedSprite()
        {
            const int textureSize = 64;
            const float cornerRadius = 14f;
            const float spriteBorder = 16f;

            roundedTexture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Pause Rounded UI Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[textureSize * textureSize];
            float halfSize = textureSize * 0.5f;
            float innerExtent = halfSize - cornerRadius;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float localX = Mathf.Abs(x + 0.5f - halfSize) - innerExtent;
                    float localY = Mathf.Abs(y + 0.5f - halfSize) - innerExtent;
                    float outsideX = Mathf.Max(localX, 0f);
                    float outsideY = Mathf.Max(localY, 0f);
                    float distance = Mathf.Sqrt(
                        outsideX * outsideX + outsideY * outsideY) +
                        Mathf.Min(Mathf.Max(localX, localY), 0f) -
                        cornerRadius;
                    byte alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(0.5f - distance) * 255f);

                    pixels[y * textureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            roundedTexture.SetPixels32(pixels);
            roundedTexture.Apply(false, true);

            Sprite sprite = Sprite.Create(
                roundedTexture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(spriteBorder, spriteBorder, spriteBorder, spriteBorder));
            sprite.name = "Pause Rounded UI Sprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            string content,
            int fontSize,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Text),
                typeof(Shadow));
            textObject.transform.SetParent(parent, false);

            Text label = textObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = content;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;

            Shadow shadow = textObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            return label;
        }

        private void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current;

            if (existing == null)
                existing = FindAnyObjectByType<EventSystem>();

            if (existing != null)
                return;

            createdEventSystemObject = new GameObject("Pause UI EventSystem");
            createdEventSystemObject.SetActive(false);
            createdEventSystemObject.AddComponent<EventSystem>();
            InputSystemUIInputModule inputModule =
                createdEventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
            createdEventSystemObject.SetActive(true);
        }

        private void RefreshLayout(bool force)
        {
            if (safeAreaRect == null || cardRect == null ||
                Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

            if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize)
                return;

            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
            safeAreaRect.anchorMin = new Vector2(
                safeArea.xMin / Screen.width,
                safeArea.yMin / Screen.height);
            safeAreaRect.anchorMax = new Vector2(
                safeArea.xMax / Screen.width,
                safeArea.yMax / Screen.height);
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;

            Canvas.ForceUpdateCanvases();

            float availableWidth = Mathf.Max(1f, safeAreaRect.rect.width - safeAreaMargin * 2f);
            float availableHeight = Mathf.Max(1f, safeAreaRect.rect.height - safeAreaMargin * 2f);
            cardScale = Mathf.Min(
                1f,
                availableWidth / Mathf.Max(1f, cardSize.x),
                availableHeight / Mathf.Max(1f, cardSize.y));
            cardRect.sizeDelta = cardSize;

            // Mientras entra, la animacion es la dueña de la escala.
            if (entranceRoutine == null)
                cardRect.localScale = Vector3.one * cardScale;
        }

        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.70158f;
            float shifted = value - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted +
                   overshoot * shifted * shifted;
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
    }
}
