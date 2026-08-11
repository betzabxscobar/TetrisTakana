using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TetrisTakana.Online
{
    /// <summary>
    /// Le pide el nombre al jugador la primera vez que una partida suya entra
    /// en el ranking. Se monta entero por codigo y se crea solo cuando hace
    /// falta, asi no hay que colocarlo en ninguna escena ni acordarse de
    /// enlazarlo en la tarjeta de fin de partida.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NamePrompt : MonoBehaviour
    {
        /// <summary>Lo que caben en players.display_name.</summary>
        public const int MaxLength = 16;

        private static readonly Color PanelColor = new Color(0.035f, 0.045f, 0.11f, 0.98f);
        private static readonly Color OverlayColor = new Color(0.01f, 0.015f, 0.04f, 0.8f);
        private static readonly Color AccentColor = new Color(0.15f, 0.78f, 1f, 1f);
        private static readonly Color ButtonColor = new Color(0.50f, 0.18f, 0.92f, 1f);

        private GameObject canvasObject;
        private GameObject createdEventSystem;
        private InputField field;
        private Button acceptButton;
        private Action<string> onAccepted;

        /// <summary>
        /// Muestra el aviso y devuelve por el callback el nombre elegido, o
        /// nulo si el jugador prefiere no darlo. Devuelve el aviso para que
        /// quien pregunte sepa si sigue vivo: al cambiar de escena se destruye
        /// y la respuesta no va a llegar nunca.
        /// </summary>
        public static NamePrompt Ask(Action<string> answered)
        {
            NamePrompt prompt = FindAnyObjectByType<NamePrompt>();

            if (prompt == null)
                prompt = new GameObject("Name Prompt").AddComponent<NamePrompt>();

            prompt.Show(answered);
            return prompt;
        }

        /// <summary>Monta la ventana si hace falta y la enseña.</summary>
        private void Show(Action<string> accepted)
        {
            onAccepted = accepted;

            if (canvasObject == null)
                CreateInterface();

            canvasObject.SetActive(true);
            field.text = string.Empty;

            // El foco puesto ya, para poder escribir sin tener que hacer clic.
            field.Select();
            field.ActivateInputField();
        }

        /// <summary>Cierra la ventana.</summary>
        private void Hide()
        {
            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        /// <summary>Limpia lo que creo este componente.</summary>
        private void OnDestroy()
        {
            if (canvasObject != null)
                Destroy(canvasObject);

            if (createdEventSystem != null)
                Destroy(createdEventSystem);
        }

        /// <summary>El jugador acepta: se valida y se avisa a quien pregunto.</summary>
        private void Accept()
        {
            string name = (field != null ? field.text : string.Empty).Trim();

            // Con la caja vacia no se cierra: no hay nada que guardar, y para
            // salir sin dar nombre esta el otro boton.
            if (name.Length == 0)
                return;

            if (name.Length > MaxLength)
                name = name.Substring(0, MaxLength);

            Answer(name);
        }

        /// <summary>
        /// El jugador pasa. Hace falta una salida: el overlay se come los
        /// clics, asi que sin esto no podria ni volver al menu.
        /// </summary>
        private void Skip()
        {
            Answer(null);
        }

        /// <summary>Cierra el aviso y contesta a quien pregunto.</summary>
        private void Answer(string name)
        {
            Hide();

            Action<string> callback = onAccepted;
            onAccepted = null;
            callback?.Invoke(name);
        }

        // --- Construccion ---------------------------------------------------

        /// <summary>Monta el canvas, el panel y sus piezas.</summary>
        private void CreateInterface()
        {
            EnsureEventSystem();

            canvasObject = new GameObject(
                "Name Prompt Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Por encima de la tarjeta de fin de partida, que va en 200.
            canvas.sortingOrder = 300;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform overlay = CreateRect("Overlay", canvasObject.transform);
            AddImage(overlay, OverlayColor);

            RectTransform panel = CreateRect("Panel", overlay);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(620f, 300f);
            panel.anchoredPosition = Vector2.zero;
            AddImage(panel, PanelColor);

            RectTransform titleRect = CreateRect("Title", panel);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(-60f, 50f);
            titleRect.anchoredPosition = new Vector2(0f, -34f);

            Text title = AddText(titleRect, "¡ENTRASTE AL RANKING!", 34);
            title.color = AccentColor;
            title.alignment = TextAnchor.MiddleCenter;

            RectTransform hintRect = CreateRect("Hint", panel);
            hintRect.anchorMin = new Vector2(0f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.sizeDelta = new Vector2(-60f, 30f);
            hintRect.anchoredPosition = new Vector2(0f, -88f);

            Text hint = AddText(hintRect, "¿Con que nombre te apuntamos?", 22);
            hint.color = new Color(0.72f, 0.8f, 0.94f, 1f);
            hint.alignment = TextAnchor.MiddleCenter;

            CreateField(panel);
            CreateButtons(panel);
        }

        /// <summary>Crea la caja donde se escribe el nombre.</summary>
        private void CreateField(RectTransform panel)
        {
            RectTransform fieldRect = CreateRect("Field", panel);
            fieldRect.anchorMin = new Vector2(0.5f, 0.5f);
            fieldRect.anchorMax = new Vector2(0.5f, 0.5f);
            fieldRect.pivot = new Vector2(0.5f, 0.5f);
            fieldRect.sizeDelta = new Vector2(460f, 62f);
            fieldRect.anchoredPosition = new Vector2(0f, -6f);
            AddImage(fieldRect, new Color(0.09f, 0.11f, 0.2f, 1f));

            RectTransform textRect = CreateRect("Text", fieldRect);
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-16f, 0f);

            Text text = AddText(textRect, string.Empty, 28);
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            RectTransform placeholderRect = CreateRect("Placeholder", fieldRect);
            placeholderRect.offsetMin = new Vector2(16f, 0f);
            placeholderRect.offsetMax = new Vector2(-16f, 0f);

            Text placeholder = AddText(placeholderRect, "Tu nombre", 28);
            placeholder.color = new Color(0.55f, 0.62f, 0.78f, 1f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.fontStyle = FontStyle.Italic;

            field = fieldRect.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = MaxLength;
            field.lineType = InputField.LineType.SingleLine;

            // Con el Enter se acepta, que es lo que la mano espera al terminar
            // de escribir en una maquina recreativa.
            field.onEndEdit.AddListener(_ =>
            {
                if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
                    Accept();
            });
        }

        /// <summary>Crea los dos botones: guardar y dejarlo estar.</summary>
        private void CreateButtons(RectTransform panel)
        {
            acceptButton = CreateButton(
                panel,
                "Accept",
                "GUARDAR",
                new Vector2(-120f, 32f),
                new Vector2(280f, 64f),
                ButtonColor,
                Color.white);
            acceptButton.onClick.AddListener(Accept);

            // Apagado, para que no compita con el de guardar.
            Button skipButton = CreateButton(
                panel,
                "Skip",
                "AHORA NO",
                new Vector2(150f, 32f),
                new Vector2(200f, 64f),
                new Color(0.12f, 0.15f, 0.26f, 1f),
                new Color(0.72f, 0.8f, 0.94f, 1f));
            skipButton.onClick.AddListener(Skip);
        }

        /// <summary>Crea un boton con su fondo y su rotulo.</summary>
        private static Button CreateButton(
            RectTransform panel,
            string objectName,
            string label,
            Vector2 position,
            Vector2 size,
            Color background,
            Color labelColor)
        {
            RectTransform rect = CreateRect(objectName, panel);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = AddImage(rect, background);

            RectTransform labelRect = CreateRect("Label", rect);
            Text text = AddText(labelRect, label, 26);
            text.color = labelColor;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        /// <summary>
        /// Sin EventSystem no hay teclado ni clics en la interfaz. Alguna
        /// escena puede no traerlo, asi que se pone uno si falta.
        /// </summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            createdEventSystem = new GameObject(
                "Event System",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        /// <summary>Crea un objeto de interfaz que llena a su padre.</summary>
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

        /// <summary>Pone un fondo de color.</summary>
        private static Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>Pone un texto con la fuente que trae Unity.</summary>
        private static Text AddText(RectTransform rect, string content, int size)
        {
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }
    }
}
