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
        [SerializeField] private TetrisGame game;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private string menuScene = "Menu";

        [Header("Diseno")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private Vector2 cardSize = new Vector2(650f, 520f);
        [SerializeField, Min(0f)] private float safeAreaMargin = 48f;
        [SerializeField] private int sortingOrder = 200;

        [Header("Animacion")]
        [SerializeField, Min(0.05f)] private float entranceDuration = 0.55f;
        [SerializeField, Min(0f)] private float hiddenExtraDistance = 60f;

        [Header("Colores")]
        [SerializeField] private Color overlayColor = new Color(0.01f, 0.015f, 0.04f, 0.76f);
        [SerializeField] private Color cardColor = new Color(0.035f, 0.045f, 0.11f, 0.98f);
        [SerializeField] private Color cardInnerColor = new Color(0.055f, 0.07f, 0.15f, 0.98f);
        [SerializeField] private Color accentColor = new Color(0.15f, 0.78f, 1f, 1f);
        [SerializeField] private Color buttonColor = new Color(0.50f, 0.18f, 0.92f, 1f);

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
        private Sprite roundedSprite;
        private Texture2D roundedTexture;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private float cardScale = 1f;
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
        public void Configure(TetrisGame targetGame, ScoreManager targetScoreManager)
        {
            if (subscribed)
                Unsubscribe();

            game = targetGame != null ? targetGame : game;
            scoreManager = targetScoreManager != null
                ? targetScoreManager
                : game != null ? game.Score : scoreManager;

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
            game ??= FindAnyObjectByType<TetrisGame>();
            scoreManager ??= game != null ? game.Score : FindAnyObjectByType<ScoreManager>();
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
            if (state == TetrisGame.GameState.GameOver)
                ShowCard();
            else
                HideCard();
        }

        private void ShowCard()
        {
            if (overlayObject == null || cardRect == null || overlayGroup == null)
                return;

            StopEntranceAnimation();
            overlayObject.SetActive(true);
            RefreshLayout(true);

            int score = scoreManager != null ? scoreManager.Score : 0;

            if (finalScoreLabel != null)
                finalScoreLabel.text = score.ToString("N0");

            if (replayButton != null)
                replayButton.interactable = true;

            if (menuButton != null)
                menuButton.interactable = true;

            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = true;
            cardRect.anchoredPosition = GetHiddenPosition();
            entranceRoutine = StartCoroutine(AnimateEntrance());
        }

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

        private void HideCard()
        {
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

        private void StopEntranceAnimation()
        {
            if (entranceRoutine == null)
                return;

            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

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
            Image panel = AddPanelImage(panelRect.gameObject, cardColor);
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
                "VOLVER A JUGAR",
                new Vector2(-140f, -139f),
                RestartGame);

            menuButton = CreateActionButton(
                innerRect,
                "Menu Button",
                "VOLVER AL MENÚ",
                new Vector2(140f, -139f),
                ReturnToMenu);

            Text hint = CreateText(
                innerRect,
                "Keyboard Hint",
                "También puedes presionar Enter",
                20,
                new Color(0.65f, 0.7f, 0.82f, 0.92f),
                new Vector2(0f, -211f),
                new Vector2(500f, 32f));
            hint.fontStyle = FontStyle.Normal;
        }

        private Button CreateActionButton(
            Transform parent,
            string objectName,
            string labelText,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform buttonRect = CreateRect(objectName, parent);
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = new Vector2(260f, 82f);

            // El ColorBlock del Button aplica el tinte; la imagen base debe ser
            // blanca para que el color configurado no se multiplique dos veces.
            Image buttonImage = AddPanelImage(buttonRect.gameObject, Color.white);
            buttonImage.raycastTarget = true;

            Shadow shadow = buttonRect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(0f, -7f);
            shadow.useGraphicAlpha = true;

            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ColorBlock colors = button.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = Color.Lerp(buttonColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(buttonColor, Color.black, 0.22f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.45f);
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
                Vector2.zero,
                new Vector2(240f, 66f));
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 15;
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

        private static Text CreateText(
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
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
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

            createdEventSystemObject = new GameObject("Game UI EventSystem");
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
            cardRect.localScale = Vector3.one * cardScale;
        }

        private Vector2 GetHiddenPosition()
        {
            float safeHeight = safeAreaRect != null ? safeAreaRect.rect.height : referenceResolution.y;
            float scaledCardHeight = cardSize.y * cardScale;
            return new Vector2(
                0f,
                -(safeHeight + scaledCardHeight) * 0.5f - hiddenExtraDistance);
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
