using System;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Puntuación del Tetris de Nintendo: 40 / 100 / 300 / 1200 según cuántas
    /// líneas se hagan de golpe, multiplicado por el nivel actual.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        /// <summary>Puntos base por 1, 2, 3 y 4 líneas.</summary>
        private static readonly int[] LinePoints = { 40, 100, 300, 1200 };

        [SerializeField, Min(0)] private int softDropPointsPerCell = 1;
        [SerializeField, Min(0)] private int hardDropPointsPerCell = 2;

        public int Score { get; private set; }
        public int TotalLines { get; private set; }

        /// <summary>Puntaje total y puntos sumados en esta jugada.</summary>
        public event Action<int, int> ScoreChanged;
        public event Action<int> LinesChanged;

        /// <summary>Cuatro líneas de golpe: el "Tetris".</summary>
        public event Action TetrisScored;

        /// <summary>Suma los puntos de las lineas hechas, multiplicados por el nivel.</summary>
        public void AddLines(int lineCount, int level)
        {
            if (lineCount <= 0)
                return;

            int index = Mathf.Clamp(lineCount, 1, LinePoints.Length) - 1;
            int gained = LinePoints[index] * Mathf.Max(1, level);

            Score += gained;
            TotalLines += lineCount;

            ScoreChanged?.Invoke(Score, gained);
            LinesChanged?.Invoke(TotalLines);

            if (lineCount >= LinePoints.Length)
                TetrisScored?.Invoke();
        }

        /// <summary>Suma los puntos de bajar la pieza a mano.</summary>
        public void AddSoftDrop(int cells)
        {
            AddDropPoints(cells, softDropPointsPerCell);
        }

        /// <summary>Suma los puntos de tirar la pieza en picado.</summary>
        public void AddHardDrop(int cells)
        {
            AddDropPoints(cells, hardDropPointsPerCell);
        }

        /// <summary>Suma los puntos de una caida, sean del tipo que sean.</summary>
        private void AddDropPoints(int cells, int pointsPerCell)
        {
            if (cells <= 0 || pointsPerCell <= 0)
                return;

            int gained = cells * pointsPerCell;
            Score += gained;
            ScoreChanged?.Invoke(Score, gained);
        }

        /// <summary>Deja el marcador a cero para una partida nueva.</summary>
        public void ResetScore()
        {
            Score = 0;
            TotalLines = 0;
            ScoreChanged?.Invoke(Score, 0);
            LinesChanged?.Invoke(TotalLines);
        }
    }
}
