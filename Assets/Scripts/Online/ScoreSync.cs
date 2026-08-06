using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TetrisTakana.Online
{
    /// <summary>
    /// El puente entre la tabla local y el ranking global. Sube las partidas
    /// que quedan pendientes y, la primera vez que una entra en el ranking,
    /// pide el nombre del jugador.
    ///
    /// La tabla local sigue siendo la que manda para lo que el jugador ve al
    /// momento: se juega igual sin conexion, y lo que no pudo subirse se queda
    /// esperando a la proxima vez que el juego arranque con red.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreSync : MonoBehaviour
    {
        private static ScoreSync instance;

        private HighScoreManager scores;
        private bool syncing;

        // Si el jugador cierra el aviso sin poner nombre, no se le insiste en
        // toda la sesion: sus partidas se quedan pendientes y ya subiran.
        private bool nameRefused;

        /// <summary>Levanta el sincronizador al arrancar el juego.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
                return;

            new GameObject("Score Sync").AddComponent<ScoreSync>();
        }

        /// <summary>Deja una sola copia viva y se engancha a la tabla local.</summary>
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            scores = HighScoreManager.EnsureInstance();
            scores.ScoresChanged += HandleScoresChanged;
        }

        /// <summary>Deja de escuchar la tabla local.</summary>
        private void OnDestroy()
        {
            if (scores != null)
                scores.ScoresChanged -= HandleScoresChanged;

            if (instance == this)
                instance = null;
        }

        /// <summary>
        /// Al arrancar se intenta subir lo que quedo pendiente de otras veces,
        /// que es el caso de quien jugo sin conexion.
        /// </summary>
        private void Start()
        {
            TrySync();
        }

        /// <summary>Hay resultado nuevo en la tabla local.</summary>
        private void HandleScoresChanged()
        {
            TrySync();
        }

        /// <summary>Lanza la subida si no hay ya una en marcha.</summary>
        public void TrySync()
        {
            if (syncing || !isActiveAndEnabled)
                return;

            StartCoroutine(SyncRoutine());
        }

        /// <summary>
        /// Sube lo pendiente. Antes se asegura de que hay nombre, porque la
        /// fila remota apunta a la ficha del jugador y sin ella la rechaza la
        /// base de datos.
        /// </summary>
        private IEnumerator SyncRoutine()
        {
            syncing = true;

            try
            {
                SupabaseClient client = SupabaseClient.EnsureInstance();

                if (!client.IsConfigured)
                    yield break;

                List<HighScoreEntry> pending = scores.PendingUploads();

                if (pending.Count == 0)
                    yield break;

                if (string.IsNullOrEmpty(client.DisplayName))
                {
                    // Solo se pregunta si alguna de las pendientes esta en el
                    // ranking: quien acaba de probar el juego y ha hecho una
                    // partida floja no tiene por que darnos su nombre.
                    if (nameRefused || !AnyInTop(pending))
                        yield break;

                    yield return AskForName(client);

                    if (string.IsNullOrEmpty(client.DisplayName))
                        yield break;
                }

                foreach (HighScoreEntry entry in pending)
                {
                    bool uploaded = false;
                    string error = string.Empty;

                    yield return client.UploadSession(entry, (ok, message) =>
                    {
                        uploaded = ok;
                        error = message;
                    });

                    if (uploaded)
                    {
                        scores.MarkUploaded(entry);
                        continue;
                    }

                    // Al primer fallo se para: si es que no hay red, insistir
                    // con las demas solo suma esperas de quince segundos.
                    Debug.LogWarning($"No se pudo subir la partida: {error}", this);
                    yield break;
                }
            }
            finally
            {
                syncing = false;
            }
        }

        /// <summary>Pregunta el nombre y da de alta al jugador con el.</summary>
        private IEnumerator AskForName(SupabaseClient client)
        {
            string chosen = null;
            bool answered = false;

            NamePrompt prompt = NamePrompt.Ask(name =>
            {
                chosen = name;
                answered = true;
            });

            // Se espera a que conteste. Si se va al menu con el aviso abierto,
            // el aviso muere con la escena y la respuesta no llegaria nunca:
            // sin esta comprobacion la corrutina se quedaria colgada y no
            // volveria a intentarse ninguna subida en toda la partida.
            while (!answered)
            {
                if (prompt == null)
                    yield break;

                yield return null;
            }

            if (string.IsNullOrEmpty(chosen))
            {
                nameRefused = true;
                yield break;
            }

            yield return client.SetDisplayName(chosen, (ok, error) =>
            {
                if (!ok)
                    Debug.LogWarning($"No se pudo guardar el nombre: {error}", this);
            });
        }

        /// <summary>
        /// Dice si alguna de esas partidas esta entre las mejores de su modo.
        /// </summary>
        private bool AnyInTop(List<HighScoreEntry> pending)
        {
            foreach (HighScoreEntry entry in pending)
                if (scores.EntriesFor(entry.Mode).Contains(entry))
                    return true;

            return false;
        }
    }
}
