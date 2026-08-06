using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Fija la resolución con la que arranca el juego (1920x1080 por defecto)
    /// y la degrada al escalón más grande que quepa en el monitor. Lo que el
    /// jugador elija después se recuerda entre sesiones.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ScreenSetup : MonoBehaviour
    {
        public const int DesignWidth = 1920;
        public const int DesignHeight = 1080;

        private const string WidthKey = "TetrisTakana.Screen.Width";
        private const string HeightKey = "TetrisTakana.Screen.Height";
        private const string ModeKey = "TetrisTakana.Screen.Mode";

        /// <summary>Resoluciones 16:9 admitidas, de mayor a menor.</summary>
        private static readonly Vector2Int[] SupportedResolutions =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(1600, 900),
            new Vector2Int(1366, 768),
            new Vector2Int(1280, 720),
            new Vector2Int(1024, 576)
        };

        [SerializeField] private bool applyOnAwake = true;
        [SerializeField] private bool rememberPlayerChoice = true;
        [SerializeField, Min(0)] private int targetFrameRate = 60;

        private static bool resolutionApplied;

        public static Vector2Int DesignResolution =>
            new Vector2Int(DesignWidth, DesignHeight);

        /// <summary>Aplica la configuracion de pantalla al arrancar.</summary>
        private void Awake()
        {
            if (applyOnAwake)
                Apply();
        }

        /// <summary>Fija fotogramas por segundo, modo de ventana y resolucion.</summary>
        public void Apply()
        {
            if (targetFrameRate > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = targetFrameRate;
            }

            // Solo la primera escena de la sesión toca la ventana: recolocarla
            // en cada cambio de escena haría parpadear la pantalla.
            if (resolutionApplied)
                return;

            resolutionApplied = true;
            ApplyStartResolution();
        }

        /// <summary>
        /// Cambia la resolución y la recuerda. Pensado para el menú de opciones.
        /// </summary>
        public static void ApplyResolution(int width, int height, FullScreenMode mode)
        {
            if (width <= 0 || height <= 0)
                return;

            Screen.SetResolution(width, height, mode);

            PlayerPrefs.SetInt(WidthKey, width);
            PlayerPrefs.SetInt(HeightKey, height);
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            PlayerPrefs.Save();
        }

        /// <summary>Olvida la resolucion guardada y vuelve a la de fabrica.</summary>
        public static void ClearSavedResolution()
        {
            PlayerPrefs.DeleteKey(WidthKey);
            PlayerPrefs.DeleteKey(HeightKey);
            PlayerPrefs.DeleteKey(ModeKey);
            PlayerPrefs.Save();
        }

        /// <summary>Elige con que resolucion arranca el juego fuera del editor.</summary>
        private void ApplyStartResolution()
        {
            // En el editor manda el tamaño elegido en la ventana Game.
            if (Application.isEditor)
                return;

            if (rememberPlayerChoice &&
                PlayerPrefs.HasKey(WidthKey) &&
                PlayerPrefs.HasKey(HeightKey))
            {
                Screen.SetResolution(
                    PlayerPrefs.GetInt(WidthKey),
                    PlayerPrefs.GetInt(HeightKey),
                    (FullScreenMode)PlayerPrefs.GetInt(
                        ModeKey,
                        (int)FullScreenMode.Windowed));
                return;
            }

            Vector2Int resolution = PickStartResolution();

            // Si la resolución llena el monitor, una ventana con bordes se
            // saldría de la pantalla: se usa pantalla completa sin bordes.
            bool fillsDisplay =
                resolution.x >= Display.main.systemWidth ||
                resolution.y >= Display.main.systemHeight;

            Screen.SetResolution(
                resolution.x,
                resolution.y,
                fillsDisplay
                    ? FullScreenMode.FullScreenWindow
                    : FullScreenMode.Windowed);
        }

        /// <summary>Busca la mayor resolucion de diseño que quepa en el monitor.</summary>
        private static Vector2Int PickStartResolution()
        {
            int maxWidth = Display.main.systemWidth;
            int maxHeight = Display.main.systemHeight;

            foreach (Vector2Int candidate in SupportedResolutions)
                if (candidate.x <= maxWidth && candidate.y <= maxHeight)
                    return candidate;

            return new Vector2Int(maxWidth, maxHeight);
        }
    }
}
