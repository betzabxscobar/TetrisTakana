using System;
using System.Collections.Generic;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Resultado almacenado en el ranking local del jugador.
    /// </summary>
    [Serializable]
    public sealed class HighScoreEntry
    {
        public int Score;
        public int TotalLines;
        public int Level;
        public long TimestampUtcTicks;
    }

    /// <summary>
    /// Mantiene y persiste las mejores puntuaciones entre escenas y sesiones.
    /// La puntuacion de la partida actual sigue perteneciendo a ScoreManager.
    /// </summary>
    public sealed class HighScoreManager : MonoBehaviour
    {
        private const string StorageKey = "TetrisTakana.HighScores.v1";
        public const int MaxEntries = 10;

        [Serializable]
        private sealed class SaveData
        {
            public List<HighScoreEntry> Entries = new List<HighScoreEntry>();
        }

        private static HighScoreManager instance;
        private readonly List<HighScoreEntry> entries = new List<HighScoreEntry>();

        public static HighScoreManager Instance => instance;
        public IReadOnlyList<HighScoreEntry> Entries => entries;

        public event Action ScoresChanged;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>
        /// Obtiene el gestor existente o crea uno si la escena se ha abierto
        /// directamente desde el editor.
        /// </summary>
        public static HighScoreManager EnsureInstance()
        {
            if (instance != null)
                return instance;

            HighScoreManager existing = FindAnyObjectByType<HighScoreManager>();

            if (existing != null)
                return existing;

            GameObject managerObject = new GameObject("High Score Manager");
            return managerObject.AddComponent<HighScoreManager>();
        }

        /// <summary>
        /// Registra un resultado y devuelve la entrada creada.
        /// </summary>
        public static HighScoreEntry SubmitScore(int score, int totalLines, int level)
        {
            return EnsureInstance().RecordScore(score, totalLines, level);
        }

        public HighScoreEntry RecordScore(int score, int totalLines, int level)
        {
            HighScoreEntry entry = new HighScoreEntry
            {
                Score = Mathf.Max(0, score),
                TotalLines = Mathf.Max(0, totalLines),
                Level = Mathf.Max(1, level),
                TimestampUtcTicks = DateTime.UtcNow.Ticks
            };

            entries.Add(entry);
            SortAndTrim();
            Save();
            ScoresChanged?.Invoke();
            return entry;
        }

        /// <summary>
        /// Borra el ranking local. Se deja publico para pruebas y depuracion.
        /// </summary>
        public void ClearScores()
        {
            entries.Clear();
            PlayerPrefs.DeleteKey(StorageKey);
            PlayerPrefs.Save();
            ScoresChanged?.Invoke();
        }

        private void Load()
        {
            entries.Clear();

            if (!PlayerPrefs.HasKey(StorageKey))
                return;

            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(
                    PlayerPrefs.GetString(StorageKey));

                if (data?.Entries != null)
                    entries.AddRange(data.Entries);

                SortAndTrim();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"No se pudo cargar el ranking: {exception.Message}",
                    this);
                entries.Clear();
            }
        }

        private void Save()
        {
            SaveData data = new SaveData { Entries = entries };
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void SortAndTrim()
        {
            entries.RemoveAll(entry => entry == null);
            entries.Sort(CompareEntries);

            if (entries.Count > MaxEntries)
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }

        private static int CompareEntries(HighScoreEntry first, HighScoreEntry second)
        {
            int result = second.Score.CompareTo(first.Score);

            if (result != 0)
                return result;

            result = second.TotalLines.CompareTo(first.TotalLines);

            if (result != 0)
                return result;

            return first.TimestampUtcTicks.CompareTo(second.TimestampUtcTicks);
        }
    }
}
