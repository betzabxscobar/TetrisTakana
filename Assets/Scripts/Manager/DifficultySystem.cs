using System;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Sube de nivel cada N líneas y ajusta la velocidad de caída siguiendo la
    /// tabla original de Nintendo, expresada en fotogramas por celda a 60 fps.
    /// </summary>
    public class DifficultySystem : MonoBehaviour
    {
        /// <summary>
        /// Fotogramas que tarda la pieza en bajar una celda, por nivel. A
        /// partir del último valor se queda en 1 (velocidad máxima).
        /// </summary>
        private static readonly int[] FramesPerCell =
        {
            48, 43, 38, 33, 28, 23, 18, 13, 8, 6,
            5, 5, 5, 4, 4, 4, 3, 3, 3,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            1
        };

        private const float FrameDuration = 1f / 60f;

        [SerializeField, Min(1)] private int startingLevel = 1;
        [SerializeField, Min(1)] private int linesPerLevel = 10;
        [SerializeField, Min(1)] private int maximumLevel = 30;

        public int Level { get; private set; } = 1;
        public int ClearedLines { get; private set; }

        /// <summary>Segundos que tarda la pieza en bajar una celda.</summary>
        public float FallInterval => FramesPerCell[
            Mathf.Clamp(Level - 1, 0, FramesPerCell.Length - 1)] * FrameDuration;

        public event Action<int> LevelChanged;

        /// <summary>Arranca en el nivel inicial configurado.</summary>
        private void Awake()
        {
            Level = startingLevel;
        }

        /// <summary>Suma las lineas hechas y sube de nivel cuando toca.</summary>
        public void NotifyLinesCleared(int amount)
        {
            if (amount <= 0)
                return;

            ClearedLines += amount;

            int nextLevel = Mathf.Clamp(
                startingLevel + ClearedLines / Mathf.Max(1, linesPerLevel),
                startingLevel,
                maximumLevel);

            if (nextLevel == Level)
                return;

            Level = nextLevel;
            LevelChanged?.Invoke(Level);
        }

        /// <summary>Vuelve al nivel y al recuento del principio.</summary>
        public void ResetDifficulty()
        {
            ClearedLines = 0;
            Level = startingLevel;
            LevelChanged?.Invoke(Level);
        }
    }
}
