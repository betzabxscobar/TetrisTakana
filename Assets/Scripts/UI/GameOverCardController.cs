using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Crea y presenta una tarjeta modal de fin de partida. La interfaz se
    /// construye en tiempo de ejecucion para mantener la escena del juego limpia.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverCardController : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private BoardGame game;
        [SerializeField] private ScoreManager scoreManager;
        [Header("Datos (modo match-3)")]
        [SerializeField] private Match3.ScoreManager match3Score;
        [SerializeField] private string menuScene = "Menu";

        [Header("Diseno")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private Vector2 cardSize = new Vector2(650f, 520f);
        [SerializeField, Min(0f)] private float safeAreaMargin = 48f;
        [SerializeField] private int sortingOrder = 200;

        [Header("Animacion")]
        [SerializeField, Min(0.05f)] private float entranceDuration = 0.55f;
        [SerializeField, Min(0f)] private float hiddenExtraDistance = 60f;
        [Tooltip("Espera antes de cubrir la escena, para dejar visible la reaccion de derrota.")]
        [SerializeField, Min(0f)] private float defeatReactionDelay = 0.8f;

        [Header("Colores")]
        [SerializeField] private Color overlayColor = new Color(0.01f, 0.015f, 0.04f, 0.76f);
        [SerializeField] private Color cardColor = new Color(0.035f, 0.045f, 0.11f, 0.98f);
        [SerializeField] private Color cardInnerColor = new Color(0.055f, 0.07f, 0.15f, 0.98f);
        [SerializeField] private Color accentColor = new Color(0.15f, 0.78f, 1f, 1f);
        [SerializeField] private Color buttonColor = new Color(0.50f, 0.18f, 0.92f, 1f);

        [Header("Sprites del juego")]
        [SerializeField] private Sprite cardPanelSprite;
        [SerializeField] private Sprite replayButtonSprite;
        [SerializeField] private Sprite menuButtonSprite;
        [SerializeField] private Font pixelFont;

        private GameObject canvasObject;
        private GameObject overlayObject;
        private GameObject createdEventSystemObject;
        private RectTransform safeAreaRect;
        private RectTransform cardRect;
        private CanvasGroup overlayGroup;
        private Text finalScoreLabel;
        private Button replayButton;
        private Button menuButton;
        private Coroutine entranceRoutine;
        private Coroutine showRoutine;
        private Sprite roundedSprite;
        private Texture2D roundedTexture;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private float cardScale = 1f;
        private bool subscribed;

        /// <summary>Busca la partida y monta la tarjeta de fin de juego.</summary>
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

        /// <summary>Suelta los botones y destruye lo que creo este componente.</summary>
        private void OnDestroy()
        {
            if (replayButton != null)
                replayButton.onClick.RemoveListener(RestartGame);

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
        public void Configure(BoardGame targetGame, ScoreManager targetScoreManager)
        {
            if (subscribed)
                Unsubscribe();

            game = targetGame != null ? targetGame : game;
            scoreManager = targetScoreManager != null
                ? targetScoreManager
                : (game as TetrisGame) != null ? ((TetrisGame)game).Score : scoreManager;

            if (isActiveAndEnabled)
            {
                Subscribe();
                HandleStateChanged(game != null
                    ? game.State
                    : TetrisGame.GameState.Ready);
            }
        }

        /// <summary>Busca la partida y el marcador de cada modo si no vienen asignados.</summary>
        private void ResolveReferences()
        {
            game ??= FindAnyObjectByType<BoardGame>();
            scoreManager ??= (game as TetrisGame) != null ? ((TetrisGame)game).Score : FindAnyObjectByType<ScoreManager>();
            match3Score ??= (game as Match3.Match3Game) != null
                ? ((Match3.Match3Game)game).Score
                : FindAnyObjectByType<Match3.ScoreManager>();
        }

        /// <summary>
        /// Puntos de la partida que se acaba de perder. Preguntar es el modo
        /// que se juega, no lo primero que aparezca en la escena: los
        /// marcadores del Tetris siguen ahi, desactivados, y devolvian un cero
        /// que se comia la puntuacion del match-3.
        /// </summary>
        private int CurrentScore()
        {
            if (game is Match3.Match3Game)
                return match3Score != null ? match3Score.Score : 0;

            if (scoreManager != null)
                return scoreManager.Score;

            return match3Score != null ? match3Score.Score : 0;
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

        /// <summary>Enseña la tarjeta al perder y la esconde en cuanto se vuelve a jugar.</summary>
        private void HandleStateChanged(TetrisGame.GameState state)
        {
            if (state == TetrisGame.GameState.GameOver)
                ShowCard();
            else
                HideCard();
        }

        /// <summary>Saca la tarjeta con su animacion de entrada.</summary>
        private void ShowCard()
        {
            if (overlayObject == null || cardRect == null || overlayGroup == null)
                return;

            StopShowDelay();
            StopEntranceAnimation();

            if (finalScoreLabel != null)
                finalScoreLabel.text = CurrentScore().ToString("N0");

            if (replayButton != null)
                replayButton.interactable = true;

            if (menuButton != null)
                menuButton.interactable = true;

            overlayObject.SetActive(false);

            if (defeatReactionDelay > 0f)
            {
                showRoutine = StartCoroutine(ShowCardAfterDefeat());
                return;
            }

            BeginEntrance();
        }

        private IEnumerator ShowCardAfterDefeat()
        {
            yield return new WaitForSecondsRealtime(defeatReactionDelay);
            showRoutine = null;

            if (game != null && game.State == TetrisGame.GameState.GameOver)
                BeginEntrance();
        }

        private void BeginEntrance()
        {
            overlayObject.SetActive(true);
            RefreshLayout(true);
            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = true;
            cardRect.anchoredPosition = GetHiddenPosition();
            entranceRoutine = StartCoroutine(AnimateEntrance());
        }

        /// <summary>Anima la tarjeta desde fuera de pantalla hasta el centro.</summary>
        private IEnumerator AnimateEntrance()
        {
            Vector2 start = GetHiddenPosition();
            Vector2 destination = Vector2.zero;
            float elapsed = 0f;

            while (elapsed < entranceDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / entranceDuration);
                float easedPosition = EaseOutBack(progress);
                float easedAlpha = 1f - Mathf.Pow(1f - progress, 3f);

                cardRect.anchoredPosition = Vector2.LerpUnclamped(
                    start,
                    destination,
                    easedPosition);
                overlayGroup.alpha = easedAlpha;
                yield return null;
            }

            cardRect.anchoredPosition = destination;
            overlayGroup.alpha = 1f;
            overlayGroup.interactable = true;
            overlayGroup.blocksRaycasts = true;
            entranceRoutine = null;
        }

        /// <summary>Esconde la tarjeta y corta su animacion.</summary>
        private void HideCard()
        {
            StopShowDelay();
            StopEntranceAnimation();

            if (overlayGroup != null)
            {
                overlayGroup.alpha = 0f;
                overlayGroup.interactable = false;
                overlayGroup.blocksRaycasts = false;
            }

            if (EventSystem.current != null &&
                (EventSystem.current.currentSelectedGameObject == replayButton?.gameObject ||
                 EventSystem.current.currentSelectedGameObject == menuButton?.gameObject))
                EventSystem.current.SetSelectedGameObject(null);

            if (overlayObject != null)
                overlayObject.SetActive(false);
        }

        private void StopShowDelay()
        {
            if (showRoutine == null)
                return;

            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        /// <summary>Corta la animacion de entrada si estaba a medias.</summary>
        private void StopEntranceAnimation()
        {
            if (entranceRoutine == null)
                return;

            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        /// <summary>Empieza una partida nueva desde la tarjeta.</summary>
        private void RestartGame()
        {
            if (game == null || game.State != TetrisGame.GameState.GameOver)
                return;

            if (replayButton != null)
                replayButton.interactable = false;

            if (menuButton != null)
                menuButton.interactable = false;

            game.StartGame();
        }

        /// <summary>Vuelve al menu principal.</summary>
        private void ReturnToMenu()
        {
            if (game != null && game.State != TetrisGame.GameState.GameOver)
                return;

            if (replayButton != null)
                replayButton.interactable = false;

            if (menuButton != null)
                menuButton.interactable = false;

            SceneManager.LoadScene(menuScene);
        }

        /// <summary>Monta el canvas, el fondo oscuro y la tarjeta.</summary>
        private void CreateInterface()
        {
            if (canvasObject != null)
                return;

            roundedSprite = CreateRoundedSprite();

            canvasObject = new GameObject(
                "Game Over Canvas",
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

            RectTransform overlayRect = CreateRect("Game Over Overlay", canvasObject.transform);
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

        /// <summary>Crea la tarjeta con su titulo, su puntuacion y sus botones.</summary>
        private void CreateCard(Transform parent)
        {
            cardRect = CreateRect("Game Over Card", parent);
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = cardSize;

            RectTransform shadowRect = CreateRect("Card Shadow", cardRect);
            Stretch(shadowRect);
            shadowRect.offsetMin = new Vector2(-16f, -24f);
            shadowRect.offsetMax = new Vector2(16f, 8f);
            Image shadow = AddPanelImage(shadowRect.gameObject, new Color(0f, 0f, 0f, 0.62f));
            shadow.raycastTarget = false;

            RectTransform panelRect = CreateRect("Card Background", cardRect);
            Stretch(panelRect);
            Image panel = AddSpritePanel(panelRect.gameObject, cardPanelSprite, cardColor);
            panel.raycastTarget = false;

            Outline outline = panelRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.82f);
            outline.effectDistance = new Vector2(4f, -4f);
            outline.useGraphicAlpha = true;

            RectTransform innerRect = CreateRect("Card Interior", panelRect);
            Stretch(innerRect);
            innerRect.offsetMin = new Vector2(13f, 13f);
            innerRect.offsetMax = new Vector2(-13f, -13f);
            Image inner = AddPanelImage(innerRect.gameObject, cardInnerColor);
            inner.raycastTarget = false;
            inner.color = new Color(cardInnerColor.r, cardInnerColor.g, cardInnerColor.b, 0.86f);

            RectTransform accentRect = CreateRect("Top Accent", innerRect);
            accentRect.anchorMin = new Vector2(0.5f, 1f);
            accentRect.anchorMax = new Vector2(0.5f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = new Vector2(0f, -20f);
            accentRect.sizeDelta = new Vector2(500f, 8f);
            Image accent = AddPanelImage(accentRect.gameObject, accentColor);
            accent.raycastTarget = false;

            Text title = CreateText(
                innerRect,
                "Title",
                "FIN DEL JUEGO",
                52,
                Color.white,
                new Vector2(0f, 168f),
                new Vector2(560f, 72f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 28;
            title.resizeTextMaxSize = 52;

            CreateText(
                innerRect,
                "Score Caption",
                "PUNTAJE FINAL",
                26,
                new Color(0.72f, 0.78f, 0.9f, 1f),
                new Vector2(0f, 91f),
                new Vector2(420f, 42f));

            finalScoreLabel = CreateText(
                innerRect,
                "Final Score",
                "0",
                82,
                accentColor,
                new Vector2(0f, 17f),
                new Vector2(540f, 100f));
            finalScoreLabel.resizeTextForBestFit = true;
            finalScoreLabel.resizeTextMinSize = 38;
            finalScoreLabel.resizeTextMaxSize = 82;

            RectTransform dividerRect = CreateRect("Divider", innerRect);
            dividerRect.anchorMin = new Vector2(0.5f, 0.5f);
            dividerRect.anchorMax = new Vector2(0.5f, 0.5f);
            dividerRect.pivot = new Vector2(0.5f, 0.5f);
            dividerRect.anchoredPosition = new Vector2(0f, -51f);
            dividerRect.sizeDelta = new Vector2(455f, 3f);
            Image divider = dividerRect.gameObject.AddComponent<Image>();
            divider.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.4f);
            divider.raycastTarget = false;

            replayButton = CreateActionButton(
                innerRect,
                "Replay Button",
                replayButtonSprite,
                "JUGAR DE NUEVO",
                new Vector2(-150f, -151f),
                RestartGame);

            menuButton = CreateActionButton(
                innerRect,
                "Menu Button",
                menuButtonSprite,
                "VOLVER AL MENU",
                new Vector2(150f, -151f),
                ReturnToMenu);
        }

        /// <summary>Crea un boton de la tarjeta con su texto y su color.</summary>
        private Button CreateActionButton(
            Transform parent,
            string objectName,
            Sprite buttonSprite,
            string labelText,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform buttonRect = CreateRect(objectName, parent);
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = new Vector2(270f, 70f);

            // El ColorBlock del Button aplica el tinte; la imagen base debe ser
            // blanca para que el color configurado no se multiplique dos veces.
            Image buttonImage = AddSpritePanel(buttonRect.gameObject, buttonSprite, Color.white);
            buttonImage.preserveAspect = buttonSprite != null;
            buttonImage.raycastTarget = true;

            Shadow shadow = buttonRect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(0f, -7f);
            shadow.useGraphicAlpha = true;

            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ColorBlock colors = button.colors;
            colors.normalColor = buttonSprite != null ? Color.white : buttonColor;
            colors.highlightedColor = buttonSprite != null
                ? new Color(0.8f, 0.95f, 1f, 1f)
                : Color.Lerp(buttonColor, Color.white, 0.18f);
            colors.pressedColor = buttonSprite != null
                ? new Color(0.65f, 0.72f, 0.9f, 1f)
                : Color.Lerp(buttonColor, Color.black, 0.22f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            // Los sprites de los botones ya incluyen su rotulo. Agregar un
            // Text encima los duplicaba y producia el texto sobreescrito.
            // Solo se crea el rotulo para el boton redondeado de respaldo.
            if (buttonSprite == null)
            {
                Text label = CreateText(
                    buttonRect,
                    "Label",
                    labelText,
                    16,
                    Color.white,
                    new Vector2(0f, 1f),
                    new Vector2(252f, 52f));
                label.resizeTextForBestFit = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
            }

            return button;
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

        /// <summary>Usa el arte existente y conserva el panel generado como respaldo.</summary>
        private Image AddSpritePanel(GameObject target, Sprite sprite, Color fallbackColor)
        {
            Image image = AddPanelImage(target, fallbackColor);
            if (sprite == null)
                return image;

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
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
                name = "Runtime Rounded UI Texture",
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
            sprite.name = "Runtime Rounded UI Sprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>Crea un texto con fuente, tamaño y color indicados.</summary>
        private Text CreateText(
            Transform parent,
            string objectName,
            string content,
            int fontSize,
            Color color,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Text),
                typeof(Shadow));
            textObject.transform.SetParent(parent, false);

            Text label = textObject.GetComponent<Text>();
            label.font = pixelFont != null
                ? pixelFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = content;
            label.fontSize = fontSize;
            label.fontStyle = pixelFont != null ? FontStyle.Normal : FontStyle.Bold;
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
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
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

            createdEventSystemObject = new GameObject("Game UI EventSystem");
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
            cardRect.localScale = Vector3.one * cardScale;
        }

        /// <summary>Punto de fuera de pantalla del que sale la tarjeta.</summary>
        private Vector2 GetHiddenPosition()
        {
            float safeHeight = safeAreaRect != null ? safeAreaRect.rect.height : referenceResolution.y;
            float scaledCardHeight = cardSize.y * cardScale;
            return new Vector2(
                0f,
                -(safeHeight + scaledCardHeight) * 0.5f - hiddenExtraDistance);
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
