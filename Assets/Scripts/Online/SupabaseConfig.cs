using System;
using System.Text;
using UnityEngine;

namespace TetrisTakana.Online
{
    /// <summary>
    /// Los datos para hablar con Supabase. Van en un asset y no escritos en el
    /// codigo para que cada quien pueda apuntar a su propio proyecto sin tocar
    /// scripts, y para que la clave no acabe en el historial de git.
    ///
    /// Se busca en Resources con el nombre del asset, asi que basta con crearlo
    /// en Assets/Resources y rellenarlo: no hay que arrastrarlo a ninguna
    /// escena.
    /// </summary>
    [CreateAssetMenu(
        menuName = "TetrisTakana/Supabase Config",
        fileName = ResourceName)]
    public sealed class SupabaseConfig : ScriptableObject
    {
        public const string ResourceName = "SupabaseConfig";

        [Tooltip("Settings > API. Algo como https://abcdefgh.supabase.co")]
        [SerializeField] private string projectUrl = string.Empty;

        [Tooltip("Settings > API Keys. La publishable o la anon public. NUNCA la secret ni la service_role.")]
        [SerializeField, TextArea(2, 5)] private string publicApiKey = string.Empty;

        [Tooltip("Segundos antes de dar una peticion por perdida.")]
        [SerializeField, Min(1f)] private float requestTimeoutSeconds = 15f;

        /// <summary>La direccion del proyecto, sin la barra final.</summary>
        public string ProjectUrl =>
            string.IsNullOrWhiteSpace(projectUrl)
                ? string.Empty
                : projectUrl.Trim().TrimEnd('/');

        /// <summary>La clave publica que viaja en cada peticion.</summary>
        public string PublicApiKey =>
            string.IsNullOrWhiteSpace(publicApiKey)
                ? string.Empty
                : publicApiKey.Trim();

        public int RequestTimeout => Mathf.CeilToInt(requestTimeoutSeconds);

        /// <summary>Hay algo con lo que intentarlo.</summary>
        public bool IsConfigured =>
            ProjectUrl.StartsWith("https://", StringComparison.Ordinal) &&
            PublicApiKey.Length > 0;

        /// <summary>
        /// La clave puesta es de las de administrador. Se comprueba porque las
        /// dos claves se copian de la misma pantalla, tienen un aspecto
        /// parecido y confundirlas no da ningun error visible: el juego
        /// funcionaria igual de bien mientras reparte por el mundo una llave
        /// que salta la seguridad entera de la base de datos.
        /// </summary>
        public bool LooksLikeSecretKey
        {
            get
            {
                string key = PublicApiKey;

                if (key.Length == 0)
                    return false;

                if (key.StartsWith("sb_secret_", StringComparison.Ordinal))
                    return true;

                // Las claves antiguas son un JWT que lleva el rol dentro.
                return DecodeJwtPayload(key).Contains("service_role");
            }
        }

        public string AuthUrl(string path) => $"{ProjectUrl}/auth/v1/{path}";

        public string RestUrl(string path) => $"{ProjectUrl}/rest/v1/{path}";

        /// <summary>Avisa en el inspector si la clave puesta es la que no es.</summary>
        private void OnValidate()
        {
            if (LooksLikeSecretKey)
                Debug.LogError(
                    "Esa es la clave secreta de Supabase: salta las reglas de " +
                    "seguridad y en un build de WebGL la puede leer cualquiera. " +
                    "Usa la publishable o la anon public.",
                    this);
        }

        /// <summary>Saca el contenido de un JWT sin comprobar la firma.</summary>
        private static string DecodeJwtPayload(string token)
        {
            try
            {
                string[] parts = token.Split('.');

                if (parts.Length < 2)
                    return string.Empty;

                // Base64 de URL: cambia dos simbolos y se come el relleno.
                string payload = parts[1].Replace('-', '+').Replace('_', '/');

                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            }
            catch (Exception)
            {
                // Una clave con formato raro no es una clave secreta; que siga.
                return string.Empty;
            }
        }
    }
}
