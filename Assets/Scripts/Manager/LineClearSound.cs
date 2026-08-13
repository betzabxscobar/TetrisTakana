using UnityEngine;

namespace TetrisTakana
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class LineClearSound : MonoBehaviour
    {
        [SerializeField] private LineClearSystem lineClearSystem;
        [SerializeField] private Match3.MatchSystem matchSystem;
        [SerializeField] private Match3.GridCursor cursor;
        [SerializeField] private BoardGame game;
        [SerializeField] private BoardFlipSystem flipSystem;
        [Tooltip("Para subir el tono con la racha. Vacio: suena siempre igual.")]
        [SerializeField] private Match3.ComboSystem comboSystem;

        [Header("Efectos")]
        [Tooltip("Cuanto sube el tono por cada eslabon del combo.")]
        [SerializeField, Range(0f, 0.3f)] private float comboPitchStep = 0.09f;
        [Tooltip("Tope del tono, para que una cascada larga no chille.")]
        [SerializeField, Range(1f, 3f)] private float comboPitchMax = 1.7f;
        [SerializeField] private AudioClip cursorMoveClip;
        [SerializeField] private AudioClip gameOverClip;
        [SerializeField] private AudioClip flipStartedClip;

        [Header("Musica")]
        [SerializeField] private AudioClip backgroundMusicClip;
        [SerializeField] private bool playBackgroundMusic = true;
        [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.45f;

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            lineClearSystem ??= FindAnyObjectByType<LineClearSystem>();
            matchSystem ??= FindAnyObjectByType<Match3.MatchSystem>();
            cursor ??= FindAnyObjectByType<Match3.GridCursor>();
            game ??= FindAnyObjectByType<Match3.Match3Game>();
            game ??= FindAnyObjectByType<TetrisGame>();
            flipSystem ??= FindAnyObjectByType<BoardFlipSystem>();
            comboSystem ??= FindAnyObjectByType<Match3.ComboSystem>();
        }

        private void Start()
        {
            AudioManager audioManager = AudioManager.EnsureInstance();

            if (audioManager == null)
                return;

            if (!playBackgroundMusic)
            {
                audioManager.StopMusic();
                return;
            }

            if (backgroundMusicClip == null)
                return;

            audioManager.MusicVolume = backgroundMusicVolume;
            audioManager.PlayMusic(backgroundMusicClip);
        }

        private void OnEnable()
        {
            lineClearSystem ??= FindAnyObjectByType<LineClearSystem>();
            matchSystem ??= FindAnyObjectByType<Match3.MatchSystem>();

            if (lineClearSystem != null)
                lineClearSystem.LinesCleared += HandleBlocksCleared;

            if (matchSystem != null)
                matchSystem.MatchResolved += HandleBlocksCleared;

            if (cursor != null)
                cursor.CursorMoved += HandleCursorMoved;

            if (game != null)
                game.GameEnded += HandleGameEnded;

            if (flipSystem != null)
                flipSystem.FlipStarted += HandleFlipStarted;
        }

        private void OnDisable()
        {
            if (lineClearSystem != null)
                lineClearSystem.LinesCleared -= HandleBlocksCleared;

            if (matchSystem != null)
                matchSystem.MatchResolved -= HandleBlocksCleared;

            if (cursor != null)
                cursor.CursorMoved -= HandleCursorMoved;

            if (game != null)
                game.GameEnded -= HandleGameEnded;

            if (flipSystem != null)
                flipSystem.FlipStarted -= HandleFlipStarted;
        }

        /// <summary>
        /// Suena la combinacion, mas aguda cuanto mas larga sea la racha. Es el
        /// truco de siempre de los juegos de fichas: la escala que sube sola
        /// mientras encadenas engancha mucho mas que el mismo golpe repetido.
        /// </summary>
        private void HandleBlocksCleared(int clearedCount)
        {
            if (clearedCount <= 0 || audioSource.clip == null)
                return;

            int combo = comboSystem != null ? comboSystem.CurrentCombo : 1;

            // PlayOneShot no admite tono, asi que se toca el del propio
            // AudioSource. Vuelve a 1 en cada golpe para que no se quede subido
            // cuando la racha se corta.
            audioSource.pitch = Mathf.Min(
                comboPitchMax,
                1f + Mathf.Max(0, combo - 1) * comboPitchStep);

            audioSource.PlayOneShot(audioSource.clip);
        }

        private void HandleCursorMoved()
        {
            Play(cursorMoveClip);
        }

        private void HandleGameEnded()
        {
            Play(gameOverClip);
        }

        private void HandleFlipStarted()
        {
            Play(flipStartedClip);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }
    }
}
