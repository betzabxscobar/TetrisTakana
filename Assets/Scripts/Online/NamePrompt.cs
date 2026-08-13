using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TetrisTakana.Online
{
    /// <summary>
    /// Le pide el nombre al jugador la primera vez que una partida suya entra
    /// en el ranking. Se monta entero por codigo y se crea solo cuando hace
    /// falta, asi no hay que colocarlo en ninguna escena ni acordarse de
    /// enlazarlo en la tarjeta de fin de partida.
    ///
    /// Los dibujos y la fuente vienen de un prefab en Resources para que la
    /// ventana tenga el mismo aspecto que el resto del juego: la misma tarjeta
    /// de madera que la pantalla de fin de partida y la misma fuente de
    /// pixeles. Sin el prefab sigue funcionando, pero con cajas de color.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NamePrompt : MonoBehaviour
    {
        /// <summary>Lo que caben en players.display_name.</summary>
        public const int MaxLength = 16;

        /// <summary>Ruta del prefab dentro de Resources.</summary>
        private const string PrefabPath = "NamePrompt";

        [Header("Recursos")]
        [Tooltip("La tarjeta de fondo. La misma de la pantalla de fin de partida.")]
        [SerializeField] private Sprite cardSprite;
        [Tooltip("Marco de la caja donde se escribe. Vacio: una caja de color.")]
        [SerializeField] private Sprite fieldSprite;
        [Tooltip("Fuente de pixeles del juego. Vacio: la de sistema.")]
        [SerializeField] private Font pixelFont;

        [Header("Diseno")]
        [SerializeField] private Vector2 cardSize = new Vector2(720f, 420f);
        [SerializeField] private Color overlayColor = new Color(0.01f, 0.015f, 0.04f, 0.82f);
        [SerializeField] private Color cardColor = new Color(0.035f, 0.045f, 0.11f, 0.98f);
        [SerializeField] private Color accentColor = new Color(0.15f, 0.78f, 1f, 1f);
        [SerializeField] private Color captionColor = new Color(0.72f, 0.8f, 0.94f, 1f);
        [SerializeField] private Color acceptColor = new Color(0.5f, 0.18f, 0.92f, 1f);
        [SerializeField] private Color skipColor = new Color(0.12f, 0.15f, 0.26f, 1f);

        private static Sprite roundedSprite;

        private GameObject canvasObject;
        private GameObject createdEventSystem;
        private InputField field;
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
                prompt = Create();

            prompt.Show(answered);
            return prompt;
        }

        /// <summary>
        /// Saca el aviso del prefab, que es el que trae los dibujos y la
        /// fuente. Si no aparece se monta uno pelado: mas vale una ventana fea
        /// que quedarse sin poder apuntar el nombre.
        /// </summary>
        private static NamePrompt Create()
        {
            NamePrompt prefab = Resources.Load<NamePrompt>(PrefabPath);

            if (prefab != null)
            {
                NamePrompt instance = Instantiate(prefab);
                instance.name = "Name Prompt";
                return instance;
            }

            Debug.LogWarning(
                $"No se encontro el prefab 'Resources/{PrefabPath}'; " +
                "el aviso del nombre sale sin los dibujos del juego.");
            return new GameObject("Name Prompt").AddComponent<NamePrompt>();
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

        /// <summary>Monta el canvas, la tarjeta y sus piezas.</summary>
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
            AddImage(overlay, overlayColor);

            RectTransform card = CreateRect("Card", overlay);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = cardSize;
            card.anchoredPosition = Vector2.zero;

            // Con el dibujo de la tarjeta la imagen va en blanco, que el sprite
            // ya trae su color; el tinte solo vale para el respaldo dibujado.
            Image cardImage = AddImage(card, cardSprite != null ? Color.white : cardColor);

            if (cardSprite != null)
            {
                cardImage.sprite = cardSprite;
                cardImage.type = Image.Type.Simple;
                cardImage.preserveAspect = true;
            }
            else
            {
                cardImage.sprite = GetRoundedSprite();
                cardImage.type = Image.Type.Sliced;
            }

            CreateTitle(card);
            CreateField(card);
            CreateButtons(card);
        }

        /// <summary>Escribe el titulo y la pregunta.</summary>
        private void CreateTitle(RectTransform card)
        {
            RectTransform titleRect = CreateRect("Title", card);
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(cardSize.x - 90f, 52f);
            titleRect.anchoredPosition = new Vector2(0f, -60f);

            // En mayusculas y sin tildes: es lo que hace el resto del juego con
            // esta fuente, que de las vocales acentuadas no trae dibujo.
            Text title = AddText(titleRect, "ENTRASTE AL RANKING", 22);
            title.color = accentColor;

            RectTransform hintRect = CreateRect("Hint", card);
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.sizeDelta = new Vector2(cardSize.x - 90f, 34f);
            hintRect.anchoredPosition = new Vector2(0f, -122f);

            Text hint = AddText(hintRect, "CON QUE NOMBRE TE APUNTAMOS", 13);
            hint.color = captionColor;
        }

        /// <summary>Crea la caja donde se escribe el nombre.</summary>
        private void CreateField(RectTransform card)
        {
            RectTransform fieldRect = CreateRect("Field", card);
            fieldRect.anchorMin = new Vector2(0.5f, 0.5f);
            fieldRect.anchorMax = new Vector2(0.5f, 0.5f);
            fieldRect.pivot = new Vector2(0.5f, 0.5f);
            fieldRect.sizeDelta = new Vector2(cardSize.x - 200f, 70f);
            fieldRect.anchoredPosition = new Vector2(0f, -6f);

            Image box = AddImage(fieldRect, new Color(0.06f, 0.08f, 0.16f, 0.95f));

            if (fieldSprite != null)
            {
                box.sprite = fieldSprite;
                box.type = Image.Type.Sliced;
                box.color = Color.white;
            }
            else
            {
                box.sprite = GetRoundedSprite();
                box.type = Image.Type.Sliced;
            }

            RectTransform textRect = CreateRect("Text", fieldRect);
            textRect.offsetMin = new Vector2(22f, 0f);
            textRect.offsetMax = new Vector2(-22f, 0f);

            Text text = AddText(textRect, string.Empty, 20);
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            RectTransform placeholderRect = CreateRect("Placeholder", fieldRect);
            placeholderRect.offsetMin = new Vector2(22f, 0f);
            placeholderRect.offsetMax = new Vector2(-22f, 0f);

            Text placeholder = AddText(placeholderRect, "TU NOMBRE", 20);
            placeholder.color = new Color(0.5f, 0.57f, 0.72f, 1f);
            placeholder.alignment = TextAnchor.MiddleLeft;

            field = fieldRect.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = MaxLength;
            field.lineType = InputField.LineType.SingleLine;

            // Con el Enter se acepta, que es lo que la mano espera al terminar
            // de escribir en una maquina recreativa. Se lee por el Input System
            // nuevo: el proyecto tiene desactivada la entrada antigua, y la
            // UnityEngine.Input de siempre revienta con una excepcion.
            field.onEndEdit.AddListener(_ =>
            {
                Keyboard keyboard = Keyboard.current;

                if (keyboard != null &&
                    (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed))
                    Accept();
            });
        }

        /// <summary>Crea los dos botones: guardar y dejarlo estar.</summary>
        private void CreateButtons(RectTransform card)
        {
            Button acceptButton = CreateButton(
                card,
                "Accept",
                "GUARDAR",
                new Vector2(-125f, 58f),
                new Vector2(260f, 68f),
                acceptColor,
                Color.white);
            acceptButton.onClick.AddListener(Accept);

            // Apagado, para que no compita con el de guardar.
            Button skipButton = CreateButton(
                card,
                "Skip",
                "AHORA NO",
                new Vector2(135f, 58f),
                new Vector2(220f, 68f),
                skipColor,
                captionColor);
            skipButton.onClick.AddListener(Skip);
        }

        /// <summary>Crea un boton con su fondo redondeado y su rotulo.</summary>
        private Button CreateButton(
            RectTransform card,
            string objectName,
            string label,
            Vector2 position,
            Vector2 size,
            Color background,
            Color labelColor)
        {
            RectTransform rect = CreateRect(objectName, card);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = AddImage(rect, background);
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;

            // La misma sombra caida que los botones de la tarjeta de derrota.
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(0f, -6f);
            shadow.useGraphicAlpha = true;

            RectTransform labelRect = CreateRect("Label", rect);
            Text text = AddText(labelRect, label, 15);
            text.color = labelColor;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.86f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.76f, 0.9f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        /// <summary>
        /// Se asegura de que hay un EventSystem, sin el no hay teclado ni clics
        /// en la interfaz. El modulo es el del Input System nuevo, que es el
        /// que tiene activado el proyecto: con el antiguo la ventana sale pero
        /// no se puede ni escribir ni pulsar nada.
        /// </summary>
        private void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current;

            if (existing == null)
                existing = FindAnyObjectByType<EventSystem>();

            if (existing != null)
                return;

            createdEventSystem = new GameObject("Name Prompt EventSystem");
            createdEventSystem.SetActive(false);
            createdEventSystem.AddComponent<EventSystem>();
            InputSystemUIInputModule inputModule =
                createdEventSystem.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
            createdEventSystem.SetActive(true);
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

        /// <summary>Pone un texto con la fuente de pixeles del juego.</summary>
        private Text AddText(RectTransform rect, string content, int size)
        {
            GameObject target = rect.gameObject;
            Text text = target.AddComponent<Text>();
            text.font = pixelFont != null
                ? pixelFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;

            // La fuente de pixeles ya viene gruesa; ponerle negrita la emborrona.
            text.fontStyle = pixelFont != null ? FontStyle.Normal : FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;
            return text;
        }

        /// <summary>
        /// Dibuja una vez el rectangulo redondeado de los botones y del
        /// respaldo de la tarjeta, igual que hacen los demas paneles del juego.
        /// </summary>
        private static Sprite GetRoundedSprite()
        {
            if (roundedSprite != null)
                return roundedSprite;

            const int size = 64;
            const float radius = 16f;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "NamePromptRoundedRect",
                hideFlags = HideFlags.HideAndDontSave
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
            roundedSprite.name = "NamePromptRoundedRect";
            roundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return roundedSprite;
        }
    }
}
