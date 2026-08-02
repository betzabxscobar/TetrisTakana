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
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button scoresButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Escenas")]
        [SerializeField] private string gameScene = "Game";
        [SerializeField] private string creditsScene = "Credits";

        [Header("Paneles opcionales")]
        [SerializeField] private GameObject scoresPanel;
        [SerializeField] private GameObject settingsPanel;

        private void Start()
        {
            Bind(ref playButton, "Btn Jugar", LoadGame);
            Bind(ref creditsButton, "BtnAyuda", LoadCredits);
            Bind(ref scoresButton, "BtnPuntuaciones", () => TogglePanel(scoresPanel));
            Bind(ref settingsButton, "BtnConfiguracion", () => TogglePanel(settingsPanel));
            Bind(ref quitButton, "BtnSalir", ExitGame);
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

        private void LoadGame()
        {
            SceneManager.LoadScene(gameScene);
        }

        private void LoadCredits()
        {
            SceneManager.LoadScene(creditsScene);
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
