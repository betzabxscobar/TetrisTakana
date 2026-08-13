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
        [SerializeField] private Match3.RisingStack risingStack;
        [SerializeField] private BoardGame game;
        [SerializeField] private BoardFlipSystem flipSystem;

        [Header("Efectos")]
        [SerializeField] private AudioClip cursorMoveClip;
        [SerializeField] private AudioClip swapClip;
        [SerializeField] private AudioClip rowPushedClip;
        [Tooltip("Sonido de una linea eliminada en el modo Tetris.")]
        [SerializeField] private AudioClip lineClearClip;
        [Tooltip("Sonido de una combinacion del modo match-3.")]
        [SerializeField] private AudioClip matchClearClip;
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
            risingStack ??= FindAnyObjectByType<Match3.RisingStack>();
            game ??= FindAnyObjectByType<Match3.Match3Game>();
            game ??= FindAnyObjectByType<TetrisGame>();
            flipSystem ??= FindAnyObjectByType<BoardFlipSystem>();
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
                lineClearSystem.LinesCleared += HandleLinesCleared;

            if (matchSystem != null)
                matchSystem.MatchResolved += HandleMatchesCleared;

            if (cursor != null)
            {
                cursor.CursorMoved += HandleCursorMoved;
                cursor.SwapPerformed += HandleSwapPerformed;
            }

            if (risingStack != null)
                risingStack.RowPushed += HandleRowPushed;

            if (game != null)
                game.GameEnded += HandleGameEnded;

            if (flipSystem != null)
                flipSystem.FlipStarted += HandleFlipStarted;
        }

        private void OnDisable()
        {
            if (lineClearSystem != null)
                lineClearSystem.LinesCleared -= HandleLinesCleared;

            if (matchSystem != null)
                matchSystem.MatchResolved -= HandleMatchesCleared;

            if (cursor != null)
            {
                cursor.CursorMoved -= HandleCursorMoved;
                cursor.SwapPerformed -= HandleSwapPerformed;
            }

            if (risingStack != null)
                risingStack.RowPushed -= HandleRowPushed;

            if (game != null)
                game.GameEnded -= HandleGameEnded;

            if (flipSystem != null)
                flipSystem.FlipStarted -= HandleFlipStarted;
        }

        private void HandleLinesCleared(int clearedCount)
        {
            if (clearedCount <= 0)
                return;

            // La referencia serializada evita depender de si el AudioSource
            // guarda un AudioClip antiguo o un AudioResource de Unity 6.
            Play(lineClearClip != null ? lineClearClip : audioSource.clip);
        }

        private void HandleMatchesCleared(int clearedCount)
        {
            if (clearedCount <= 0)
                return;

            Play(matchClearClip != null ? matchClearClip : audioSource.clip);
        }

        private void HandleCursorMoved()
        {
            Play(cursorMoveClip);
        }

        private void HandleSwapPerformed()
        {
            Play(swapClip);
        }

        private void HandleRowPushed()
        {
            Play(rowPushedClip);
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
