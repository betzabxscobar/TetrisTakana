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

        [Header("Efectos")]
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

        private void HandleBlocksCleared(int clearedCount)
        {
            if (clearedCount > 0 && audioSource.clip != null)
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
