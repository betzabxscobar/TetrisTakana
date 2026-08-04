using System;
using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Representa una fila creada en la escena Puntuaciones.
    /// Unity controla el layout; este componente solo actualiza sus textos.
    /// </summary>
    public sealed class LeaderboardRowUI : MonoBehaviour
    {
        [SerializeField] private Text rankText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text linesText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text dateText;

        public void SetEntry(int rank, HighScoreEntry entry)
        {
            if (entry == null)
            {
                Clear();
                return;
            }

            if (rankText != null)
                rankText.text = rank.ToString("D2");

            if (scoreText != null)
                scoreText.text = entry.Score.ToString("N0");

            if (linesText != null)
                linesText.text = entry.TotalLines.ToString("N0");

            if (levelText != null)
                levelText.text = entry.Level.ToString("D2");

            if (dateText != null)
                dateText.text = FormatDate(entry.TimestampUtcTicks);
        }

        public void Clear()
        {
            if (rankText != null)
                rankText.text = string.Empty;

            if (scoreText != null)
                scoreText.text = string.Empty;

            if (linesText != null)
                linesText.text = string.Empty;

            if (levelText != null)
                levelText.text = string.Empty;

            if (dateText != null)
                dateText.text = string.Empty;
        }

        private static string FormatDate(long ticks)
        {
            if (ticks <= 0)
                return "--/--/----";

            try
            {
                return new DateTime(ticks, DateTimeKind.Utc)
                    .ToLocalTime()
                    .ToString("dd/MM/yyyy");
            }
            catch (ArgumentOutOfRangeException)
            {
                return "--/--/----";
            }
        }
    }
}
