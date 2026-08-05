using System;
using UnityEngine;

namespace TetrisTakana.Match3
{
    public class ScoreManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private int pointsPerBlock = 10;

        public int Score { get; private set; }
        public event Action<int, int> ScoreChanged;

        public void AddMatch(int blockCount, int combo)
        {
            int multiplier = Mathf.Max(1, combo);
            Score += Mathf.Max(0, blockCount) * pointsPerBlock * multiplier;
            ScoreChanged?.Invoke(Score, multiplier);
        }

        public void ResetScore()
        {
            Score = 0;
            ScoreChanged?.Invoke(Score, 1);
        }
    }
}
