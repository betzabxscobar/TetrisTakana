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
        [SerializeField] private Sprite scoreCaptionSprite;
        [SerializeField] private Sprite linesCaptionSprite;
        [SerializeField] private Sprite levelCaptionSprite;
        [Tooltip("Marcos decorativos para los tres indicadores de la pausa.")]
        [SerializeField] private Sprite scorePanelSprite;
        [SerializeField] private Sprite linesPanelSprite;
        [SerializeField] private Sprite levelPanelSprite;
        [SerializeField] private Sprite restartButtonSprite;
        [SerializeField] private Sprite menuButtonSprite;
        [SerializeField] private Sprite closeButtonSprite;
        [SerializeField] private Sprite helpButtonSprite;
        [SerializeField] private Sprite helpPanelSprite;
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

        // La paleta es la misma que la de GameOverCardController, para que la
        // pausa y el fin de partida se lean como la misma tarjeta.
        [Header("Colores")]
        [SerializeField] private Color overlayColor = new Color(0.01f, 0.015f, 0.04f, 0.76f);
        [SerializeField] private Color cardFallbackColor = new Color(0.035f, 0.045f, 0.11f, 0.98f);
        [SerializeField] private Color cardInnerColor = new Color(0.055f, 0.07f, 0.15f, 0.98f);
        [SerializeField] private Color accentColor = new Color(0.15f, 0.78f, 1f, 1f);
        [SerializeField] private Color titleColor = Color.white;
        [SerializeField] private Color captionColor = new Color(0.72f, 0.78f, 0.9f, 1f);
        [SerializeField] private Color chipColor = new Color(0.02f, 0.03f, 0.08f, 0.55f);
        [SerializeField] private Color scoreColor = new Color(0.15f, 0.78f, 1f, 1f);
        [SerializeField] private Color linesColor = new Color(0.282f, 0.655f, 0.161f, 1f);
        [SerializeField] private Color levelColor = new Color(0.984f, 0.404f, 0f, 1f);
        [SerializeField] private Color resumeButtonColor = new Color(0.20f, 0.62f, 0.28f, 1f);
        [SerializeField] private Color restartButtonColor = new Color(0.50f, 0.18f, 0.92f, 1f);
        [SerializeField] private Color menuButtonColor = new Color(0.50f, 0.18f, 0.92f, 1f);

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
        private Button closeButton;
        private Button helpButton;
        private GameObject helpPanelObject;
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

        /// <summary>Busca la partida y monta la tarjeta de pausa.</summary>
        private void Awake()
        {
            ResolveReferences();
            CreateInterface();
        }

        /// <summary>Enciende la tarjeta y se suscribe al estado de la partida.</summary>
        private void OnEnable()
        {
            if (canvasObject != null)
                canvasObject.SetActive(true);

            Subscribe();
            RefreshLayout(true);
            HandleStateChanged(game != null ? game.State : TetrisGame.GameState.Ready);
        }

        /// <summary>Reajusta la tarjeta cuando cambia la pantalla.</summary>
        private void LateUpdate()
        {
            RefreshLayout(false);
        }

        /// <summary>Se da de baja de los eventos y esconde la tarjeta.</summary>
        private void OnDisable()
        {
            Unsubscribe();
            HideCard();

            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        /// <summary>Suelta la pausa antes de morir para no dejar el juego congelado.</summary>
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

            if (closeButton != null)
                closeButton.onClick.RemoveListener(Resume);

            if (helpButton != null)
                helpButton.onClick.RemoveListener(ShowHelp);

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

        /// <summary>Busca partida, marcador y dificultad de cualquiera de los dos modos.</summary>
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

        /// <summary>Se pone a escuchar los cambios de estado de la partida.</summary>
        private void Subscribe()
        {
            if (subscribed || game == null)
                return;

            game.StateChanged += HandleStateChanged;
            subscribed = true;
        }

        /// <summary>Deja de escuchar los cambios de estado.</summary>
        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (game != null)
                game.StateChanged -= HandleStateChanged;

            subscribed = false;
        }

        /// <summary>Saca la tarjeta al pausar y la quita al reanudar.</summary>
        private void HandleStateChanged(TetrisGame.GameState state)
        {
            if (state == TetrisGame.GameState.Paused)
                ShowCard();
            else
                HideCard();
        }

        // --- Mostrar y ocultar ------------------------------------------

        /// <summary>Enseña la tarjeta, congela el tiempo y baja la musica.</summary>
        private void ShowCard()
        {
            if (overlayObject == null || cardRect == null || overlayGroup == null)
                return;

            StopEntranceAnimation();
            StopPulse();
            overlayObject.SetActive(true);
            cardRect.gameObject.SetActive(true);

            if (helpPanelObject != null)
                helpPanelObject.SetActive(false);

            RefreshLayout(true);
            RefreshValues();

            if (resumeButton != null)
                resumeButton.interactable = true;

            if (restartButton != null)
                restartButton.interactable = true;

            if (menuButton != null)
                menuButton.interactable = true;

            if (closeButton != null)
                closeButton.interactable = true;

            if (helpButton != null)
                helpButton.interactable = true;

            HoldTimeScale();
            DuckMusic();

            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = true;
            entranceRoutine = StartCoroutine(AnimateEntrance());
            pulseRoutine = StartCoroutine(AnimateIconPulse());
        }

        /// <summary>Esconde la tarjeta, devuelve el tiempo y sube la musica.</summary>
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

            if (helpPanelObject != null)
                helpPanelObject.SetActive(false);
        }

        /// <summary>Dice si lo seleccionado ahora es uno de los botones de esta tarjeta.</summary>
        private bool IsOwnSelection()
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;

            return selected != null &&
                   (selected == resumeButton?.gameObject ||
                    selected == restartButton?.gameObject ||
                    selected == menuButton?.gameObject ||
                    selected == closeButton?.gameObject);
        }

        /// <summary>Anima la entrada de la tarjeta con su rebote.</summary>
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

        /// <summary>Hace latir el icono mientras la tarjeta esta en pantalla.</summary>
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

        /// <summary>Corta la animacion de entrada si estaba a medias.</summary>
        private void StopEntranceAnimation()
        {
            if (entranceRoutine == null)
                return;

            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        /// <summary>Corta el latido del icono.</summary>
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

        /// <summary>Escribe en la tarjeta la puntuacion, las lineas y el nivel.</summary>
        private void RefreshValues()
        {
            if (scoreValueLabel != null)
                scoreValueLabel.text = CurrentScore().ToString("N0");

            if (linesValueLabel != null)
                linesValueLabel.text = CurrentLines().ToString("D3");

            if (levelValueLabel != null)
                levelValueLabel.text = CurrentLevel().ToString("D2");
        }

        /// <summary>
        /// Puntos de la partida en curso. Quien manda es el modo que se juega,
        /// no lo primero que aparezca en la escena: los marcadores del Tetris
        /// siguen ahi, desactivados, y contestaban antes que los del match-3
        /// dejando la cifra clavada en cero.
        /// </summary>
        private int CurrentScore()
        {
            if (game is Match3.Match3Game)
                return match3Score != null ? match3Score.Score : 0;

            if (scoreManager != null)
                return scoreManager.Score;

            return match3Score != null ? match3Score.Score : 0;
        }

        /// <summary>Nivel actual, del sistema de dificultad del modo que se juega.</summary>
        private int CurrentLevel()
        {
            if (game is Match3.Match3Game)
                return match3Difficulty != null ? match3Difficulty.Level : 1;

            if (difficulty != null)
                return difficulty.Level;

            return match3Difficulty != null ? match3Difficulty.Level : 1;
        }

        /// <summary>Lineas hechas. El match-3 no cuenta lineas, asi que ahi va cero.</summary>
        private int CurrentLines()
        {
            return game is Match3.Match3Game || scoreManager == null
                ? 0
                : scoreManager.TotalLines;
        }

        // --- Tiempo y audio ---------------------------------------------

        /// <summary>Congela el tiempo del juego mientras dura la pausa.</summary>
        private void HoldTimeScale()
        {
            if (timeScaleHeld)
                return;

            timeScaleHeld = true;
            Time.timeScale = 0f;
        }

        /// <summary>Devuelve el tiempo del juego a su marcha normal.</summary>
        private void ReleaseTimeScale()
        {
            if (!timeScaleHeld)
                return;

            timeScaleHeld = false;
            Time.timeScale = 1f;
        }

        /// <summary>Baja el volumen de la musica mientras la tarjeta esta delante.</summary>
        private void DuckMusic()
        {
            AudioManager audio = AudioManager.Instance;

            if (audio == null || previousMusicVolume >= 0f)
                return;

            previousMusicVolume = audio.MusicVolume;
            audio.MusicVolume = pausedMusicVolume;
        }

        /// <summary>Devuelve la musica a su volumen de antes.</summary>
        private void RestoreMusic()
        {
            if (previousMusicVolume < 0f)
                return;

            AudioManager audio = AudioManager.Instance;

            if (audio != null)
                audio.MusicVolume = previousMusicVolume;

            previousMusicVolume = -1f;
        }

        /// <summary>Suena el clic de los botones.</summary>
        private void PlayClick()
        {
            if (clickSfx == null)
                return;

            AudioManager audio = AudioManager.Instance;

            if (audio != null)
                audio.PlaySfx(clickSfx);
        }

        // --- Acciones de los botones ------------------------------------

        /// <summary>Reanuda la partida.</summary>
        private void Resume()
        {
            if (game == null || game.State != TetrisGame.GameState.Paused)
                return;

            PlayClick();

            // TogglePause dispara StateChanged y de ahi sale el HideCard, que
            // ya devuelve el timeScale y el volumen a su sitio.
            game.TogglePause();
        }

        /// <summary>Empieza una partida nueva desde la pausa.</summary>
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

        /// <summary>Vuelve al menu principal.</summary>
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

        /// <summary>Enciende o apaga los botones de la tarjeta.</summary>
        private void SetButtonsInteractable(bool value)
        {
            if (resumeButton != null)
                resumeButton.interactable = value;

            if (restartButton != null)
                restartButton.interactable = value;

            if (menuButton != null)
                menuButton.interactable = value;

            if (closeButton != null)
                closeButton.interactable = value;

            if (helpButton != null)
                helpButton.interactable = value;
        }

        // --- Construccion de la interfaz --------------------------------

        /// <summary>Monta el canvas, el fondo oscuro y la tarjeta.</summary>
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

        /// <summary>Tamaño de la tarjeta segun la proporcion de su arte.</summary>
        private Vector2 ResolveCardSize()
        {
            if (cardSprite == null || cardSprite.rect.height <= 0f)
                return fallbackCardSize;

            float aspect = cardSprite.rect.width / cardSprite.rect.height;
            return new Vector2(cardHeight * aspect, cardHeight);
        }

        /// <summary>Crea la tarjeta con su cabecera, sus cifras y sus botones.</summary>
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

            // Con arte, el marco lo dibuja el propio sprite; sin el, se monta
            // el mismo panel de dos capas y borde de acento que la tarjeta de
            // fin de partida, para que las dos se lean igual.
            RectTransform frameRect = panelRect;

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

                Outline outline = panelRect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(
                    accentColor.r,
                    accentColor.g,
                    accentColor.b,
                    0.82f);
                outline.effectDistance = new Vector2(4f, -4f);
                outline.useGraphicAlpha = true;

                RectTransform innerRect = CreateRect("Card Interior", panelRect);
                Stretch(innerRect);
                innerRect.offsetMin = new Vector2(13f, 13f);
                innerRect.offsetMax = new Vector2(-13f, -13f);
                Image inner = AddPanelImage(innerRect.gameObject, cardInnerColor);
                inner.raycastTarget = false;
                frameRect = innerRect;
            }

            // Todo el contenido vive dentro del marco.
            RectTransform contentRect = CreateRect("Content", frameRect);
            contentRect.anchorMin = new Vector2(contentInset.x, contentInset.y);
            contentRect.anchorMax = new Vector2(1f - contentInset.x, 1f - contentInset.y);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            CreateHeader(contentRect);
            CreateStatsRow(contentRect);
            CreateButtonsRow(contentRect);
            CreateCloseButton(contentRect);
            CreateHelpPanel(safeAreaRect);

            CreateText(
                contentRect,
                "Keyboard Hint",
                "Esc o P para continuar",
                20,
                new Color(captionColor.r, captionColor.g, captionColor.b, 0.9f),
                new Vector2(0.5f, 0.02f),
                new Vector2(0.5f, 0.02f),
                new Vector2(0.5f, 0.5f),
                new Vector2(340f, 30f));
        }

        /// <summary>Crea el titulo y el icono de la tarjeta.</summary>
        private void CreateHeader(RectTransform parent)
        {
            // El arte pausaTarjeta ya incorpora su propio titulo e icono.
            if (cardSprite != null)
                return;

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

        /// <summary>Crea una de las barras decorativas de la cabecera.</summary>
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

        /// <summary>Crea la fila con puntos, lineas y nivel.</summary>
        private void CreateStatsRow(RectTransform parent)
        {
            scoreValueLabel = CreateStatChip(parent, "Puntos", "PUNTOS", scoreCaptionSprite, scorePanelSprite, 0.18f, scoreColor);
            linesValueLabel = CreateStatChip(parent, "Lineas", "LINEAS", linesCaptionSprite, linesPanelSprite, 0.5f, linesColor);
            levelValueLabel = CreateStatChip(parent, "Nivel", "NIVEL", levelCaptionSprite, levelPanelSprite, 0.82f, levelColor);
        }

        /// <summary>Crea una de las cifras de la fila con su rotulo.</summary>
        private Text CreateStatChip(
            RectTransform parent,
            string objectName,
            string caption,
            Sprite captionSprite,
            Sprite panelSprite,
            float horizontalAnchor,
            Color valueColor)
        {
            RectTransform chipRect = CreateRect(objectName, parent);
            chipRect.anchorMin = new Vector2(horizontalAnchor, 0.52f);
            chipRect.anchorMax = new Vector2(horizontalAnchor, 0.52f);
            chipRect.pivot = new Vector2(0.5f, 0.5f);
            chipRect.sizeDelta = new Vector2(cardSize.x * 0.26f, 130f);

            Image chip = chipRect.gameObject.AddComponent<Image>();
            chip.sprite = panelSprite != null ? panelSprite : roundedSprite;
            chip.type = panelSprite != null ? Image.Type.Simple : Image.Type.Sliced;
            chip.preserveAspect = panelSprite != null;
            chip.color = panelSprite != null ? Color.white : chipColor;
            chip.raycastTarget = false;

            if (panelSprite == null)
            {
                Outline outline = chipRect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(valueColor.r, valueColor.g, valueColor.b, 0.5f);
                outline.effectDistance = new Vector2(3f, -3f);
                outline.useGraphicAlpha = true;
            }

            if (captionSprite != null)
            {
                RectTransform captionRect = CreateRect("Caption", chipRect);
                captionRect.anchorMin = new Vector2(0.5f, 1f);
                captionRect.anchorMax = new Vector2(0.5f, 1f);
                captionRect.pivot = new Vector2(0.5f, 1f);
                captionRect.anchoredPosition = new Vector2(0f, -8f);
                captionRect.sizeDelta = new Vector2(164f, 48f);
                Image captionImage = captionRect.gameObject.AddComponent<Image>();
                captionImage.sprite = captionSprite;
                captionImage.preserveAspect = true;
                captionImage.raycastTarget = false;
            }
            else
            {
                CreateText(chipRect, "Caption", caption, 22, captionColor,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(200f, 34f))
                    .rectTransform.anchoredPosition = new Vector2(0f, -14f);
            }

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

        /// <summary>Crea los botones de reanudar, reiniciar y menu.</summary>
        private void CreateButtonsRow(RectTransform parent)
        {
            restartButton = CreateActionButton(
                parent,
                "Restart Button",
                "REINICIAR",
                0.2f,
                restartButtonColor,
                restartButtonSprite,
                RestartGame);

            helpButton = CreateActionButton(
                parent,
                "Help Button",
                "AYUDA",
                0.5f,
                Color.white,
                helpButtonSprite,
                ShowHelp);

            menuButton = CreateActionButton(
                parent,
                "Menu Button",
                "MENU",
                0.8f,
                menuButtonColor,
                menuButtonSprite,
                ReturnToMenu);
        }

        /// <summary>Crea un boton de la tarjeta con su texto y su color.</summary>
        private Button CreateActionButton(
            RectTransform parent,
            string objectName,
            string labelText,
            float horizontalAnchor,
            Color color,
            Sprite buttonSprite,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform buttonRect = CreateRect(objectName, parent);
            buttonRect.anchorMin = new Vector2(horizontalAnchor, 0.16f);
            buttonRect.anchorMax = new Vector2(horizontalAnchor, 0.16f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(cardSize.x * 0.26f, 78f);

            // El ColorBlock del Button aplica el tinte; la imagen base debe ser
            // blanca para que el color configurado no se multiplique dos veces.
            Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.sprite = buttonSprite != null ? buttonSprite : roundedSprite;
            buttonImage.type = buttonSprite != null ? Image.Type.Simple : Image.Type.Sliced;
            buttonImage.preserveAspect = buttonSprite != null;
            buttonImage.color = Color.white;
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

            if (buttonSprite == null)
            {
                Text label = CreateText(
                    buttonRect, "Label", labelText, 24, Color.white,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(cardSize.x * 0.24f, 62f));
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 14;
                label.resizeTextMaxSize = 24;
            }

            return button;
        }

        /// <summary>La X usa el arte del proyecto y reanuda la partida.</summary>
        private void CreateCloseButton(RectTransform parent)
        {
            if (closeButtonSprite == null)
                return;

            RectTransform closeRect = CreateRect("Close Button", parent);
            closeRect.anchorMin = new Vector2(0.95f, 0.93f);
            closeRect.anchorMax = new Vector2(0.95f, 0.93f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.sizeDelta = new Vector2(44f, 44f);

            Image image = closeRect.gameObject.AddComponent<Image>();
            image.sprite = closeButtonSprite;
            image.preserveAspect = true;
            image.raycastTarget = true;

            closeButton = closeRect.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = image;
            closeButton.navigation = new Navigation { mode = Navigation.Mode.None };
            closeButton.onClick.AddListener(Resume);
        }

        /// <summary>Crea el panel de controles que ya usa la pantalla de ayuda.</summary>
        private void CreateHelpPanel(Transform parent)
        {
            if (helpPanelSprite == null)
                return;

            RectTransform panelRect = CreateRect("Controls Panel", parent);
            Stretch(panelRect);
            helpPanelObject = panelRect.gameObject;

            Image panel = helpPanelObject.AddComponent<Image>();
            panel.sprite = helpPanelSprite;
            panel.preserveAspect = true;
            panel.raycastTarget = true;

            RectTransform closeRect = CreateRect("Close Controls", panelRect);
            // El boton esta dibujado dentro de Ayuda.png, en el borde inferior.
            // El area clicable debe cubrirlo completo con la imagen a pantalla.
            closeRect.anchorMin = new Vector2(0.5f, 0.078f);
            closeRect.anchorMax = new Vector2(0.5f, 0.078f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.sizeDelta = new Vector2(440f, 106f);
            Image closeImage = closeRect.gameObject.AddComponent<Image>();
            closeImage.color = Color.clear;
            closeImage.raycastTarget = true;
            Button close = closeRect.gameObject.AddComponent<Button>();
            close.targetGraphic = closeImage;
            close.navigation = new Navigation { mode = Navigation.Mode.None };
            close.onClick.AddListener(HideHelp);

            helpPanelObject.SetActive(false);
        }

        /// <summary>Muestra los controles sin abandonar ni reanudar la partida.</summary>
        private void ShowHelp()
        {
            if (helpPanelObject == null)
                return;

            PlayClick();
            cardRect.gameObject.SetActive(false);
            helpPanelObject.SetActive(true);
        }

        /// <summary>Vuelve desde los controles a la tarjeta de pausa.</summary>
        private void HideHelp()
        {
            if (helpPanelObject == null)
                return;

            PlayClick();
            helpPanelObject.SetActive(false);
            cardRect.gameObject.SetActive(true);
        }

        /// <summary>Pone a un objeto un fondo de esquinas redondeadas.</summary>
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

        /// <summary>Dibuja por codigo el rectangulo redondeado que usan los paneles.</summary>
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

        /// <summary>Crea un texto con fuente, tamaño y color indicados.</summary>
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

        /// <summary>Se asegura de que hay un EventSystem, sin el los botones no responden.</summary>
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

        /// <summary>Reajusta la tarjeta a la zona segura y al tamaño de pantalla.</summary>
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

        /// <summary>Curva de entrada que se pasa un poco y vuelve, para que rebote.</summary>
        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.70158f;
            float shifted = value - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted +
                   overshoot * shifted * shifted;
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
    }
}
