using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Gestiona los cambios de escena con una cortina de bloques que se abre y
    /// se cierra como un tablero de Tetris Takana.
    ///
    /// Se crea bajo demanda para que funcione al abrir cualquier escena
    /// directamente desde el editor, y vive entre escenas para que el Canvas
    /// no desaparezca mientras LoadSceneAsync activa el destino.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneTransitionManager : MonoBehaviour
    {
        private static SceneTransitionManager instance;

        [Header("Cuadricula")]
        [SerializeField, Min(1)] private int columns = 12;
        [SerializeField, Min(1)] private int rows = 7;
        [SerializeField, Min(0f)] private float blockGap = 1.5f;
        [SerializeField] private Color curtainBackground = new Color(0.01f, 0.015f, 0.04f, 1f);
        [SerializeField] private Color[] blockColors =
        {
            new Color(0.15f, 0.78f, 1f, 1f),
            new Color(0.50f, 0.18f, 0.92f, 1f),
            new Color(0.28f, 0.65f, 0.16f, 1f),
            new Color(0.98f, 0.40f, 0f, 1f),
            new Color(1f, 0.28f, 0.55f, 1f),
            new Color(1f, 0.78f, 0.12f, 1f)
        };

        [Header("Animacion")]
        [SerializeField, Min(0.01f)] private float coverDuration = 0.24f;
        [SerializeField, Min(0.01f)] private float revealDuration = 0.28f;
        [SerializeField, Min(0f)] private float rowStagger = 0.025f;
        [SerializeField] private int sortingOrder = 1000;

        private GameObject overlayObject;
        private RectTransform overlayRect;
        private Image backgroundImage;
        private Texture2D pixelTexture;
        private Sprite pixelSprite;
        private Image[] activeBlocks;
        private Coroutine transitionRoutine;
        private bool isTransitioning;

        public static bool IsTransitioning => instance != null && instance.isTransitioning;

        /// <summary>
        /// Solicita una carga de escena. Las solicitudes repetidas se ignoran
        /// mientras la cortina esta activa.
        /// </summary>
        public static bool LoadScene(string sceneName)
        {
            SceneTransitionManager manager = EnsureInstance();

            if (manager == null)
                return false;

            return manager.BeginLoad(sceneName);
        }

        /// <summary>Recarga la escena actual usando la misma transicion.</summary>
        public static bool ReloadCurrentScene()
        {
            return LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Obtiene el gestor vivo o crea uno si la escena se abrio sola.</summary>
        public static SceneTransitionManager EnsureInstance()
        {
            if (instance != null)
                return instance;

            instance = FindAnyObjectByType<SceneTransitionManager>();

            if (instance != null)
                return instance;

            GameObject managerObject = new GameObject("Scene Transition Manager");
            SceneTransitionManager manager =
                managerObject.AddComponent<SceneTransitionManager>();
            // AddComponent suele llamar a Awake de inmediato, pero dejar la
            // referencia aqui tambien cubre la inicializacion diferida en
            // tests o al abrir una escena directamente desde el editor.
            instance = manager;
            return manager;
        }

        /// <summary>Conserva una sola instancia al cambiar de escena.</summary>
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Limpia la referencia estatica si se destruye el gestor.</summary>
        private void OnDestroy()
        {
            if (pixelSprite != null)
                Destroy(pixelSprite);

            if (pixelTexture != null)
                Destroy(pixelTexture);

            if (instance == this)
                instance = null;
        }

        private bool BeginLoad(string sceneName)
        {
            if (isTransitioning)
                return false;

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("No se puede cargar una escena sin nombre.", this);
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"No se puede cargar la escena '{sceneName}'. " +
                    "Comprueba que este incluida en Build Settings.",
                    this);
                return false;
            }

            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);

            // Se marca antes de arrancar la corrutina: dos botones que
            // respondan en el mismo frame tampoco pueden abrir dos cargas.
            isTransitioning = true;
            transitionRoutine = StartCoroutine(Transition(sceneName));
            return true;
        }

        private IEnumerator Transition(string sceneName)
        {
            CreateOverlay();
            SetBlocksProgress(0f, true);

            yield return AnimateBlocks(true);
            SetBackgroundAlpha(0f);

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (load == null)
            {
                Debug.LogError($"No se pudo iniciar la carga de '{sceneName}'.", this);
                FinishTransition();
                yield break;
            }

            // La escena nueva no se activa hasta que la cortina esta completa.
            // Asi nunca se ve un frame de la escena antigua o una escena a
            // medio montar durante el cambio.
            load.allowSceneActivation = false;

            while (load.progress < 0.9f)
                yield return null;

            load.allowSceneActivation = true;

            while (!load.isDone)
                yield return null;

            // Dejar pasar un frame permite que los Canvas de la escena nueva
            // terminen su Awake/OnEnable antes de abrir la cortina.
            yield return null;
            yield return AnimateBlocks(false);

            FinishTransition();
        }

        private IEnumerator AnimateBlocks(bool covering)
        {
            float duration = Mathf.Max(0.01f, covering ? coverDuration : revealDuration);
            float totalDuration = duration + Mathf.Max(0, rows - 1) * rowStagger;
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetBlocksProgress(
                    Mathf.Clamp01(elapsed / totalDuration),
                    covering);
                yield return null;
            }

            SetBlocksProgress(1f, covering);
        }

        /// <summary>
        /// Cambia la escala vertical de cada celda. Las filas inferiores entran
        /// primero al cubrir y las superiores desaparecen primero al revelar.
        /// </summary>
        private void SetBlocksProgress(float progress, bool covering)
        {
            if (activeBlocks == null)
                return;

            float duration = Mathf.Max(0.01f, covering ? coverDuration : revealDuration);

            for (int index = 0; index < activeBlocks.Length; index++)
            {
                Image image = activeBlocks[index];

                if (image == null)
                    continue;

                int row = index / Mathf.Max(1, columns);
                float delayRow = covering ? row : rows - 1 - row;
                float local = Mathf.Clamp01(
                    (progress * (duration + Mathf.Max(0, rows - 1) * rowStagger) -
                     delayRow * rowStagger) / duration);
                float eased = EaseInOutCubic(local);
                float scale = covering ? eased : 1f - eased;
                image.rectTransform.localScale = new Vector3(1f, scale, 1f);
            }
        }

        private void CreateOverlay()
        {
            if (overlayObject != null)
                return;

            EnsurePixelSprite();

            overlayObject = new GameObject(
                "Scene Transition Overlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            overlayObject.transform.SetParent(transform, false);

            Canvas canvas = overlayObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = overlayObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image background = CreateImage("Transition Background", overlayRect);
            background.sprite = pixelSprite;
            background.color = curtainBackground;
            backgroundImage = background;
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;
            background.rectTransform.SetAsFirstSibling();

            int count = Mathf.Max(1, columns) * Mathf.Max(1, rows);
            activeBlocks = new Image[count];

            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;
                Image block = CreateImage($"Transition Block {column}-{row}", overlayRect);
                block.sprite = pixelSprite;
                RectTransform rect = block.rectTransform;

                rect.anchorMin = new Vector2(
                    column / (float)columns,
                    row / (float)rows);
                rect.anchorMax = new Vector2(
                    (column + 1f) / columns,
                    (row + 1f) / rows);
                rect.offsetMin = new Vector2(blockGap, blockGap);
                rect.offsetMax = new Vector2(-blockGap, -blockGap);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = new Vector3(1f, 0f, 1f);

                block.color = GetBlockColor(index);
                activeBlocks[index] = block;
            }
        }

        private Image CreateImage(string objectName, Transform parent)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = true;
            return image;
        }

        private void EnsurePixelSprite()
        {
            if (pixelSprite != null)
                return;

            pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Scene Transition Pixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            pixelTexture.SetPixel(0, 0, Color.white);
            pixelTexture.Apply();

            pixelSprite = Sprite.Create(
                pixelTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            pixelSprite.name = "Scene Transition Pixel Sprite";
            pixelSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private Color GetBlockColor(int index)
        {
            if (blockColors == null || blockColors.Length == 0)
                return Color.white;

            return blockColors[index % blockColors.Length];
        }

        private void FinishTransition()
        {
            if (overlayObject != null)
                Destroy(overlayObject);

            overlayObject = null;
            overlayRect = null;
            backgroundImage = null;
            activeBlocks = null;
            transitionRoutine = null;
            isTransitioning = false;
        }

        private void SetBackgroundAlpha(float alpha)
        {
            if (backgroundImage == null)
                return;

            Color color = backgroundImage.color;
            color.a = Mathf.Clamp01(alpha);
            backgroundImage.color = color;
        }

        private static float EaseInOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return value < 0.5f
                ? 4f * value * value * value
                : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f;
        }
    }
}
