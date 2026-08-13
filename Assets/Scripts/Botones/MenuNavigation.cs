using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Conecta los botones del menú principal. Se pueden asignar en el
    /// inspector; si no, se buscan por nombre incluyendo objetos desactivados.
    /// </summary>
    public class MenuNavigation : MonoBehaviour
    {
        [Header("Botones")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button helpButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button scoresButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Escenas")]
        [SerializeField] private string gameScene = "Game";
        [SerializeField] private string creditsScene = "Credits";
        [SerializeField] private string scoresScene = "Puntuaciones";
        [SerializeField] private string helpScene = "Ayuda";

        [Header("Paneles opcionales")]
        [SerializeField] private GameObject scoresPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Ayuda")]
        [SerializeField] private GameObject helpCardPrefab;
        [SerializeField] private Transform helpCardParent;

        private HelpCardController helpCardController;

        /// <summary>Prepara la tarjeta de ayuda antes de que arranque la escena.</summary>
        private void Awake()
        {
            CreateHelpCard();
        }

        /// <summary>Engancha cada boton del menu con lo que tiene que hacer.</summary>
        private void Start()
        {
            Bind(ref playButton, "Btn Jugar", LoadGame);
            Bind(ref helpButton, "BtnAyuda", LoadHelp);
            Bind(ref creditsButton, "BtnCredito", LoadCredits);
            Bind(ref scoresButton, "BtnPuntuaciones", LoadScores);
            Bind(ref settingsButton, "BtnConfiguracion", () => TogglePanel(settingsPanel), true);
            Bind(ref quitButton, "BtnSalir", ExitGame);
        }

        /// <summary>Instancia la tarjeta de ayuda a partir de su prefab.</summary>
        private void CreateHelpCard()
        {
            if (helpCardController != null || helpCardPrefab == null)
                return;

            Transform parent = helpCardParent != null
                ? helpCardParent
                : FindTransform("UI");
            GameObject instance = parent != null
                ? Instantiate(helpCardPrefab, parent, false)
                : Instantiate(helpCardPrefab);

            helpCardController = instance.GetComponentInChildren<HelpCardController>(true);

            if (helpCardController == null)
            {
                Debug.LogError(
                    "El prefab de ayuda no contiene un HelpCardController.",
                    instance);
                Destroy(instance);
            }
        }

        /// <summary>Busca el boton si no esta asignado y le engancha su accion.</summary>
        private static void Bind(
            ref Button button,
            string fallbackName,
            UnityEngine.Events.UnityAction action,
            bool optional = false)
        {
            button ??= FindButton(fallbackName);

            if (button == null)
            {
                if (!optional)
                    Debug.LogWarning($"No se encontró el botón '{fallbackName}' en el menú.");
                return;
            }

            button.onClick.AddListener(action);
        }

        /// <summary>Busca un boton por nombre, incluso si esta desactivado.</summary>
        private static Button FindButton(string objectName)
        {
            // GameObject.Find ignora los objetos desactivados, por eso se
            // recorre la lista completa de botones de la escena.
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);

            foreach (Button button in buttons)
                if (button.name == objectName)
                    return button;

            return null;
        }

        /// <summary>Enseña o esconde un panel.</summary>
        private void TogglePanel(GameObject panel)
        {
            if (panel == null)
                return;

            panel.SetActive(!panel.activeSelf);
        }

        /// <summary>Abre o cierra la tarjeta de ayuda.</summary>
        private void ToggleHelpCard()
        {
            if (helpCardController == null)
                CreateHelpCard();

            if (helpCardController != null)
                helpCardController.AlternarTarjeta();
        }

        /// <summary>Va a la escena de juego.</summary>
        private void LoadGame()
        {
            SceneTransitionManager.LoadScene(gameScene);
        }

        /// <summary>Va a los creditos, si la escena esta incluida en el build.</summary>
        private void LoadCredits()
        {
            if (string.IsNullOrWhiteSpace(creditsScene) ||
                !Application.CanStreamedLevelBeLoaded(creditsScene))
            {
                Debug.LogError(
                    $"No se puede abrir la escena de creditos '{creditsScene}'. " +
                    "Comprueba que este incluida en Build Settings.",
                    this);
                return;
            }

            SceneTransitionManager.LoadScene(creditsScene);
        }

        /// <summary>Va a la ayuda, si la escena esta incluida en el build.</summary>
        private void LoadHelp()
        {
            if (string.IsNullOrWhiteSpace(helpScene) ||
                !Application.CanStreamedLevelBeLoaded(helpScene))
            {
                // La tarjeta del menú sigue sirviendo de respaldo si la escena
                // no está en Build Settings.
                Debug.LogWarning(
                    $"No se puede abrir la escena de ayuda '{helpScene}'; " +
                    "se muestra la tarjeta del menú.",
                    this);
                ToggleHelpCard();
                return;
            }

            SceneTransitionManager.LoadScene(helpScene);
        }

        /// <summary>Va a la tabla de puntuaciones.</summary>
        private void LoadScores()
        {
            SceneTransitionManager.LoadScene(scoresScene);
        }

        /// <summary>Busca un objeto de la escena por su nombre.</summary>
        private static Transform FindTransform(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            return target != null ? target.transform : null;
        }

        /// <summary>Cierra el juego; en el editor solo detiene la ejecucion.</summary>
        private void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
