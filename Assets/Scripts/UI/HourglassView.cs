using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Dibuja el reloj de arena que avisa del proximo giro del tablero. Los
    /// fotogramas se reparten por la cuenta atras: el primero recien girado y
    /// el ultimo a punto de volcar. Cuando el giro llega, el reloj da media
    /// vuelta acompañando al tablero.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HourglassView : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private BoardFlipSystem flipSystem;

        [Header("Arte")]
        [Tooltip("De mas lleno a mas vacio; el ultimo es el del volcado.")]
        [SerializeField] private Sprite[] frames;

        [Header("Diseno")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private Vector2 iconSize = new Vector2(96f, 96f);
        [Tooltip("Esquina de la zona segura donde se ancla, en tanto por uno.")]
        [SerializeField] private Vector2 anchor = new Vector2(0.5f, 1f);
        [SerializeField] private Vector2 offset = new Vector2(0f, -80f);
        [SerializeField] private int sortingOrder = 150;

        [Header("Cuenta atras")]
        [Tooltip("Segundos que faltan para el giro, en numero bajo el reloj.")]
        [SerializeField] private bool showCountdown = true;
        [SerializeField, Min(8)] private int countdownFontSize = 34;
        [Tooltip("Separacion entre el reloj y el numero, en puntos.")]
        [SerializeField, Min(0f)] private float countdownSpacing = 12f;
        [SerializeField] private Color countdownColor = new Color(1f, 0.95f, 0.82f, 1f);

        [Header("Aviso")]
        [Tooltip("Segundos finales en los que el reloj late para avisar.")]
        [SerializeField, Min(0f)] private float warningSeconds = 5f;
        [SerializeField, Min(0f)] private float warningPulse = 0.16f;
        [SerializeField] private Color warningColor = new Color(1f, 0.45f, 0.3f, 1f);

        [Header("Animacion")]
        [SerializeField, Min(0.05f)] private float flipDuration = 0.7f;

        private GameObject canvasObject;
        private RectTransform safeAreaRect;
        private RectTransform iconRect;
        private RectTransform countdownRect;
        private Image icon;
        private Text countdownLabel;
        private Coroutine flipRoutine;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private int lastFrameIndex = -1;
        private int lastCountdown = -1;
        private bool subscribed;

        /// <summary>Busca el reloj de la partida y monta el icono en pantalla.</summary>
        private void Awake()
        {
            flipSystem ??= FindAnyObjectByType<BoardFlipSystem>();
            CreateInterface();
        }

        /// <summary>Enciende el icono y se suscribe a la cuenta atras.</summary>
        private void OnEnable()
        {
            if (canvasObject != null)
                canvasObject.SetActive(true);

            Subscribe();
            RefreshLayout(true);

            if (flipSystem != null)
                HandleTimeChanged(flipSystem.RemainingNormalized);
        }

        /// <summary>Reajusta el encuadre y repinta los segundos que faltan.</summary>
        private void LateUpdate()
        {
            RefreshLayout(false);
            RefreshCountdown();
        }

        /// <summary>Se da de baja, corta el giro y apaga el icono.</summary>
        private void OnDisable()
        {
            Unsubscribe();

            if (flipRoutine != null)
            {
                StopCoroutine(flipRoutine);
                flipRoutine = null;
            }

            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        /// <summary>Destruye el canvas que creo este componente.</summary>
        private void OnDestroy()
        {
            if (canvasObject != null)
                Destroy(canvasObject);
        }

        /// <summary>Permite al HUD entregar el sistema ya resuelto.</summary>
        public void Configure(BoardFlipSystem targetFlipSystem)
        {
            if (targetFlipSystem == null)
                return;

            if (subscribed)
                Unsubscribe();

            flipSystem = targetFlipSystem;

            if (isActiveAndEnabled)
            {
                Subscribe();
                HandleTimeChanged(flipSystem.RemainingNormalized);
            }
        }

        /// <summary>Se pone a escuchar la cuenta atras y el aviso de giro.</summary>
        private void Subscribe()
        {
            if (subscribed || flipSystem == null)
                return;

            flipSystem.TimeChanged += HandleTimeChanged;
            flipSystem.FlipStarted += HandleFlipStarted;
            subscribed = true;
        }

        /// <summary>Deja de escuchar al reloj.</summary>
        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (flipSystem != null)
            {
                flipSystem.TimeChanged -= HandleTimeChanged;
                flipSystem.FlipStarted -= HandleFlipStarted;
            }

            subscribed = false;
        }

        /// <summary>Cambia el fotograma de arena segun lo que queda y avisa al final.</summary>
        private void HandleTimeChanged(float remainingNormalized)
        {
            if (icon == null || frames == null || frames.Length == 0)
                return;

            // 1 recien girado -> primer fotograma; 0 a punto de volcar -> ultimo.
            float drained = 1f - Mathf.Clamp01(remainingNormalized);
            int index = Mathf.Clamp(
                Mathf.FloorToInt(drained * frames.Length),
                0,
                frames.Length - 1);

            if (index != lastFrameIndex)
            {
                lastFrameIndex = index;

                if (frames[index] != null)
                    icon.sprite = frames[index];
            }

            ApplyWarning(remainingNormalized);
        }

        /// <summary>Ultimos segundos: el reloj late y se tiñe para avisar.</summary>
        private void ApplyWarning(float remainingNormalized)
        {
            if (flipSystem == null || flipRoutine != null)
                return;

            bool warning = warningSeconds > 0f && flipSystem.Remaining <= warningSeconds;

            if (!warning)
            {
                icon.color = Color.white;
                iconRect.localScale = Vector3.one;
                return;
            }

            float wave = Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f));
            icon.color = Color.Lerp(Color.white, warningColor, wave);
            iconRect.localScale = Vector3.one * (1f + wave * warningPulse);
        }

        /// <summary>
        /// El numero de segundos que faltan. Va aparte de los fotogramas del
        /// reloj: la arena da una idea del tiempo, pero para decidir si te da
        /// tiempo a una jugada mas hace falta la cifra exacta.
        /// </summary>
        private void RefreshCountdown()
        {
            if (countdownLabel == null || flipSystem == null)
                return;

            int seconds = Mathf.Max(0, Mathf.CeilToInt(flipSystem.Remaining));

            if (seconds != lastCountdown)
            {
                lastCountdown = seconds;
                countdownLabel.text = seconds.ToString();
            }

            // El numero avisa igual que el icono: se tiñe y late en los
            // ultimos segundos, para que los dos cuenten lo mismo.
            bool warning = warningSeconds > 0f &&
                           flipSystem.Remaining <= warningSeconds &&
                           flipRoutine == null;

            if (!warning)
            {
                countdownLabel.color = countdownColor;
                countdownRect.localScale = Vector3.one;
                return;
            }

            float wave = Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f));
            countdownLabel.color = Color.Lerp(countdownColor, warningColor, wave);
            countdownRect.localScale = Vector3.one * (1f + wave * warningPulse);
        }

        /// <summary>Empieza el giro: el reloj da media vuelta con el tablero.</summary>
        private void HandleFlipStarted()
        {
            if (iconRect == null)
                return;

            if (flipRoutine != null)
                StopCoroutine(flipRoutine);

            flipRoutine = StartCoroutine(AnimateFlip());
        }

        /// <summary>Anima el volteo del icono acompañando al giro del tablero.</summary>
        private IEnumerator AnimateFlip()
        {
            if (frames != null && frames.Length > 0 && frames[^1] != null)
            {
                icon.sprite = frames[^1];
                lastFrameIndex = frames.Length - 1;
            }

            icon.color = Color.white;
            iconRect.localScale = Vector3.one;

            float elapsed = 0f;

            while (elapsed < flipDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / flipDuration);
                float eased = progress * progress * (3f - 2f * progress);

                iconRect.localRotation = Quaternion.Euler(0f, 0f, 180f * eased);
                yield return null;
            }

            // El reloj queda derecho otra vez, listo para la siguiente vuelta.
            iconRect.localRotation = Quaternion.identity;
            lastFrameIndex = -1;
            flipRoutine = null;
        }

        // --- Construccion de la interfaz --------------------------------

        /// <summary>Monta el canvas y el icono del reloj.</summary>
        private void CreateInterface()
        {
            if (canvasObject != null)
                return;

            canvasObject = new GameObject(
                "Hourglass Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.SetActive(false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            safeAreaRect = CreateRect("Safe Area", canvasObject.transform);

            iconRect = CreateRect("Hourglass", safeAreaRect);
            iconRect.anchorMin = anchor;
            iconRect.anchorMax = anchor;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = offset;
            iconRect.sizeDelta = iconSize;

            icon = iconRect.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            if (frames != null && frames.Length > 0)
                icon.sprite = frames[0];

            Shadow shadow = iconRect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(3f, -3f);
            shadow.useGraphicAlpha = true;

            CreateCountdown();
        }

        /// <summary>
        /// El numero cuelga de la zona segura y no del icono: el reloj da media
        /// vuelta en cada giro y la cifra se leeria del reves.
        /// </summary>
        private void CreateCountdown()
        {
            if (!showCountdown)
                return;

            countdownRect = CreateRect("Countdown", safeAreaRect);
            countdownRect.anchorMin = anchor;
            countdownRect.anchorMax = anchor;
            countdownRect.pivot = new Vector2(0.5f, 1f);
            countdownRect.anchoredPosition = offset -
                new Vector2(0f, iconSize.y * 0.5f + countdownSpacing);
            countdownRect.sizeDelta = new Vector2(
                Mathf.Max(iconSize.x, 100f),
                countdownFontSize + 12f);

            countdownLabel = countdownRect.gameObject.AddComponent<Text>();
            countdownLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            countdownLabel.fontSize = countdownFontSize;
            countdownLabel.fontStyle = FontStyle.Bold;
            countdownLabel.alignment = TextAnchor.UpperCenter;
            countdownLabel.color = countdownColor;
            countdownLabel.raycastTarget = false;
            countdownLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            countdownLabel.verticalOverflow = VerticalWrapMode.Overflow;
            countdownLabel.text = "0";

            // Contorno negro: el fondo del juego es claro por zonas y una cifra
            // blanca a secas se pierde encima de los bloques.
            Outline outline = countdownRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2.4f, -2.4f);
        }

        /// <summary>Rehace el encuadre solo si cambio la pantalla o la zona segura.</summary>
        private void RefreshLayout(bool force)
        {
            if (safeAreaRect == null || Screen.width <= 0 || Screen.height <= 0)
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
        }

        /// <summary>Crea un objeto de interfaz vacio colgado de otro.</summary>
        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject instance = new GameObject(objectName, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<RectTransform>();
        }
    }
}
