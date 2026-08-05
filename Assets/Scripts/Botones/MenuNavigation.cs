using UnityEngine;
using UnityEngine.SceneManagement;
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

        [Header("Paneles opcionales")]
        [SerializeField] private GameObject scoresPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Ayuda")]
        [SerializeField] private GameObject helpCardPrefab;
        [SerializeField] private Transform helpCardParent;

        private HelpCardController helpCardController;

        private void Awake()
        {
            CreateHelpCard();
        }

        private void Start()
        {
            Bind(ref playButton, "Btn Jugar", LoadGame);
            Bind(ref helpButton, "BtnAyuda", ToggleHelpCard);
            Bind(ref creditsButton, "BtnCredito", LoadCredits);
            Bind(ref scoresButton, "BtnPuntuaciones", LoadScores);
            Bind(ref settingsButton, "BtnConfiguracion", () => TogglePanel(settingsPanel));
            Bind(ref quitButton, "BtnSalir", ExitGame);
        }

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

        private static void Bind(
            ref Button button,
            string fallbackName,
            UnityEngine.Events.UnityAction action)
        {
            button ??= FindButton(fallbackName);

            if (button == null)
            {
                Debug.LogWarning($"No se encontró el botón '{fallbackName}' en el menú.");
                return;
            }

            button.onClick.AddListener(action);
        }

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

        private void TogglePanel(GameObject panel)
        {
            if (panel == null)
                return;

            panel.SetActive(!panel.activeSelf);
        }

        private void ToggleHelpCard()
        {
            if (helpCardController == null)
                CreateHelpCard();

            if (helpCardController != null)
                helpCardController.AlternarTarjeta();
        }

        private void LoadGame()
        {
            SceneManager.LoadScene(gameScene);
        }

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

            SceneManager.LoadScene(creditsScene);
        }

        private void LoadScores()
        {
            SceneManager.LoadScene(scoresScene);
        }

        private static Transform FindTransform(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            return target != null ? target.transform : null;
        }

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
