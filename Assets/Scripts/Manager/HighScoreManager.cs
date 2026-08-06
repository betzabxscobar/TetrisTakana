using System;
using System.Collections.Generic;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Resultado de una partida. Es lo que se guarda en el ranking local y lo
    /// que viaja a la tabla game_sessions del ranking global, asi que lleva
    /// todo lo que la fila de alla necesita.
    /// </summary>
    [Serializable]
    public sealed class HighScoreEntry
    {
        public GameMode Mode;
        public int Score;
        public int TotalLines;
        public int Level;

        /// <summary>Cuando termino la partida; el ended_at de la fila remota.</summary>
        public long TimestampUtcTicks;

        /// <summary>Lo que duro jugada, sin contar pausas.</summary>
        public int DurationSeconds;

        /// <summary>
        /// Version del juego con la que se hizo. Sirve para poder separar
        /// marcas de antes y despues de un cambio de equilibrio, que si no se
        /// comparan puntuaciones de dos juegos distintos.
        /// </summary>
        public string GameVersion;

        /// <summary>Sigue pendiente de subir al ranking global.</summary>
        public bool PendingUpload;
    }

    /// <summary>
    /// Mantiene y persiste las mejores puntuaciones entre escenas y sesiones.
    /// La puntuacion de la partida actual sigue perteneciendo a ScoreManager.
    /// </summary>
    public sealed class HighScoreManager : MonoBehaviour
    {
        // v2: las entradas de la v1 no traen modo ni duracion, y darlas todas
        // por Tetris ensuciaria el ranking nuevo para siempre. La clave vieja
        // se queda en las preferencias sin tocar, por si hubiera que mirarla.
        private const string StorageKey = "TetrisTakana.HighScores.v2";

        /// <summary>Cuantas marcas se guardan de cada modo.</summary>
        public const int MaxEntries = 10;

        /// <summary>
        /// Tope duro de la lista entera. Los resultados sin subir no se
        /// recortan aunque se caigan del top, que si no una racha de partidas
        /// flojas sin conexion los perderia antes de llegar al ranking; pero
        /// sin este tope, un jugador siempre desconectado la haria crecer sin
        /// fin dentro de las preferencias.
        /// </summary>
        private const int HardLimit = 100;

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
        public static HighScoreEntry SubmitScore(
            GameMode mode,
            int score,
            int totalLines,
            int level,
            float durationSeconds)
        {
            return EnsureInstance()
                .RecordScore(mode, score, totalLines, level, durationSeconds);
        }

        /// <summary>Apunta una partida en la tabla y la guarda en disco.</summary>
        public HighScoreEntry RecordScore(
            GameMode mode,
            int score,
            int totalLines,
            int level,
            float durationSeconds)
        {
            HighScoreEntry entry = new HighScoreEntry
            {
                Mode = mode,
                Score = Mathf.Max(0, score),

                // El match-3 no hace lineas, y la fila remota rechaza las que
                // lleguen con ese modo. Se limpia aqui y no alli para que el
                // rechazo no se descubra al subir, con la partida ya jugada.
                TotalLines = mode == GameMode.Tetris ? Mathf.Max(0, totalLines) : 0,
                Level = Mathf.Clamp(level, 1, 99),
                TimestampUtcTicks = DateTime.UtcNow.Ticks,
                DurationSeconds = Mathf.Clamp(
                    Mathf.RoundToInt(durationSeconds),
                    0,
                    86400),
                GameVersion = Application.version,
                PendingUpload = true
            };

            entries.Add(entry);
            SortAndTrim();
            Save();
            ScoresChanged?.Invoke();
            return entry;
        }

        /// <summary>Las mejores partidas de un modo, de mejor a peor.</summary>
        public List<HighScoreEntry> EntriesFor(GameMode mode)
        {
            List<HighScoreEntry> result = new List<HighScoreEntry>();

            foreach (HighScoreEntry entry in entries)
                if (entry.Mode == mode && result.Count < MaxEntries)
                    result.Add(entry);

            return result;
        }

        /// <summary>
        /// Los resultados que todavia no han llegado al ranking global. Los
        /// recorre el sincronizador cuando hay conexion.
        /// </summary>
        public List<HighScoreEntry> PendingUploads()
        {
            List<HighScoreEntry> result = new List<HighScoreEntry>();

            foreach (HighScoreEntry entry in entries)
                if (entry.PendingUpload)
                    result.Add(entry);

            return result;
        }

        /// <summary>
        /// Da un resultado por subido. A partir de aqui ya puede recortarse si
        /// se cae del top, que la copia buena vive en la base de datos.
        /// </summary>
        public void MarkUploaded(HighScoreEntry entry)
        {
            if (entry == null || !entry.PendingUpload)
                return;

            entry.PendingUpload = false;
            SortAndTrim();
            Save();
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

        /// <summary>
        /// Ordena de mejor a peor y recorta. El recorte cuenta por modo: las
        /// marcas del Tetris y las del match-3 no compiten entre si, y una
        /// buena tarde de un modo no puede vaciar el ranking del otro.
        /// </summary>
        private void SortAndTrim()
        {
            entries.RemoveAll(entry => entry == null);
            entries.Sort(CompareEntries);

            int tetris = 0;
            int match3 = 0;

            for (int index = 0; index < entries.Count; index++)
            {
                HighScoreEntry entry = entries[index];
                int kept = entry.Mode == GameMode.Match3 ? ++match3 : ++tetris;

                if (kept <= MaxEntries || entry.PendingUpload)
                    continue;

                entries.RemoveAt(index--);
            }

            if (entries.Count > HardLimit)
                entries.RemoveRange(HardLimit, entries.Count - HardLimit);
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
