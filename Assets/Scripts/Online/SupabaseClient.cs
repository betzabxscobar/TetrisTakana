using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TetrisTakana.Online
{
    /// <summary>Una fila del ranking global tal y como llega del servidor.</summary>
    public sealed class LeaderboardRow
    {
        public int Rank;
        public string DisplayName;
        public int Score;
        public int Lines;
        public int Level;
        public DateTime EndedAtUtc;
    }

    /// <summary>
    /// Todo lo que el juego habla con Supabase: entrar, darse de alta, subir
    /// una partida y pedir el ranking. Va por HTTP con UnityWebRequest porque
    /// es lo unico que funciona en WebGL, donde no hay hilos ni sockets.
    ///
    /// La clave que usa es la publica; lo que protege los datos son las
    /// politicas RLS de la base. Por eso aqui no hay nada que ocultar y el
    /// cliente puede vivir dentro del build sin problema.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupabaseClient : MonoBehaviour
    {
        // La sesion se guarda entre partidas a proposito: Supabase limita las
        // altas anonimas a 30 por hora y por IP, y crear una nueva cada vez que
        // se abre el juego agotaria el cupo de toda una clase probando desde la
        // misma red. Ademas, asi el jugador conserva su identidad y su puesto.
        private const string RefreshTokenKey = "TetrisTakana.Supabase.RefreshToken";
        private const string UserIdKey = "TetrisTakana.Supabase.UserId";
        private const string DisplayNameKey = "TetrisTakana.Supabase.DisplayName";

        private static SupabaseClient instance;

        private SupabaseConfig config;
        private string accessToken;
        private double accessTokenExpiresAt;

        public static SupabaseClient Instance => instance;

        /// <summary>Hay proyecto y clave configurados.</summary>
        public bool IsConfigured => config != null && config.IsConfigured;

        /// <summary>Hay una sesion viva con la que escribir en la base.</summary>
        public bool IsSignedIn =>
            !string.IsNullOrEmpty(accessToken) &&
            !string.IsNullOrEmpty(UserId);

        /// <summary>El id del jugador en la base, o vacio si aun no ha entrado.</summary>
        public string UserId
        {
            get => PlayerPrefs.GetString(UserIdKey, string.Empty);
            private set => PlayerPrefs.SetString(UserIdKey, value ?? string.Empty);
        }

        /// <summary>El nombre con el que sale en el ranking.</summary>
        public string DisplayName
        {
            get => PlayerPrefs.GetString(DisplayNameKey, string.Empty);
            private set => PlayerPrefs.SetString(DisplayNameKey, value ?? string.Empty);
        }

        /// <summary>Deja una sola copia viva y carga la configuracion.</summary>
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            config = Resources.Load<SupabaseConfig>(SupabaseConfig.ResourceName);

            if (config == null)
                Debug.LogWarning(
                    "No hay SupabaseConfig en Resources: el ranking global se " +
                    "queda apagado y el juego sigue con la tabla local.",
                    this);
            else if (config.LooksLikeSecretKey)
                Debug.LogError(
                    "El SupabaseConfig lleva la clave secreta. Cambiala por la " +
                    "publishable antes de compartir ningun build.",
                    this);
        }

        /// <summary>Suelta la referencia global si el que se destruye es este.</summary>
        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>
        /// Levanta el cliente al arrancar el juego, sin tener que ponerlo en
        /// ninguna escena. Asi esta listo para reintentar los envios pendientes
        /// aunque el jugador no pase por la pantalla de puntuaciones, y no hay
        /// que acordarse de arrastrarlo a cada escena nueva.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        /// <summary>Devuelve el cliente, creandolo si todavia no existe.</summary>
        public static SupabaseClient EnsureInstance()
        {
            if (instance != null)
                return instance;

            SupabaseClient existing = FindAnyObjectByType<SupabaseClient>();

            if (existing != null)
                return existing;

            return new GameObject("Supabase Client").AddComponent<SupabaseClient>();
        }

        // --- Sesion ---------------------------------------------------------

        /// <summary>
        /// Deja una sesion lista. Si ya hay uno guardado, renueva el token; si
        /// no, se da de alta como anonimo. El jugador no ve nada de esto: entra
        /// al juego y ya tiene identidad.
        /// </summary>
        public IEnumerator SignIn(Action<bool, string> done = null)
        {
            if (!IsConfigured)
            {
                done?.Invoke(false, "Supabase no esta configurado.");
                yield break;
            }

            // El token de acceso dura una hora; mientras valga, no se toca nada.
            if (!string.IsNullOrEmpty(accessToken) &&
                Now() < accessTokenExpiresAt - 60d)
            {
                done?.Invoke(true, string.Empty);
                yield break;
            }

            string refreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);

            if (!string.IsNullOrEmpty(refreshToken))
            {
                bool refreshed = false;
                yield return Refresh(refreshToken, ok => refreshed = ok);

                if (refreshed)
                {
                    done?.Invoke(true, string.Empty);
                    yield break;
                }

                // El de refresco caduca o lo revocan; si ya no vale, mas vale
                // volver a entrar de cero que dejar al jugador sin ranking.
                PlayerPrefs.DeleteKey(RefreshTokenKey);
            }

            yield return SignUpAnonymously(done);
        }

        /// <summary>Crea un usuario anonimo nuevo.</summary>
        private IEnumerator SignUpAnonymously(Action<bool, string> done)
        {
            using UnityWebRequest request = BuildRequest(
                config.AuthUrl("signup"),
                "{}",
                false);

            yield return request.SendWebRequest();

            if (!IsOk(request, out string error))
            {
                done?.Invoke(false, error);
                yield break;
            }

            StoreSession(request.downloadHandler.text);
            done?.Invoke(IsSignedIn, IsSignedIn ? string.Empty : "Respuesta sin token.");
        }

        /// <summary>Cambia el token de refresco por uno de acceso nuevo.</summary>
        private IEnumerator Refresh(string refreshToken, Action<bool> done)
        {
            RefreshBody body = new RefreshBody { refresh_token = refreshToken };

            using UnityWebRequest request = BuildRequest(
                config.AuthUrl("token?grant_type=refresh_token"),
                JsonUtility.ToJson(body),
                false);

            yield return request.SendWebRequest();

            if (!IsOk(request, out string _))
            {
                done?.Invoke(false);
                yield break;
            }

            StoreSession(request.downloadHandler.text);
            done?.Invoke(IsSignedIn);
        }

        /// <summary>Guarda lo que devuelve el servidor tras entrar.</summary>
        private void StoreSession(string json)
        {
            AuthResponse response = JsonUtility.FromJson<AuthResponse>(json);

            if (response == null || string.IsNullOrEmpty(response.access_token))
                return;

            accessToken = response.access_token;
            accessTokenExpiresAt = Now() + Mathf.Max(60, response.expires_in);

            if (!string.IsNullOrEmpty(response.refresh_token))
                PlayerPrefs.SetString(RefreshTokenKey, response.refresh_token);

            if (response.user != null && !string.IsNullOrEmpty(response.user.id))
                UserId = response.user.id;

            PlayerPrefs.Save();
        }

        // --- Jugador --------------------------------------------------------

        /// <summary>
        /// Da de alta al jugador o le cambia el nombre. Hay que llamarlo al
        /// menos una vez antes de subir partidas: game_sessions apunta a
        /// players, y sin ficha la fila se rechaza.
        /// </summary>
        public IEnumerator SetDisplayName(string displayName, Action<bool, string> done = null)
        {
            string clean = (displayName ?? string.Empty).Trim();

            if (clean.Length == 0)
            {
                done?.Invoke(false, "El nombre no puede estar vacio.");
                yield break;
            }

            if (clean.Length > 16)
                clean = clean.Substring(0, 16);

            bool signedIn = false;
            yield return SignIn((ok, _) => signedIn = ok);

            if (!signedIn)
            {
                done?.Invoke(false, "No se pudo entrar en el servidor.");
                yield break;
            }

            EnsurePlayerBody body = new EnsurePlayerBody { p_display_name = clean };

            using UnityWebRequest request = BuildRequest(
                config.RestUrl("rpc/ensure_player"),
                JsonUtility.ToJson(body),
                true);

            yield return request.SendWebRequest();

            if (!IsOk(request, out string error))
            {
                done?.Invoke(false, error);
                yield break;
            }

            DisplayName = clean;
            PlayerPrefs.Save();
            done?.Invoke(true, string.Empty);
        }

        // --- Partidas -------------------------------------------------------

        /// <summary>
        /// Sube una partida al ranking global. Quien llama decide que hacer si
        /// falla; lo normal es dejarla marcada como pendiente y reintentar.
        /// </summary>
        public IEnumerator UploadSession(HighScoreEntry entry, Action<bool, string> done = null)
        {
            if (entry == null)
            {
                done?.Invoke(false, "No hay partida que subir.");
                yield break;
            }

            if (string.IsNullOrEmpty(DisplayName))
            {
                // Sin ficha de jugador la fila se rechazaria por la clave
                // ajena; mejor decirlo claro que dejar un error de Postgres.
                done?.Invoke(false, "Falta el nombre del jugador.");
                yield break;
            }

            bool signedIn = false;
            yield return SignIn((ok, _) => signedIn = ok);

            if (!signedIn)
            {
                done?.Invoke(false, "No se pudo entrar en el servidor.");
                yield break;
            }

            DateTime endedAt = new DateTime(entry.TimestampUtcTicks, DateTimeKind.Utc);

            SessionRow row = new SessionRow
            {
                player_id = UserId,
                mode = entry.Mode.ToKey(),
                score = entry.Score,
                lines = entry.TotalLines,
                level = entry.Level,
                duration_seconds = entry.DurationSeconds,
                game_version = string.IsNullOrEmpty(entry.GameVersion)
                    ? "desconocida"
                    : entry.GameVersion,
                started_at = Iso(endedAt.AddSeconds(-entry.DurationSeconds)),
                ended_at = Iso(endedAt)
            };

            using UnityWebRequest request = BuildRequest(
                config.RestUrl("game_sessions"),
                JsonUtility.ToJson(row),
                true);

            // Sin esto la respuesta trae la fila entera y no nos sirve de nada.
            request.SetRequestHeader("Prefer", "return=minimal");

            yield return request.SendWebRequest();

            if (!IsOk(request, out string error))
            {
                done?.Invoke(false, error);
                yield break;
            }

            done?.Invoke(true, string.Empty);
        }

        /// <summary>
        /// Pide el ranking de un modo. No hace falta haber jugado ni entrado:
        /// la funcion del servidor esta abierta tambien al rol anonimo.
        /// </summary>
        public IEnumerator FetchLeaderboard(
            GameMode mode,
            int limit,
            Action<bool, List<LeaderboardRow>, string> done)
        {
            if (!IsConfigured)
            {
                done?.Invoke(false, null, "Supabase no esta configurado.");
                yield break;
            }

            LeaderboardBody body = new LeaderboardBody
            {
                p_mode = mode.ToKey(),
                p_limit = Mathf.Clamp(limit, 1, 100)
            };

            using UnityWebRequest request = BuildRequest(
                config.RestUrl("rpc/leaderboard"),
                JsonUtility.ToJson(body),
                !string.IsNullOrEmpty(accessToken));

            yield return request.SendWebRequest();

            if (!IsOk(request, out string error))
            {
                done?.Invoke(false, null, error);
                yield break;
            }

            done?.Invoke(true, ParseLeaderboard(request.downloadHandler.text), string.Empty);
        }

        /// <summary>Convierte la respuesta del ranking en filas.</summary>
        private static List<LeaderboardRow> ParseLeaderboard(string json)
        {
            List<LeaderboardRow> rows = new List<LeaderboardRow>();

            if (string.IsNullOrEmpty(json))
                return rows;

            // JsonUtility no sabe leer un array suelto, asi que se le envuelve
            // en un objeto con un solo campo.
            LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(
                "{\"items\":" + json + "}");

            if (response?.items == null)
                return rows;

            foreach (LeaderboardItem item in response.items)
                rows.Add(new LeaderboardRow
                {
                    Rank = item.rank,
                    DisplayName = item.display_name,
                    Score = item.score,
                    Lines = item.lines,
                    Level = item.level,
                    EndedAtUtc = ParseDate(item.ended_at)
                });

            return rows;
        }

        // --- Prueba rapida --------------------------------------------------

        /// <summary>
        /// Comprueba de una vez la clave, el login anonimo y el esquema. Se
        /// lanza desde el menu del componente con el juego corriendo.
        /// </summary>
        [ContextMenu("Probar conexion")]
        public void TestConnection()
        {
            StartCoroutine(TestConnectionRoutine());
        }

        /// <summary>Entra, se da de alta y pide el ranking, contandolo todo.</summary>
        private IEnumerator TestConnectionRoutine()
        {
            if (!IsConfigured)
            {
                Debug.LogError("Falta el asset SupabaseConfig o sus datos.", this);
                yield break;
            }

            bool ok = false;
            string error = string.Empty;

            yield return SignIn((success, message) =>
            {
                ok = success;
                error = message;
            });

            if (!ok)
            {
                Debug.LogError($"No se pudo entrar: {error}", this);
                yield break;
            }

            Debug.Log($"Sesion lista. Jugador: {UserId}", this);

            yield return SetDisplayName(
                string.IsNullOrEmpty(DisplayName) ? "Prueba" : DisplayName,
                (success, message) =>
                {
                    ok = success;
                    error = message;
                });

            if (!ok)
            {
                Debug.LogError($"No se pudo dar de alta al jugador: {error}", this);
                yield break;
            }

            Debug.Log($"Jugador dado de alta como {DisplayName}.", this);

            yield return FetchLeaderboard(GameMode.Match3, 10, (success, rows, message) =>
            {
                if (success)
                    Debug.Log($"Ranking de match-3: {rows.Count} filas.", this);
                else
                    Debug.LogError($"No se pudo leer el ranking: {message}", this);
            });
        }

        // --- Piezas sueltas -------------------------------------------------

        /// <summary>Arma una peticion POST con las cabeceras que Supabase pide.</summary>
        private UnityWebRequest BuildRequest(string url, string body, bool authenticated)
        {
            UnityWebRequest request = new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = config.RequestTimeout
            };

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", config.PublicApiKey);

            // Sin el Bearer, PostgREST trabaja como anonimo y auth.uid() sale
            // nulo: las politicas RLS rechazarian todo lo que sea escribir.
            request.SetRequestHeader(
                "Authorization",
                $"Bearer {(authenticated && !string.IsNullOrEmpty(accessToken) ? accessToken : config.PublicApiKey)}");

            return request;
        }

        /// <summary>Dice si la peticion salio bien y, si no, con que mensaje.</summary>
        private static bool IsOk(UnityWebRequest request, out string error)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                error = string.Empty;
                return true;
            }

            string body = request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;

            ErrorResponse parsed = null;

            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    parsed = JsonUtility.FromJson<ErrorResponse>(body);
                }
                catch (Exception)
                {
                    // Hay errores que no vienen en JSON, como los del proxy.
                }
            }

            string detail = parsed?.message;

            if (string.IsNullOrEmpty(detail))
                detail = parsed?.error_description;

            if (string.IsNullOrEmpty(detail))
                detail = string.IsNullOrEmpty(body) ? request.error : body;

            error = $"{request.responseCode}: {detail}";
            return false;
        }

        /// <summary>Segundos desde 1970, para saber cuando caduca el token.</summary>
        private static double Now()
        {
            return (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
        }

        /// <summary>Fecha en el formato que entiende Postgres.</summary>
        private static string Iso(DateTime utc)
        {
            return utc.ToString("o", CultureInfo.InvariantCulture);
        }

        /// <summary>Lee una fecha del servidor sin romperse si viene rara.</summary>
        private static DateTime ParseDate(string value)
        {
            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime parsed)
                ? parsed
                : DateTime.UnixEpoch;
        }

        // --- Formas de los mensajes ------------------------------------------
        // Los nombres van en minusculas con guion bajo porque JsonUtility casa
        // los campos por nombre exacto y asi vienen y van en la API.
        //
        // Los rellena JsonUtility por reflexion, que el compilador no ve, asi
        // que avisaria de que nadie los asigna. El aviso se apaga solo aqui.
#pragma warning disable 0649

        [Serializable]
        private sealed class AuthResponse
        {
            public string access_token;
            public string refresh_token;
            public int expires_in;
            public AuthUser user;
        }

        [Serializable]
        private sealed class AuthUser
        {
            public string id;
        }

        [Serializable]
        private sealed class RefreshBody
        {
            public string refresh_token;
        }

        [Serializable]
        private sealed class EnsurePlayerBody
        {
            public string p_display_name;
        }

        [Serializable]
        private sealed class LeaderboardBody
        {
            public string p_mode;
            public int p_limit;
        }

        [Serializable]
        private sealed class SessionRow
        {
            public string player_id;
            public string mode;
            public int score;
            public int lines;
            public int level;
            public int duration_seconds;
            public string game_version;
            public string started_at;
            public string ended_at;
        }

        [Serializable]
        private sealed class LeaderboardResponse
        {
            public LeaderboardItem[] items;
        }

        [Serializable]
        private sealed class LeaderboardItem
        {
            public int rank;
            public string display_name;
            public int score;
            public int lines;
            public int level;
            public string ended_at;
        }

        [Serializable]
        private sealed class ErrorResponse
        {
            public string message;
            public string error_description;
        }

#pragma warning restore 0649
    }
}
