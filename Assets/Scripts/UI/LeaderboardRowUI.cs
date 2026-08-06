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

        /// <summary>Aplica una apariencia legible a los valores de la tabla.</summary>
        public void ApplyDisplayStyle(Font displayFont)
        {
            // La puntuacion es el dato principal: se lee primero y tiene el mayor peso visual.
            StyleValue(rankText, displayFont, 30, new Color(0.16f, 0.9f, 1f, 1f));
            StyleValue(scoreText, displayFont, 32, new Color(1f, 0.93f, 0.52f, 1f));
            StyleValue(linesText, displayFont, 30, new Color(0.58f, 1f, 0.76f, 1f));
            StyleValue(levelText, displayFont, 30, new Color(1f, 0.68f, 0.26f, 1f));
            StyleValue(dateText, displayFont, 18, new Color(0.86f, 0.91f, 1f, 1f));
        }

        /// <summary>Escribe en la fila los datos de una partida de la tabla.</summary>
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

        /// <summary>Deja la fila en blanco.</summary>
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

        /// <summary>Pasa la fecha guardada a algo legible.</summary>
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

        /// <summary>Evita que el ajuste automatico reduzca los datos de la fila.</summary>
        private static void StyleValue(Text value, Font displayFont, int fontSize, Color color)
        {
            if (value == null)
                return;

            if (displayFont != null)
                value.font = displayFont;

            value.fontSize = fontSize;
            // Press Start 2P ya tiene trazo grueso; simular negrita suaviza sus pixeles.
            value.fontStyle = FontStyle.Normal;
            value.resizeTextForBestFit = false;
            value.color = color;
            value.alignment = TextAnchor.MiddleCenter;
            value.alignByGeometry = true;
            value.supportRichText = false;
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            value.verticalOverflow = VerticalWrapMode.Overflow;

            Shadow shadow = value.GetComponent<Shadow>();
            if (shadow != null)
            {
                // Una sola sombra oscura mantiene el borde definido al escalar el canvas.
                shadow.effectColor = new Color(0.01f, 0.01f, 0.08f, 1f);
                shadow.effectDistance = new Vector2(2f, -2f);
                shadow.useGraphicAlpha = false;
            }

            Outline outline = value.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
        }
    }
}
