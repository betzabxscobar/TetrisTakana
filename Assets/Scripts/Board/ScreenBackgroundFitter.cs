using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Escala un sprite de fondo para que cubra siempre la vista de la cámara,
    /// sin franjas negras, sea cual sea la relación de aspecto.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class ScreenBackgroundFitter : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [Tooltip("Cubrir recorta los bordes; contener deja margen pero no recorta.")]
        [SerializeField] private bool cover = true;
        [SerializeField] private float distanceFromCamera = 20f;

        private SpriteRenderer spriteRenderer;
        private float lastAspect;
        private float lastSize;

        /// <summary>Ajusta el fondo nada mas activarse.</summary>
        private void OnEnable()
        {
            Fit();
        }

        /// <summary>Vuelve a ajustar al tocar valores en el inspector.</summary>
        private void OnValidate()
        {
            Fit();
        }

        /// <summary>Reajusta cuando cambia el tamaño de la ventana.</summary>
        private void LateUpdate()
        {
            if (targetCamera == null)
                return;

            // La cámara puede reencuadrarse al cambiar la ventana, así que se
            // vigilan aspecto y tamaño, no solo la resolución.
            if (Mathf.Approximately(targetCamera.aspect, lastAspect) &&
                Mathf.Approximately(targetCamera.orthographicSize, lastSize))
                return;

            Fit();
        }

        /// <summary>Escala el fondo hasta cubrir o caber en lo que ve la camara.</summary>
        [ContextMenu("Ajustar fondo a la cámara")]
        public void Fit()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (targetCamera == null || spriteRenderer == null || !targetCamera.orthographic)
                return;

            Sprite sprite = spriteRenderer.sprite;

            if (sprite == null)
                return;

            Vector2 spriteSize = sprite.bounds.size;

            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                return;

            lastAspect = targetCamera.aspect;
            lastSize = targetCamera.orthographicSize;

            float viewHeight = lastSize * 2f;
            float viewWidth = viewHeight * lastAspect;

            float scaleX = viewWidth / spriteSize.x;
            float scaleY = viewHeight / spriteSize.y;
            float scale = cover
                ? Mathf.Max(scaleX, scaleY)
                : Mathf.Min(scaleX, scaleY);

            transform.localScale = new Vector3(scale, scale, 1f);
            transform.position = targetCamera.transform.position +
                                 targetCamera.transform.forward * distanceFromCamera;
        }
    }
}
