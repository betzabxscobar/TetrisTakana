using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Gestiona la música y los efectos de sonido del juego.
    /// Es un Singleton y usa DontDestroyOnLoad para que la música
    /// no se detenga ni se duplique al cambiar de escena.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;

        [Tooltip("AudioSource dedicado a la música de fondo.")]
        [SerializeField] private AudioSource musicSource;

        [Tooltip("AudioSource dedicado a los efectos de sonido.")]
        [SerializeField] private AudioSource sfxSource;

        public static AudioManager Instance => instance;

        /// <summary>Deja una sola copia viva y la conserva al cambiar de escena.</summary>
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (musicSource == null)
                musicSource = gameObject.AddComponent<AudioSource>();

            if (sfxSource == null)
                sfxSource = gameObject.AddComponent<AudioSource>();

            musicSource.loop = true;
            musicSource.playOnAwake = false;
            sfxSource.playOnAwake = false;
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
        public static AudioManager EnsureInstance()
        {
            if (instance != null)
                return instance;

            AudioManager existing = FindAnyObjectByType<AudioManager>();

            if (existing != null)
                return existing;

            GameObject managerObject = new GameObject("Audio Manager");
            return managerObject.AddComponent<AudioManager>();
        }

        /// <summary>Reproduce una pista como música de fondo en bucle.</summary>
        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || clip == musicSource.clip && musicSource.isPlaying)
                return;

            musicSource.clip = clip;
            musicSource.Play();
        }

        /// <summary>Detiene la música de fondo.</summary>
        public void StopMusic()
        {
            musicSource.Stop();
        }

        /// <summary>Reproduce un efecto de sonido una sola vez.</summary>
        public void PlaySfx(AudioClip clip)
        {
            if (clip == null)
                return;

            sfxSource.PlayOneShot(clip);
        }

        public float MusicVolume
        {
            get => musicSource.volume;
            set => musicSource.volume = Mathf.Clamp01(value);
        }

        public float SfxVolume
        {
            get => sfxSource.volume;
            set => sfxSource.volume = Mathf.Clamp01(value);
        }
    }
}