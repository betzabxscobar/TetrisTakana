using System;
using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// El nivel sube cada tantos puntos, y con el nivel la pila empuja mas
    /// seguido. Tambien decide cuantos tipos de ficha entran en juego.
    /// </summary>
    public class DifficultySystem : MonoBehaviour
    {
        [Header("Nivel")]
        [SerializeField] private ScoreManager scoreManager;
        [Tooltip("Puntos necesarios para cada nivel.")]
        [SerializeField, Min(1)] private int pointsPerLevel = 1000;
        [SerializeField, Min(1)] private int maximumLevel = 30;

        [Header("Fichas")]
        [SerializeField, Min(1)] private int startingBlockTypes = 5;
        [SerializeField, Min(1)] private int maximumBlockTypes = 7;
        [Tooltip("Cada cuantos niveles entra un tipo de ficha nuevo.")]
        [SerializeField, Min(1)] private int levelsPerNewType = 3;

        [Header("Ritmo de subida")]
        [Tooltip("Segundos entre filas nuevas en el nivel 1.")]
        [SerializeField, Min(0.2f)] private float baseRiseInterval = 6f;
        [Tooltip("Cuanto se acelera por nivel, en tanto por uno.")]
        [SerializeField, Min(0f)] private float speedUpPerLevel = 0.12f;
        [Tooltip("Por rapido que vaya, nunca baja de aqui.")]
        [SerializeField, Min(0.2f)] private float minimumRiseInterval = 1.2f;

        public int Level { get; private set; } = 1;

        public int BlockTypeCount => Mathf.Clamp(
            startingBlockTypes + (Level - 1) / Mathf.Max(1, levelsPerNewType),
            1,
            maximumBlockTypes);

        /// <summary>Segundos entre dos filas nuevas al nivel actual.</summary>
        public float RiseInterval => Mathf.Max(
            minimumRiseInterval,
            baseRiseInterval / (1f + (Level - 1) * speedUpPerLevel));

        public event Action<int> DifficultyChanged;

        /// <summary>Coge el marcador del propio objeto si no viene asignado.</summary>
        private void Awake()
        {
            scoreManager ??= GetComponent<ScoreManager>();
        }

        /// <summary>Se pone a escuchar los cambios de puntuacion.</summary>
        private void OnEnable()
        {
            if (scoreManager != null)
                scoreManager.ScoreChanged += HandleScoreChanged;
        }

        /// <summary>Deja de escuchar los cambios de puntuacion.</summary>
        private void OnDisable()
        {
            if (scoreManager != null)
                scoreManager.ScoreChanged -= HandleScoreChanged;
        }

        /// <summary>Sube de nivel cuando la puntuacion pasa el siguiente escalon.</summary>
        private void HandleScoreChanged(int score, int multiplier)
        {
            int nextLevel = Mathf.Clamp(
                score / Mathf.Max(1, pointsPerLevel) + 1,
                1,
                maximumLevel);

            if (nextLevel == Level)
                return;

            Level = nextLevel;
            DifficultyChanged?.Invoke(Level);
        }

        /// <summary>Vuelve al nivel 1 para una partida nueva.</summary>
        public void ResetDifficulty()
        {
            if (Level == 1)
                return;

            Level = 1;
            DifficultyChanged?.Invoke(Level);
        }

        /// <summary>
        /// Se mantiene por compatibilidad con MatchSystem, que avisa de cada
        /// limpieza. El nivel ya no depende de esto sino del marcador.
        /// </summary>
        public void NotifyClear(int amount)
        {
        }
    }
}
