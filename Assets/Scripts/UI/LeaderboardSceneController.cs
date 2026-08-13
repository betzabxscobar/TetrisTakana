using System.Collections.Generic;
using UnityEngine;
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

        [Header("Legibilidad")]
        [Tooltip("Fuente pixel usada por toda la tabla.")]
        [SerializeField] private Font tableFont;
        [SerializeField] private Text[] headerLabels;

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

            if (rows == null)
                return;

            foreach (LeaderboardRowUI row in rows)
            {
                if (row != null)
                    row.ApplyDisplayStyle(tableFont);
            }

            if (headerLabels == null || headerLabels.Length == 0)
            {
                GameObject headerRow = GameObject.Find("HeaderRow");
                if (headerRow != null)
                    headerLabels = headerRow.GetComponentsInChildren<Text>(true);
            }

            if (headerLabels != null)
            {
                foreach (Text label in headerLabels)
                    ApplyHeaderStyle(label);
            }
        }

        /// <summary>Mantiene los rotulos pequeños, nitidos y dentro del mismo lenguaje pixel.</summary>
        private void ApplyHeaderStyle(Text label)
        {
            if (label == null)
                return;

            if (tableFont != null)
                label.font = tableFont;

            label.fontSize = 18;
            label.fontStyle = FontStyle.Normal;
            label.resizeTextForBestFit = false;
            label.alignment = TextAnchor.MiddleCenter;
            label.alignByGeometry = true;
            label.supportRichText = false;
            label.color = new Color(0.84f, 0.9f, 1f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            Shadow shadow = label.GetComponent<Shadow>();
            if (shadow != null)
            {
                shadow.effectColor = new Color(0.01f, 0.01f, 0.08f, 1f);
                shadow.effectDistance = new Vector2(1f, -1f);
                shadow.useGraphicAlpha = false;
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
            SceneTransitionManager.LoadScene(menuScene);
        }
    }
}
