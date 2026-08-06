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

        /// <summary>Envoltorio que hace falta para guardar la lista como JSON.</summary>
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

        /// <summary>Deja una sola copia viva y carga la tabla guardada.</summary>
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

        /// <summary>Suelta la referencia global si el que se destruye es este.</summary>
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

        /// <summary>Apunta una partida en la tabla y la guarda en disco.</summary>
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

        /// <summary>Lee la tabla guardada en las preferencias del jugador.</summary>
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

        /// <summary>Escribe la tabla en las preferencias del jugador.</summary>
        private void Save()
        {
            SaveData data = new SaveData { Entries = entries };
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        /// <summary>Ordena de mayor a menor y se queda solo con las mejores.</summary>
        private void SortAndTrim()
        {
            entries.RemoveAll(entry => entry == null);
            entries.Sort(CompareEntries);

            if (entries.Count > MaxEntries)
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }

        /// <summary>Ordena por puntos y, si empatan, por lineas y nivel.</summary>
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
