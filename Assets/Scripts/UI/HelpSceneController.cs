using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Da funcionalidad a los botones de la escena de ayuda. Los dos estan
    /// dibujados de forma distinta: "Cerrar" es un sprite del mundo y "Salir"
    /// una imagen de UI, asi que cada uno se vuelve pulsable a su manera.
    /// Escape tambien vuelve al menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelpSceneController : MonoBehaviour
    {
        [Header("Escenas")]
        [SerializeField] private string menuScene = "Menu";

        [Header("Botones")]
        [Tooltip("Vuelve al menu principal.")]
        [SerializeField] private string closeObjectName = "Cerrar_0";
        [Tooltip("Cierra el juego.")]
        [SerializeField] private string quitObjectName = "Salir";

        [Header("Sonido")]
        [SerializeField] private AudioClip clickSfx;

        private Camera worldCamera;
        private Collider2D closeCollider;
        private Button quitButton;
        private Button closeButton;
        private bool leaving;

        private void Awake()
        {
            worldCamera = Camera.main;

            SetUpButton(closeObjectName, ReturnToMenu, ref closeButton, ref closeCollider);

            Collider2D quitCollider = null;
            SetUpButton(quitObjectName, QuitGame, ref quitButton, ref quitCollider);
        }

        private void Update()
        {
            if (leaving)
                return;

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null &&
                (keyboard.escapeKey.wasPressedThisFrame ||
                 keyboard.backspaceKey.wasPressedThisFrame))
            {
                ReturnToMenu();
                return;
            }

            HandleWorldClick();
        }

        /// <summary>
        /// Deja el objeto listo para recibir clics: si es UI le pone un Button,
        /// y si es un sprite del mundo le pone un collider que luego se prueba
        /// contra el puntero.
        /// </summary>
        private void SetUpButton(
            string objectName,
            UnityEngine.Events.UnityAction action,
            ref Button button,
            ref Collider2D collider)
        {
            GameObject target = FindByName(objectName);

            if (target == null)
            {
                Debug.LogWarning(
                    $"No se encontro el objeto '{objectName}' en la escena de ayuda.",
                    this);
                return;
            }

            if (target.GetComponent<Graphic>() != null)
            {
                button = target.GetComponent<Button>();

                if (button == null)
                    button = target.AddComponent<Button>();

                button.onClick.AddListener(action);
                return;
            }

            if (target.GetComponent<SpriteRenderer>() == null)
            {
                Debug.LogWarning(
                    $"'{objectName}' no es ni UI ni sprite; no se puede pulsar.",
                    target);
                return;
            }

            collider = target.GetComponent<Collider2D>();

            if (collider == null)
            {
                // El BoxCollider2D se ajusta solo al alto y ancho del sprite.
                collider = target.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = true;
        }

        private void HandleWorldClick()
        {
            if (closeCollider == null)
                return;

            Mouse mouse = Mouse.current;

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            worldCamera ??= Camera.main;

            if (worldCamera == null)
                return;

            Vector3 screenPoint = mouse.position.ReadValue();
            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(screenPoint);

            if (closeCollider.OverlapPoint(worldPoint))
                ReturnToMenu();
        }

        private static GameObject FindByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;

            // Se recorre todo porque GameObject.Find ignora los desactivados.
            Transform[] all = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Transform candidate in all)
                if (candidate.name == objectName)
                    return candidate.gameObject;

            return null;
        }

        private void ReturnToMenu()
        {
            if (leaving)
                return;

            PlayClick();

            if (string.IsNullOrWhiteSpace(menuScene) ||
                !Application.CanStreamedLevelBeLoaded(menuScene))
            {
                Debug.LogError(
                    $"No se puede volver a '{menuScene}'. " +
                    "Comprueba que este en Build Settings.",
                    this);
                return;
            }

            leaving = true;
            SceneManager.LoadScene(menuScene);
        }

        private void QuitGame()
        {
            PlayClick();
            leaving = true;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void PlayClick()
        {
            if (clickSfx == null)
                return;

            AudioManager audioManager = AudioManager.Instance;

            if (audioManager != null)
                audioManager.PlaySfx(clickSfx);
        }
    }
}
