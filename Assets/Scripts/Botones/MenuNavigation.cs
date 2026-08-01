using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TetrisTakana
{
    public class MenuNavigation : MonoBehaviour
    {
        private void Start()
        {
            AddListener("Btn Jugar", LoadGame);
            AddListener("BtnAyuda", LoadCredits);
            AddListener("BtnSalir", ExitGame);
        }

        private static void AddListener(string objectName, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = GameObject.Find(objectName);
            Button button = buttonObject != null ? buttonObject.GetComponent<Button>() : null;
            if (button != null)
                button.onClick.AddListener(action);
        }

        private static void LoadGame() { SceneManager.LoadScene("Game"); }
        private static void LoadCredits() { SceneManager.LoadScene("Credits"); }
        private static void ExitGame() { Application.Quit(); }
    }
}
