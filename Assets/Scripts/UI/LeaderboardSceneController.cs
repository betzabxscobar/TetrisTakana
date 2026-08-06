using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Actualiza los objetos UI que ya existen en la escena Puntuaciones.
    /// El layout y el aspecto se editan directamente desde Unity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardSceneController : MonoBehaviour
    {
        [Header("Escena")]
        [SerializeField] private string menuScene = "Menu";

        [Header("Objetos de Unity")]
        [SerializeField] private LeaderboardRowUI[] rows;
        [SerializeField] private GameObject emptyMessage;
        [SerializeField] private Button backButton;

        private HighScoreManager scoreManager;

        /// <summary>Recoge las filas de la tabla que trae la escena.</summary>
        private void Awake()
        {
            ResolveReferences();
        }

        /// <summary>Se suscribe al gestor de puntuaciones y pinta la tabla.</summary>
        private void OnEnable()
        {
            ResolveReferences();
            scoreManager = HighScoreManager.EnsureInstance();
            scoreManager.ScoresChanged += Refresh;

            if (backButton != null)
                backButton.onClick.AddListener(ReturnToMenu);

            Refresh();
        }

        /// <summary>Deja de escuchar los cambios de la tabla.</summary>
        private void OnDisable()
        {
            if (scoreManager != null)
                scoreManager.ScoresChanged -= Refresh;

            if (backButton != null)
                backButton.onClick.RemoveListener(ReturnToMenu);
        }

        /// <summary>Busca las filas y el boton de volver si no vienen asignados.</summary>
        private void ResolveReferences()
        {
            if (rows == null || rows.Length == 0)
            {
                List<LeaderboardRowUI> foundRows = new List<LeaderboardRowUI>(
                    FindObjectsByType<LeaderboardRowUI>(FindObjectsInactive.Include));
                foundRows.Sort((first, second) =>
                    string.CompareOrdinal(first.name, second.name));
                rows = foundRows.ToArray();
            }

            if (emptyMessage == null)
            {
                GameObject messageObject = GameObject.Find("EmptyMessage");
                emptyMessage = messageObject;
            }

            if (backButton == null)
            {
                GameObject buttonObject = GameObject.Find("BackButton");
                if (buttonObject != null)
                    backButton = buttonObject.GetComponent<Button>();
            }
        }

        /// <summary>Vuelca las mejores partidas en las filas de la tabla.</summary>
        private void Refresh()
        {
            if (scoreManager == null || rows == null)
                return;

            IReadOnlyList<HighScoreEntry> entries = scoreManager.Entries;
            bool hasEntries = entries.Count > 0;

            if (emptyMessage != null)
                emptyMessage.SetActive(!hasEntries);

            for (int index = 0; index < rows.Length; index++)
            {
                LeaderboardRowUI row = rows[index];

                if (row == null)
                    continue;

                bool visible = index < entries.Count;
                row.gameObject.SetActive(visible);

                if (visible)
                    row.SetEntry(index + 1, entries[index]);
                else
                    row.Clear();
            }
        }

        /// <summary>Vuelve al menu principal.</summary>
        private void ReturnToMenu()
        {
            SceneManager.LoadScene(menuScene);
        }
    }
}
