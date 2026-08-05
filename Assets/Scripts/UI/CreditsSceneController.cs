using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Datos de una persona que aparece en la pantalla de creditos.
    /// Unity muestra Nombre y Rol dentro de la lista del Inspector.
    /// </summary>
    [Serializable]
    public sealed class CreditEntry
    {
        [Tooltip("Nombre que aparecera en los creditos.")]
        [SerializeField] private string nombre = string.Empty;

        [Tooltip("Funcion o aporte realizado por esta persona.")]
        [SerializeField] private string rol = string.Empty;

        public string Nombre => nombre?.Trim() ?? string.Empty;
        public string Rol => rol?.Trim() ?? string.Empty;
        public bool IsEmpty => Nombre.Length == 0 && Rol.Length == 0;
    }

    /// <summary>
    /// Muestra nombres y roles sobre un fondo negro y los desplaza lentamente
    /// desde la parte superior hasta la parte inferior de la pantalla.
    /// </summary>
    [AddComponentMenu("Tetris Takana/UI/Creditos Descendentes")]
    [DisallowMultipleComponent]
    public sealed class CreditsSceneController : MonoBehaviour
    {
        [Header("Colaboradores")]
        [Tooltip("Agrega un elemento por persona y completa Nombre y Rol.")]
        [SerializeField] private List<CreditEntry> colaboradores =
            new List<CreditEntry>();

        [Header("Movimiento")]
        [Tooltip("Unidades de interfaz recorridas por segundo.")]
        [SerializeField, Min(1f)] private float velocidadDescenso = 45f;
        [SerializeField] private bool repetir = true;
        [SerializeField, Min(0f)] private float margenFueraPantalla = 80f;

        [Header("Texto")]
        [SerializeField, Min(240f)] private float anchoMaximo = 1000f;
        [SerializeField, Min(0f)] private float margenLateral = 64f;
        [SerializeField, Min(70f)] private float altoEntrada = 110f;
        [SerializeField, Min(0f)] private float separacionEntradas = 30f;
        [SerializeField, Min(12)] private int tamanoNombre = 36;
        [SerializeField, Min(10)] private int tamanoRol = 23;

        [Header("Escena")]
        [Tooltip("Canvas de la escena que contiene la imagen de fondo y los creditos.")]
        [SerializeField] private Canvas creditsCanvas;
        [Tooltip("Imagen negra que cubre el fondo. Configurala directamente en el Canvas.")]
        [SerializeField] private Image blackBackground;
        [Tooltip("RectTransform que recorta el contenido de los creditos.")]
        [SerializeField] private RectTransform creditsViewport;
        [Tooltip("RectTransform dentro del viewport donde se generan los textos.")]
        [SerializeField] private RectTransform creditsContent;
        [SerializeField] private string menuScene = "Menu";
        [SerializeField] private Vector2 referenceResolution =
            new Vector2(1920f, 1080f);
        [SerializeField] private int sortingOrder = 100;

        private readonly List<GameObject> generatedEntries =
            new List<GameObject>();

        private RectTransform viewportRect;
        private RectTransform contentRect;
        private Vector2Int lastScreenSize;
        private float startY;
        private float endY;
        private float currentY;
        private bool hasVisibleCredits;
        private bool travelReady;
        private bool isLeavingScene;

        private void Awake()
        {
            if (!ConfigureInterfaceReferences())
                return;

            RefreshCredits();
        }

        private void OnEnable()
        {
            isLeavingScene = false;
            RecalculateTravelBounds(true);
        }

        private void Update()
        {
            HandleExitInput();
            MoveCredits();
        }

        private void LateUpdate()
        {
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

            if (screenSize != lastScreenSize)
                RecalculateTravelBounds(false);
        }

        /// <summary>
        /// Regenera los textos usando la lista actual del Inspector y reinicia
        /// su recorrido desde la parte superior.
        /// </summary>
        public void RefreshCredits()
        {
            if (contentRect == null)
                return;

            ClearGeneratedEntries();
            int visibleCount = 0;

            if (colaboradores != null)
            {
                // Al desplazarse hacia abajo, el elemento inferior entra
                // primero. Se invierte la creacion para respetar el orden de
                // la lista configurada en el Inspector.
                for (int index = colaboradores.Count - 1; index >= 0; index--)
                {
                    CreditEntry collaborator = colaboradores[index];

                    if (collaborator == null || collaborator.IsEmpty)
                        continue;

                    CreateCreditEntry(collaborator, index);
                    visibleCount++;
                }
            }

            hasVisibleCredits = visibleCount > 0;

            if (!hasVisibleCredits)
                CreateEmptyMessage();

            RecalculateTravelBounds(true);
        }

        private bool ConfigureInterfaceReferences()
        {
            viewportRect = creditsViewport;
            contentRect = creditsContent;

            if (creditsCanvas == null || blackBackground == null ||
                viewportRect == null || contentRect == null)
            {
                Debug.LogError(
                    "CreditsSceneController necesita referencias a Canvas, " +
                    "Black Background, Credits Viewport y Moving Credits.",
                    this);
                enabled = false;
                return false;
            }

            CanvasScaler scaler = creditsCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = referenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            creditsCanvas.sortingOrder = sortingOrder;
            blackBackground.raycastTarget = false;

            if (viewportRect.GetComponent<RectMask2D>() == null)
                viewportRect.gameObject.AddComponent<RectMask2D>();

            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(anchoMaximo, 0f);

            VerticalLayoutGroup layout =
                contentRect.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = separacionEntradas;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                contentRect.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return true;
        }

        private void CreateCreditEntry(CreditEntry collaborator, int sourceIndex)
        {
            RectTransform entryRect = CreateRect(
                $"Credit {sourceIndex + 1:00}",
                contentRect);
            generatedEntries.Add(entryRect.gameObject);

            LayoutElement entryLayout =
                entryRect.gameObject.AddComponent<LayoutElement>();
            entryLayout.minHeight = altoEntrada;
            entryLayout.preferredHeight = altoEntrada;
            entryLayout.flexibleHeight = 0f;

            VerticalLayoutGroup entryContents =
                entryRect.gameObject.AddComponent<VerticalLayoutGroup>();
            entryContents.spacing = 3f;
            entryContents.childAlignment = TextAnchor.MiddleCenter;
            entryContents.childControlWidth = true;
            entryContents.childControlHeight = true;
            entryContents.childForceExpandWidth = true;
            entryContents.childForceExpandHeight = false;

            if (collaborator.Rol.Length > 0)
            {
                Text roleLabel = CreateText(
                    entryRect,
                    "Role",
                    collaborator.Rol.ToUpperInvariant(),
                    tamanoRol,
                    new Color(0.72f, 0.72f, 0.72f, 1f),
                    FontStyle.Normal);
                ConfigureTextLayout(roleLabel, 34f, Mathf.Max(10, tamanoRol / 2));
            }

            string displayedName = collaborator.Nombre.Length > 0
                ? collaborator.Nombre
                : "Sin nombre";
            Text nameLabel = CreateText(
                entryRect,
                "Name",
                displayedName,
                tamanoNombre,
                Color.white,
                FontStyle.Normal);
            ConfigureTextLayout(nameLabel, 52f, Mathf.Max(12, tamanoNombre / 2));
        }

        private void CreateEmptyMessage()
        {
            RectTransform messageRect = CreateRect(
                "Empty Credits Message",
                contentRect);
            generatedEntries.Add(messageRect.gameObject);

            LayoutElement messageLayout =
                messageRect.gameObject.AddComponent<LayoutElement>();
            messageLayout.minHeight = 80f;
            messageLayout.preferredHeight = 80f;

            Text message = CreateText(
                messageRect,
                "Message",
                "Agrega colaboradores en la lista del Inspector.",
                22,
                new Color(0.72f, 0.72f, 0.72f, 1f),
                FontStyle.Normal);
            Stretch(message.rectTransform);
            message.resizeTextForBestFit = true;
            message.resizeTextMinSize = 13;
            message.resizeTextMaxSize = 22;
        }

        private void RecalculateTravelBounds(bool resetPosition)
        {
            if (viewportRect == null || contentRect == null)
                return;

            Canvas.ForceUpdateCanvases();

            float availableWidth = Mathf.Max(
                1f,
                viewportRect.rect.width - margenLateral * 2f);
            contentRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Min(anchoMaximo, availableWidth));
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            if (!hasVisibleCredits)
            {
                travelReady = false;
                currentY = 0f;
                contentRect.anchoredPosition = Vector2.zero;
                return;
            }

            float previousProgress = travelReady
                ? Mathf.InverseLerp(startY, endY, currentY)
                : 0f;
            float halfViewportHeight = viewportRect.rect.height * 0.5f;
            float halfContentHeight = contentRect.rect.height * 0.5f;

            startY = halfViewportHeight + halfContentHeight + margenFueraPantalla;
            endY = -halfViewportHeight - halfContentHeight - margenFueraPantalla;
            currentY = resetPosition || !travelReady
                ? startY
                : Mathf.Lerp(startY, endY, previousProgress);
            contentRect.anchoredPosition = new Vector2(0f, currentY);
            travelReady = true;
        }

        private void MoveCredits()
        {
            if (!hasVisibleCredits || !travelReady || isLeavingScene)
                return;

            currentY -= velocidadDescenso * Time.unscaledDeltaTime;

            if (currentY <= endY)
            {
                if (repetir)
                    currentY = startY;
                else
                {
                    ReturnToMenu();
                    return;
                }
            }

            contentRect.anchoredPosition = new Vector2(0f, currentY);
        }

        private void HandleExitInput()
        {
            bool keyboardInput = Keyboard.current != null &&
                Keyboard.current.anyKey.wasPressedThisFrame;
            bool gamepadCancel = Gamepad.current != null &&
                Gamepad.current.buttonEast.wasPressedThisFrame;

            if (keyboardInput || gamepadCancel)
                ReturnToMenu();
        }

        private void ReturnToMenu()
        {
            if (isLeavingScene)
                return;

            if (string.IsNullOrWhiteSpace(menuScene) ||
                !Application.CanStreamedLevelBeLoaded(menuScene))
            {
                Debug.LogError(
                    $"No se puede abrir la escena de menu '{menuScene}'. " +
                    "Comprueba que este incluida en Build Settings.",
                    this);
                return;
            }

            isLeavingScene = true;
            SceneManager.LoadScene(menuScene);
        }

        private void ClearGeneratedEntries()
        {
            foreach (GameObject entry in generatedEntries)
            {
                if (entry == null)
                    continue;

                entry.SetActive(false);
                Destroy(entry);
            }

            generatedEntries.Clear();
        }

        private static void ConfigureTextLayout(
            Text text,
            float preferredHeight,
            int minimumFontSize)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimumFontSize;
            text.resizeTextMaxSize = text.fontSize;

            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = preferredHeight;
            layout.preferredHeight = preferredHeight;
            layout.flexibleHeight = 0f;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            string content,
            int fontSize,
            Color color,
            FontStyle fontStyle)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text label = textObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = content ?? string.Empty;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
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
