using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// Marcador de la partida: puntaje y racha de combos. Se construye entero
    /// por codigo, sin depender de nada colocado en la escena, y se escala con
    /// una resolucion de referencia para que se lea igual en cualquier pantalla.
    ///
    /// El combo es lo que se busca en este modo, asi que es lo que manda en el
    /// diseno: la insignia cambia de color segun la racha, da un golpe de
    /// escala cada vez que sube y arrastra un resplandor que se apaga solo.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private ComboSystem comboSystem;

        [Header("Diseno")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private Vector2 margin = new Vector2(36f, 30f);
        [SerializeField] private Vector2 panelSize = new Vector2(360f, 190f);
        [SerializeField] private int sortingOrder = 140;

        [Header("Colores")]
        [SerializeField] private Color panelColor = new Color(0.03f, 0.04f, 0.11f, 0.82f);
        [SerializeField] private Color captionColor = new Color(0.62f, 0.74f, 0.95f, 1f);
        [SerializeField] private Color scoreColor = Color.white;

        [Header("Combo")]
        [Tooltip("Color de la insignia por racha: x1, x2, x3, x4 y x5 o mas.")]
        [SerializeField]
        private Color[] comboColors =
        {
            new Color(0.16f, 0.24f, 0.42f, 0.9f),
            new Color(0.15f, 0.72f, 0.95f, 1f),
            new Color(0.25f, 0.85f, 0.45f, 1f),
            new Color(1f, 0.72f, 0.12f, 1f),
            new Color(1f, 0.28f, 0.55f, 1f)
        };
        [Tooltip("Cuanto crece la insignia al subir la racha, en tanto por uno.")]
        [SerializeField, Min(0f)] private float punchScale = 0.32f;
        [SerializeField, Min(0.05f)] private float punchDuration = 0.5f;
        [Tooltip("Puntos por segundo a los que el contador alcanza al marcador.")]
        [SerializeField, Min(1f)] private float countUpSpeed = 900f;

        private static Sprite roundedSprite;

        private GameObject canvasObject;
        private RectTransform safeAreaRect;
        private RectTransform panelRect;
        private RectTransform badgeRect;
        private Image badgeImage;
        private Image badgeGlow;
        private Image accentBar;
        private Text scoreLabel;
        private Text comboLabel;
        private Text comboHint;

        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private float displayedScore;
        private float punchTime = -1f;
        private int lastCombo;

        private void Awake()
        {
            scoreManager ??= FindAnyObjectByType<ScoreManager>();
            comboSystem ??= FindAnyObjectByType<ComboSystem>();
            CreateInterface();
        }

        private void OnEnable()
        {
            if (canvasObject != null)
                canvasObject.SetActive(true);

            if (scoreManager != null)
                scoreManager.ScoreChanged += HandleScoreChanged;
            if (comboSystem != null)
                comboSystem.ComboChanged += HandleComboChanged;
        }

        private void OnDisable()
        {
            if (scoreManager != null)
                scoreManager.ScoreChanged -= HandleScoreChanged;
            if (comboSystem != null)
                comboSystem.ComboChanged -= HandleComboChanged;

            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (canvasObject != null)
                Destroy(canvasObject);
        }

        private void Start()
        {
            displayedScore = scoreManager != null ? scoreManager.Score : 0f;
            RefreshLayout(true);
            RefreshCombo();
            RefreshScore();
        }

        private void Update()
        {
            RefreshLayout(false);
            AnimateScore();
            AnimatePunch();
        }

        private void HandleScoreChanged(int score, int multiplier)
        {
            RefreshScore();
        }

        private void HandleComboChanged(int combo)
        {
            // Solo golpea cuando la racha crece: al reiniciarse a cero la
            // insignia se apaga sin llamar la atencion.
            if (combo > lastCombo)
                punchTime = 0f;

            lastCombo = combo;
            RefreshCombo();
        }

        // --- Contenido ----------------------------------------------------

        /// <summary>
        /// El contador persigue al marcador en vez de saltar de golpe: una
        /// cascada larga se lee como una subida continua.
        /// </summary>
        private void AnimateScore()
        {
            if (scoreLabel == null)
                return;

            float target = scoreManager != null ? scoreManager.Score : 0f;

            if (Mathf.Approximately(displayedScore, target))
                return;

            displayedScore = Mathf.MoveTowards(
                displayedScore,
                target,
                Mathf.Max(countUpSpeed, Mathf.Abs(target - displayedScore) * 2f) *
                Time.unscaledDeltaTime);

            RefreshScore();
        }

        private void RefreshScore()
        {
            if (scoreLabel == null)
                return;

            int target = scoreManager != null ? scoreManager.Score : 0;

            // Con la partida recien empezada el contador debe caer a cero de
            // inmediato, no bajar poco a poco desde la puntuacion anterior.
            if (target == 0)
                displayedScore = 0f;

            scoreLabel.text = Mathf.RoundToInt(displayedScore).ToString("N0");
        }

        private void RefreshCombo()
        {
            if (badgeImage == null)
                return;

            int combo = Mathf.Max(1, comboSystem != null ? comboSystem.CurrentCombo : 0);
            Color color = GetComboColor(combo);

            badgeImage.color = color;
            comboLabel.text = $"COMBO  x{combo}";

            // A partir de dos encadenados la insignia se enciende del todo y
            // aparece el rotulo que celebra la racha.
            bool hot = combo >= 2;
            comboLabel.color = hot ? Color.white : new Color(0.78f, 0.85f, 0.96f, 1f);
            comboHint.gameObject.SetActive(hot);
            comboHint.text = combo >= 4 ? "¡EN RACHA!" : "¡ENCADENADO!";
            comboHint.color = color;
            accentBar.color = color;

            // El halo se tiñe del color de la racha; su transparencia la lleva
            // el golpe, salvo cuando no hay racha y se apaga del todo.
            badgeGlow.color = new Color(
                color.r,
                color.g,
                color.b,
                hot ? badgeGlow.color.a : 0f);
        }

        private Color GetComboColor(int combo)
        {
            if (comboColors == null || comboColors.Length == 0)
                return Color.cyan;

            int index = Mathf.Clamp(combo - 1, 0, comboColors.Length - 1);
            return comboColors[index];
        }

        /// <summary>
        /// El golpe: la insignia crece de golpe y vuelve a su sitio con un
        /// rebote corto, mientras el resplandor de detras se apaga.
        /// </summary>
        private void AnimatePunch()
        {
            if (badgeRect == null || punchTime < 0f)
                return;

            punchTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(punchTime / punchDuration);

            // Seno amortiguado: sube rapido, se pasa un poco y se asienta.
            float wave = Mathf.Sin(progress * Mathf.PI * 1.5f) * (1f - progress);
            float scale = 1f + punchScale * wave;
            badgeRect.localScale = new Vector3(scale, scale, 1f);

            Color glow = badgeGlow.color;
            badgeGlow.color = new Color(glow.r, glow.g, glow.b, 0.55f * (1f - progress));

            if (progress < 1f)
                return;

            badgeRect.localScale = Vector3.one;
            punchTime = -1f;
        }

        // --- Construccion de la interfaz -----------------------------------

        private void CreateInterface()
        {
            if (canvasObject != null)
                return;

            canvasObject = new GameObject(
                "Match3 HUD Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            safeAreaRect = CreateRect("Safe Area", canvasObject.transform);

            panelRect = CreateRect("Score Panel", safeAreaRect);
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = panelSize;
            panelRect.anchoredPosition = new Vector2(margin.x, -margin.y);

            Image panel = AddRounded(panelRect, panelColor);
            panel.raycastTarget = false;

            // Filo de color a la izquierda: es lo que tiñe el panel entero con
            // el color de la racha sin tener que repintar el fondo.
            RectTransform accentRect = CreateRect("Accent", panelRect);
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.sizeDelta = new Vector2(8f, -20f);
            accentRect.anchoredPosition = new Vector2(10f, 0f);
            accentBar = AddRounded(accentRect, GetComboColor(1));
            accentBar.raycastTarget = false;

            CreateCaption();
            CreateScoreLabel();
            CreateComboBadge();
        }

        private void CreateCaption()
        {
            RectTransform rect = CreateRect("Caption", panelRect);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(panelSize.x - 50f, 26f);
            rect.anchoredPosition = new Vector2(30f, -16f);

            Text caption = AddText(rect, "P U N T A J E", 20, FontStyle.Bold);
            caption.color = captionColor;
            caption.alignment = TextAnchor.MiddleLeft;
        }

        private void CreateScoreLabel()
        {
            RectTransform rect = CreateRect("Score", panelRect);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(panelSize.x - 50f, 58f);
            rect.anchoredPosition = new Vector2(30f, -40f);

            scoreLabel = AddText(rect, "0", 52, FontStyle.Bold);
            scoreLabel.color = scoreColor;
            scoreLabel.alignment = TextAnchor.MiddleLeft;
            AddOutline(scoreLabel, new Color(0f, 0f, 0f, 0.85f), 2.2f);
        }

        private void CreateComboBadge()
        {
            badgeRect = CreateRect("Combo Badge", panelRect);
            badgeRect.anchorMin = new Vector2(0f, 0f);
            badgeRect.anchorMax = new Vector2(0f, 0f);
            badgeRect.pivot = new Vector2(0f, 0f);
            badgeRect.sizeDelta = new Vector2(panelSize.x - 60f, 46f);
            badgeRect.anchoredPosition = new Vector2(30f, 18f);

            // El resplandor va detras y algo mas grande, para que asome como un
            // halo alrededor de la insignia mientras dura el golpe.
            RectTransform glowRect = CreateRect("Glow", badgeRect);
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-10f, -10f);
            glowRect.offsetMax = new Vector2(10f, 10f);
            badgeGlow = AddRounded(glowRect, new Color(1f, 1f, 1f, 0f));
            badgeGlow.raycastTarget = false;

            badgeImage = AddRounded(badgeRect, GetComboColor(1));
            badgeImage.raycastTarget = false;
            // El fondo se dibuja despues del halo, asi que hay que devolver el
            // halo al primer puesto para que quede por detras.
            glowRect.SetAsFirstSibling();

            RectTransform labelRect = CreateRect("Combo Label", badgeRect);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 0f);
            labelRect.offsetMax = new Vector2(-16f, 0f);

            comboLabel = AddText(labelRect, "COMBO  x1", 26, FontStyle.Bold);
            comboLabel.alignment = TextAnchor.MiddleLeft;
            AddOutline(comboLabel, new Color(0f, 0f, 0f, 0.6f), 1.6f);

            RectTransform hintRect = CreateRect("Combo Hint", badgeRect);
            hintRect.anchorMin = Vector2.zero;
            hintRect.anchorMax = Vector2.one;
            hintRect.offsetMin = new Vector2(16f, 0f);
            hintRect.offsetMax = new Vector2(-16f, 0f);

            comboHint = AddText(hintRect, "¡ENCADENADO!", 20, FontStyle.Bold);
            comboHint.alignment = TextAnchor.MiddleRight;
            AddOutline(comboHint, new Color(0f, 0f, 0f, 0.75f), 1.6f);
            comboHint.gameObject.SetActive(false);
        }

        // --- Zona segura ---------------------------------------------------

        private void RefreshLayout(bool force)
        {
            if (safeAreaRect == null)
                return;

            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

            if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize)
                return;

            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
            UpdateSafeArea();
        }

        private void UpdateSafeArea()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
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

        // --- Piezas sueltas ------------------------------------------------

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject instance = new GameObject(objectName, typeof(RectTransform));
            instance.transform.SetParent(parent, false);

            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image AddRounded(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static Text AddText(RectTransform rect, string value, int size, FontStyle style)
        {
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.text = value;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void AddOutline(Text text, Color color, float thickness)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
        }

        /// <summary>
        /// Genera una vez el rectangulo redondeado que usan el panel y la
        /// insignia. Se dibuja a mano en vez de tirar de un sprite del proyecto
        /// para que el HUD funcione sin nada asignado en la escena.
        /// </summary>
        private static Sprite GetRoundedSprite()
        {
            if (roundedSprite != null)
                return roundedSprite;

            const int size = 64;
            const float radius = 18f;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "HudRoundedRect"
            };

            Color32[] pixels = new Color32[size * size];
            Vector2 half = new Vector2(size * 0.5f - radius, size * 0.5f - radius);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(
                    x + 0.5f - size * 0.5f,
                    y + 0.5f - size * 0.5f);
                Vector2 delta = new Vector2(
                    Mathf.Abs(point.x) - half.x,
                    Mathf.Abs(point.y) - half.y);

                // Distancia con signo al borde redondeado: negativa dentro y
                // positiva fuera, con un pixel de transicion para suavizarlo.
                float outside = new Vector2(
                    Mathf.Max(delta.x, 0f),
                    Mathf.Max(delta.y, 0f)).magnitude;
                float inside = Mathf.Min(Mathf.Max(delta.x, delta.y), 0f);
                float distance = outside + inside - radius;
                byte alpha = (byte)(Mathf.Clamp01(0.5f - distance) * 255f);

                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            roundedSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            roundedSprite.name = "HudRoundedRect";
            return roundedSprite;
        }
    }
}
