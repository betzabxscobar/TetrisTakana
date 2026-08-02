using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Encuadra la cámara ortográfica sobre el tablero para que se vea entero
    /// en cualquier relación de aspecto, desde 4:3 hasta ultrapanorámico.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class BoardCameraFitter : MonoBehaviour
    {
        [SerializeField] private Board board;

        [Header("Margen alrededor del tablero (unidades de mundo)")]
        [SerializeField, Min(0f)] private float horizontalPadding = 1.5f;
        [SerializeField, Min(0f)] private float verticalPadding = 0.75f;

        [Header("Encuadre")]
        [Tooltip("Desplaza el centro del encuadre; útil para dejar hueco al HUD.")]
        [SerializeField] private Vector2 focusOffset = Vector2.zero;
        [SerializeField, Min(0.1f)] private float minimumSize = 1f;

        private Camera targetCamera;
        private int lastScreenWidth;
        private int lastScreenHeight;

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();

            if (board == null)
                board = FindAnyObjectByType<Board>();

            Fit();
        }

        private void OnValidate()
        {
            targetCamera = GetComponent<Camera>();
            Fit();
        }

        private void Update()
        {
            // Recalcular solo cuando cambia el tamaño de la ventana evita
            // trabajo inútil cada frame.
            if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight)
                return;

            Fit();
        }

        [ContextMenu("Encuadrar tablero")]
        public void Fit()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();

            if (targetCamera == null || board == null)
                return;

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            Transform boardTransform = board.transform;
            Vector3 bottomLeft = boardTransform.TransformPoint(Vector3.zero);
            Vector3 topRight = boardTransform.TransformPoint(new Vector3(
                board.Width * board.CellSize,
                board.Height * board.CellSize,
                0f));

            Vector3 center = (bottomLeft + topRight) * 0.5f;
            float boardWidth = Mathf.Abs(topRight.x - bottomLeft.x) + horizontalPadding * 2f;
            float boardHeight = Mathf.Abs(topRight.y - bottomLeft.y) + verticalPadding * 2f;

            float aspect = targetCamera.aspect;

            if (aspect <= 0f)
                aspect = lastScreenHeight > 0
                    ? (float)lastScreenWidth / lastScreenHeight
                    : 16f / 9f;

            // El lado que primero se queda corto manda: así el tablero entra
            // completo tanto en pantallas anchas como en pantallas altas.
            float size = Mathf.Max(
                boardHeight * 0.5f,
                boardWidth * 0.5f / aspect);

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = Mathf.Max(minimumSize, size);

            transform.position = new Vector3(
                center.x + focusOffset.x,
                center.y + focusOffset.y,
                transform.position.z);
        }
    }
}
