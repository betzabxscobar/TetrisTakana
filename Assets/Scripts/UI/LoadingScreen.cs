using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// La pantalla de carga que tapa el cambio de escena. Entra desde el menu
    /// al pulsar JUGAR y no se quita hasta que la escena nueva esta montada y
    /// ha dibujado sus primeros fotogramas.
    ///
    /// Cargar la escena de juego no es instantaneo: hay que subir a la tarjeta
    /// grafica las texturas de las fichas, del tablero y de la mascota, y los
    /// HUD de esta escena se construyen por codigo y generan sus propias
    /// texturas en el primer fotograma. Sin esto el juego se queda pillado con
    /// el menu congelado en pantalla y parece que se ha colgado.
    ///
    /// Se monta entera por codigo y vive en un prefab de Resources, asi que no
    /// hay que tocar ninguna escena para que funcione desde cualquiera de
    /// ellas. Sobrevive al cambio de escena con DontDestroyOnLoad porque tiene
    /// que seguir tapando justo mientras la escena vieja desaparece.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : MonoBehaviour
    {
        /// <summary>Ruta del prefab dentro de Resources.</summary>
        private const string PrefabPath = "LoadingScreen";

        [Header("Recursos")]
        [Tooltip("Fondo a pantalla completa. El mismo del menu para que no de un salto.")]
        [SerializeField] private Sprite background;
        [Tooltip("Logo del juego, arriba.")]
        [SerializeField] private Sprite logo;
        [Tooltip("Fotogramas del reloj de arena, en orden. Es el indicador de que sigue vivo.")]
        [SerializeField] private Sprite[] hourglassFrames;
        [Tooltip("Fuente de los rotulos. Vacio: la de sistema.")]
        [SerializeField] private Font font;

        [Header("Diseno")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [Tooltip("Por encima de todo lo demas: la tarjeta de derrota va en 200.")]
        [SerializeField] private int sortingOrder = 500;
        [Tooltip("Velo oscuro sobre el fondo para que se lean los rotulos.")]
        [SerializeField] private Color veilColor = new Color(0.01f, 0.015f, 0.04f, 0.72f);
        [SerializeField] private Color textColor = new Color(1f, 0.95f, 0.82f, 1f);
        [SerializeField] private Color barColor = new Color(0.15f, 0.78f, 1f, 1f);
        [SerializeField] private Color barTrackColor = new Color(1f, 1f, 1f, 0.16f);
        [SerializeField] private Vector2 barSize = new Vector2(620f, 18f);
        [SerializeField] private float hourglassSize = 150f;

        [Header("Ritmo")]
        [Tooltip("Lo que tarda en aparecer el telon.")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.18f;
        [Tooltip("Lo que tarda en quitarse cuando ya se puede jugar.")]
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [Tooltip("Lo minimo que se queda en pantalla, aunque cargue en un suspiro.")]
        [SerializeField, Min(0f)] private float minimumDuration = 0.9f;
        [Tooltip("Cada cuanto pasa de fotograma el reloj.")]
        [SerializeField, Min(0.02f)] private float frameInterval = 0.12f;
        [Tooltip("Fotogramas que se aguanta el telon con la escena ya activa.")]
        [SerializeField, Min(0)] private int warmupFrames = 4;

        private static LoadingScreen instance;

        private CanvasGroup group;
        private Image hourglassImage;
        private RectTransform barFillRect;
        private Text captionLabel;
        private float frameTimer;
        private int frameIndex;
        private float progress;

        /// <summary>Hay una carga en marcha.</summary>
        public static bool IsLoading => instance != null;

        /// <summary>
        /// Cambia de escena con la pantalla de carga por delante. Si el prefab
        /// no aparece o la escena no esta en el build, carga a pelo: mas vale
        /// un cambio brusco que un boton que no hace nada.
        /// </summary>
        public static void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            // Dos veces JUGAR seguidas cargarian la escena dos veces.
            if (instance != null)
                return;

            LoadingScreen prefab = Resources.Load<LoadingScreen>(PrefabPath);

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"No se encontro el prefab 'Resources/{PrefabPath}'; " +
                    "se cambia de escena sin pantalla de carga.");
                SceneManager.LoadScene(sceneName);
                return;
            }

            instance = Instantiate(prefab);
            instance.name = "LoadingScreen";
            DontDestroyOnLoad(instance.gameObject);
            instance.StartCoroutine(instance.Run(sceneName));
        }

        /// <summary>Mueve el reloj de arena y los puntos suspensivos del rotulo.</summary>
        private void Update()
        {
            // Sin escalar: la tarjeta de pausa deja Time.timeScale en cero y el
            // reloj se quedaria clavado justo al volver al menu desde la pausa.
            frameTimer += Time.unscaledDeltaTime;

            if (frameTimer < frameInterval)
                return;

            frameTimer = 0f;
            AdvanceHourglass();
        }

        /// <summary>Pasa al fotograma siguiente del reloj y anima los puntos.</summary>
        private void AdvanceHourglass()
        {
            if (hourglassFrames != null && hourglassFrames.Length > 0 && hourglassImage != null)
            {
                frameIndex = (frameIndex + 1) % hourglassFrames.Length;
                hourglassImage.sprite = hourglassFrames[frameIndex];
            }

            if (captionLabel == null)
                return;

            // Los puntos se mueven aunque la carga se atasque un momento: es lo
            // que distingue "va lento" de "se ha colgado".
            int dots = frameIndex % 4;
            captionLabel.text = "CARGANDO" + new string('.', dots);
        }

        /// <summary>
        /// Todo el viaje: telon, carga, activacion de la escena y retirada.
        /// </summary>
        private IEnumerator Run(string sceneName)
        {
            Build();
            yield return Fade(0f, 1f, fadeInDuration);

            float started = Time.unscaledTime;
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

            if (operation == null)
            {
                Debug.LogError(
                    $"No se pudo cargar la escena '{sceneName}'. " +
                    "Comprueba que este en Build Settings.",
                    this);
                Dismiss();
                yield break;
            }

            // Se corta la activacion para poder enseñar la barra hasta el final
            // y no saltar a la escena nueva a mitad de la animacion.
            operation.allowSceneActivation = false;

            // Con la activacion cortada el progreso se planta en 0.9, asi que
            // ese 0.9 es el 100% de lo que se puede cargar por adelantado.
            while (operation.progress < 0.9f)
            {
                SetProgress(operation.progress / 0.9f);
                yield return null;
            }

            SetProgress(1f);

            // Un parpadeo de telon marea mas que una espera corta.
            while (Time.unscaledTime - started < minimumDuration)
                yield return null;

            // La pausa deja el tiempo congelado; la escena nueva tiene que
            // arrancar con el reloj corriendo.
            Time.timeScale = 1f;

            operation.allowSceneActivation = true;

            while (!operation.isDone)
                yield return null;

            // Aqui la escena ya corrio sus Awake y sus Start, pero todavia no ha
            // dibujado nada. Estos fotogramas con el telon puesto son los que se
            // comen el tiron del primer render: subir texturas, compilar los
            // shaders y construir los HUD que se generan por codigo.
            for (int frame = 0; frame < warmupFrames; frame++)
                yield return new WaitForEndOfFrame();

            yield return Fade(1f, 0f, fadeOutDuration);
            Dismiss();
        }

        /// <summary>Deja la barra en su sitio, de 0 a 1.</summary>
        private void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);

            if (barFillRect != null)
                barFillRect.sizeDelta = new Vector2(barSize.x * progress, barSize.y);
        }

        /// <summary>Lleva la opacidad del telon de un valor a otro.</summary>
        private IEnumerator Fade(float from, float to, float duration)
        {
            if (group == null)
                yield break;

            if (duration <= 0f)
            {
                group.alpha = to;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = to;
        }

        /// <summary>Se quita de en medio y deja pasar la siguiente carga.</summary>
        private void Dismiss()
        {
            if (instance == this)
                instance = null;

            Destroy(gameObject);
        }

        // --- Construccion de la interfaz -------------------------------------

        /// <summary>Monta el canvas con el fondo, el logo, el reloj y la barra.</summary>
        private void Build()
        {
            GameObject canvasObject = new GameObject(
                "Loading Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;

            RectTransform root = canvasObject.GetComponent<RectTransform>();

            CreateBackground(root);
            CreateLogo(root);
            CreateHourglass(root);
            CreateCaption(root);
            CreateBar(root);

            SetProgress(0f);
        }

        /// <summary>Fondo a pantalla completa con su velo por encima.</summary>
        private void CreateBackground(RectTransform parent)
        {
            RectTransform rect = CreateRect("Background", parent);
            Image image = rect.gameObject.AddComponent<Image>();

            if (background != null)
            {
                image.sprite = background;
                image.color = Color.white;
            }
            else
            {
                image.color = new Color(0.02f, 0.03f, 0.08f, 1f);
            }

            // El unico que come clics: mientras dura la carga no debe llegar
            // ninguna pulsacion a los botones de la escena de debajo.
            image.raycastTarget = true;

            RectTransform veilRect = CreateRect("Veil", parent);
            Image veil = veilRect.gameObject.AddComponent<Image>();
            veil.color = veilColor;
            veil.raycastTarget = false;
        }

        /// <summary>El logo del juego, en el tercio de arriba.</summary>
        private void CreateLogo(RectTransform parent)
        {
            if (logo == null)
                return;

            RectTransform rect = CreateRect("Logo", parent);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(760f, 240f);
            rect.anchoredPosition = new Vector2(0f, -140f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = logo;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        /// <summary>El reloj de arena que va pasando fotogramas en el centro.</summary>
        private void CreateHourglass(RectTransform parent)
        {
            if (hourglassFrames == null || hourglassFrames.Length == 0)
                return;

            RectTransform rect = CreateRect("Hourglass", parent);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(hourglassSize, hourglassSize);
            rect.anchoredPosition = new Vector2(0f, 40f);

            hourglassImage = rect.gameObject.AddComponent<Image>();
            hourglassImage.sprite = hourglassFrames[0];
            hourglassImage.preserveAspect = true;
            hourglassImage.raycastTarget = false;
        }

        /// <summary>El rotulo de CARGANDO, debajo del reloj.</summary>
        private void CreateCaption(RectTransform parent)
        {
            RectTransform rect = CreateRect("Caption", parent);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 60f);
            rect.anchoredPosition = new Vector2(0f, -80f);

            captionLabel = AddText(rect, "CARGANDO", 30);
            captionLabel.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>La barra que avanza con lo que lleva cargado.</summary>
        private void CreateBar(RectTransform parent)
        {
            RectTransform trackRect = CreateRect("Bar Track", parent);
            trackRect.anchorMin = new Vector2(0.5f, 0.5f);
            trackRect.anchorMax = new Vector2(0.5f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.sizeDelta = barSize;
            trackRect.anchoredPosition = new Vector2(0f, -160f);

            Image track = trackRect.gameObject.AddComponent<Image>();
            track.color = barTrackColor;
            track.raycastTarget = false;

            // Anclado a la izquierda: al crecer se estira hacia la derecha en
            // vez de ensancharse desde el centro.
            barFillRect = CreateRect("Bar Fill", trackRect);
            barFillRect.anchorMin = new Vector2(0f, 0.5f);
            barFillRect.anchorMax = new Vector2(0f, 0.5f);
            barFillRect.pivot = new Vector2(0f, 0.5f);
            barFillRect.anchoredPosition = Vector2.zero;
            barFillRect.sizeDelta = new Vector2(0f, barSize.y);

            Image fill = barFillRect.gameObject.AddComponent<Image>();
            fill.color = barColor;
            fill.raycastTarget = false;
        }

        // --- Piezas sueltas --------------------------------------------------

        /// <summary>Crea un objeto de interfaz estirado sobre el que lo contiene.</summary>
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

        /// <summary>Añade un rotulo con la fuente del juego y su contorno.</summary>
        private Text AddText(RectTransform rect, string value, int size)
        {
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font != null
                ? font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.text = value;
            text.color = textColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }
    }
}
