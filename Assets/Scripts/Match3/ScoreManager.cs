using System;
using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>El marcador del modo match-3: puntos por ficha, multiplicados por el combo.</summary>
    public class ScoreManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private int pointsPerBlock = 10;

        public int Score { get; private set; }
        public event Action<int, int> ScoreChanged;

        /// <summary>Suma los puntos de una combinacion segun cuantas fichas y que combo.</summary>
        public void AddMatch(int blockCount, int combo)
        {
            int multiplier = Mathf.Max(1, combo);
            Score += Mathf.Max(0, blockCount) * pointsPerBlock * multiplier;
            ScoreChanged?.Invoke(Score, multiplier);
        }

        /// <summary>Deja el marcador a cero para una partida nueva.</summary>
        public void ResetScore()
        {
            Score = 0;
            ScoreChanged?.Invoke(Score, 1);
        }
    }
}
