using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Mueve dos copias contiguas de un RawImage para crear un fondo que
    /// reaparece por el lado opuesto cuando sale de la pantalla. La textura
    /// conserva su proporcion y debe tener Wrap Mode en Repeat.
    /// </summary>
    [AddComponentMenu("Tetris Takana/UI/Raw Image Scroller")]
    [RequireComponent(typeof(RawImage))]
    public sealed class RawImageScroller : MonoBehaviour
    {
        [Tooltip("Velocidad en unidades de interfaz por segundo. Positivo mueve a la izquierda; negativo, a la derecha.")]
        [SerializeField] private float speed = 45f;

        [Tooltip("Usa tiempo no escalado, igual que el movimiento de los creditos.")]
        [SerializeField] private bool useUnscaledTime = true;

        private RawImage rawImage;
        private RawImage loopImage;
        private RectTransform rawRect;
        private RectTransform loopRect;
        private RectTransform containerRect;
        private Vector2 lastContainerSize;
        private Vector2 tileSize;
        private Texture lastTexture;
        private bool layoutReady;

        /// <summary>Coge la imagen que va a desplazarse.</summary>
        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            rawRect = rawImage.rectTransform;
            containerRect = rawRect.parent as RectTransform;
        }

        /// <summary>Crea la copia que cierra el bucle y coloca las dos piezas.</summary>
        private void Start()
        {
            CreateLoopImage();
            RefreshLayout(true);
        }

        /// <summary>Desplaza el fondo y lo devuelve al principio al salirse.</summary>
        private void Update()
        {
            if (rawImage == null || containerRect == null ||
                rawImage.texture == null)
                return;

            if (loopImage == null)
                CreateLoopImage();

            if (loopImage == null)
                return;

            Canvas.ForceUpdateCanvases();

            Vector2 containerSize = containerRect.rect.size;
            if (!layoutReady || containerSize != lastContainerSize ||
                rawImage.texture != lastTexture)
            {
                RefreshLayout(!layoutReady);
            }

            if (!layoutReady)
                return;

            float deltaTime = useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            if (deltaTime <= 0f || speed == 0f)
                return;

            float distance = speed * deltaTime;
            Vector2 movement = Vector2.left * distance;

            rawRect.anchoredPosition += movement;
            loopRect.anchoredPosition += movement;

            WrapPosition(rawRect);
            WrapPosition(loopRect);
        }

        /// <summary>Duplica la imagen para que el desplazamiento no deje huecos.</summary>
        private void CreateLoopImage()
        {
            if (loopImage != null || rawImage == null || containerRect == null)
                return;

            GameObject loopObject = Instantiate(
                rawImage.gameObject,
                containerRect);
            loopObject.name = $"{gameObject.name} Loop";

            RawImageScroller loopScroller =
                loopObject.GetComponent<RawImageScroller>();
            if (loopScroller != null)
                loopScroller.enabled = false;

            loopImage = loopObject.GetComponent<RawImage>();
            loopRect = loopImage != null ? loopImage.rectTransform : null;

            if (loopImage != null)
                loopImage.raycastTarget = false;
        }

        /// <summary>Recalcula el tamaño de las dos piezas cuando cambia el contenedor.</summary>
        private void RefreshLayout(bool resetPosition)
        {
            if (rawImage == null || loopImage == null ||
                containerRect == null || rawImage.texture == null)
                return;

            Vector2 containerSize = containerRect.rect.size;
            if (containerSize.x <= 0f || containerSize.y <= 0f)
                return;

            float previousOffset = 0f;
            if (layoutReady && tileSize.x > 0f)
            {
                previousOffset = Mathf.Repeat(
                    -rawRect.anchoredPosition.x,
                    tileSize.x);
            }

            tileSize = CalculateTileSize(containerSize, rawImage.texture);
            lastContainerSize = containerSize;
            lastTexture = rawImage.texture;

            ConfigureTileRect(rawRect);
            ConfigureTileRect(loopRect);

            float offset = resetPosition || !layoutReady
                ? 0f
                : Mathf.Repeat(previousOffset, tileSize.x);

            rawRect.anchoredPosition = new Vector2(-offset, 0f);
            loopRect.anchoredPosition =
                new Vector2(tileSize.x - offset, 0f);
            layoutReady = true;
        }

        /// <summary>Calcula el tamaño de cada pieza para cubrir sin deformar la textura.</summary>
        private Vector2 CalculateTileSize(Vector2 parentSize, Texture texture)
        {
            float textureAspect = texture.width / (float)texture.height;
            float parentAspect = parentSize.x / parentSize.y;

            // Cover mantiene la proporcion y evita dejar franjas vacias.
            return textureAspect >= parentAspect
                ? new Vector2(parentSize.y * textureAspect, parentSize.y)
                : new Vector2(parentSize.x, parentSize.x / textureAspect);
        }

        /// <summary>Deja una pieza con el anclaje y el tamaño que le tocan.</summary>
        private void ConfigureTileRect(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = tileSize;
            rect.localScale = Vector3.one;
        }

        /// <summary>Devuelve la pieza al otro extremo cuando se sale del todo.</summary>
        private void WrapPosition(RectTransform rect)
        {
            if (rect == null || tileSize.x <= 0f)
                return;

            float position = rect.anchoredPosition.x;
            float cycleWidth = tileSize.x * 2f;

            while (position <= -tileSize.x)
                position += cycleWidth;

            while (position >= tileSize.x)
                position -= cycleWidth;

            rect.anchoredPosition = new Vector2(position, 0f);
        }

        /// <summary>Destruye la copia que creo este componente.</summary>
        private void OnDestroy()
        {
            if (loopImage != null && loopImage.gameObject != null)
                Destroy(loopImage.gameObject);
        }
    }
}
