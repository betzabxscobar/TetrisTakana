using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Pone sonido a la partida: engancha los efectos del Tetris de Game Boy a
    /// los eventos que ya publican <see cref="TetrisGame"/>,
    /// <see cref="ScoreManager"/> y <see cref="DifficultySystem"/>, y arranca la
    /// musica de ambiente. Todo sale por el <see cref="AudioManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayAudio : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private TetrisGame game;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private DifficultySystem difficulty;

        [Header("Ambiente")]
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private bool playMusicOnStart = true;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.45f;

        [Header("Bloques")]
        [Tooltip("Solo suena en los movimientos laterales, no al caer.")]
        [SerializeField] private AudioClip moveClip;
        [SerializeField] private AudioClip rotateClip;
        [SerializeField] private AudioClip hardDropClip;
        [SerializeField] private AudioClip lockClip;

        [Header("Lineas")]
        [SerializeField] private AudioClip lineClearClip;
        [Tooltip("Cuatro lineas de golpe.")]
        [SerializeField] private AudioClip tetrisClip;
        [SerializeField] private AudioClip levelUpClip;

        [Header("Partida")]
        [SerializeField] private AudioClip gameOverClip;

        [Header("Mezcla")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;

        private AudioManager audioManager;
        private int lastAnchorX;
        private int lastLines;
        private int lastLevel = 1;
        private bool subscribed;

        private void Awake()
        {
            game ??= FindAnyObjectByType<TetrisGame>();
            scoreManager ??= game != null ? game.Score : FindAnyObjectByType<ScoreManager>();
            difficulty ??= game != null ? game.Difficulty : FindAnyObjectByType<DifficultySystem>();
        }

        private void Start()
        {
            // En Start y no en Awake: si el AudioManager viene del menu con
            // DontDestroyOnLoad, para entonces ya se ha registrado.
            audioManager = AudioManager.EnsureInstance();

            if (audioManager == null)
                return;

            audioManager.SfxVolume = sfxVolume;
            audioManager.MusicVolume = musicVolume;

            if (playMusicOnStart && musicClip != null)
                audioManager.PlayMusic(musicClip);
        }

        private void OnEnable()
        {
            Subscribe();
            lastLines = scoreManager != null ? scoreManager.TotalLines : 0;
            lastLevel = difficulty != null ? difficulty.Level : 1;
            lastAnchorX = GetAnchorX();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed)
                return;

            if (game != null)
            {
                game.PieceMoved += HandlePieceMoved;
                game.PieceRotated += HandlePieceRotated;
                game.PieceLocked += HandlePieceLocked;
                game.HardDropped += HandleHardDropped;
                game.GameEnded += HandleGameEnded;
            }

            if (scoreManager != null)
            {
                scoreManager.LinesChanged += HandleLinesChanged;
                scoreManager.TetrisScored += HandleTetrisScored;
            }

            if (difficulty != null)
                difficulty.LevelChanged += HandleLevelChanged;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (game != null)
            {
                game.PieceMoved -= HandlePieceMoved;
                game.PieceRotated -= HandlePieceRotated;
                game.PieceLocked -= HandlePieceLocked;
                game.HardDropped -= HandleHardDropped;
                game.GameEnded -= HandleGameEnded;
            }

            if (scoreManager != null)
            {
                scoreManager.LinesChanged -= HandleLinesChanged;
                scoreManager.TetrisScored -= HandleTetrisScored;
            }

            if (difficulty != null)
                difficulty.LevelChanged -= HandleLevelChanged;

            subscribed = false;
        }

        // --- Bloques -----------------------------------------------------

        private void HandlePieceMoved()
        {
            int anchorX = GetAnchorX();

            // PieceMoved tambien salta con la gravedad y con la bajada suave;
            // sin este filtro el sonido lateral sonaria en cada celda que baja.
            bool movedSideways = anchorX != lastAnchorX;
            lastAnchorX = anchorX;

            if (movedSideways)
                Play(moveClip);
        }

        private void HandlePieceRotated()
        {
            lastAnchorX = GetAnchorX();
            Play(rotateClip);
        }

        private void HandleHardDropped()
        {
            Play(hardDropClip);
        }

        private void HandlePieceLocked()
        {
            Play(lockClip);

            // La pieza siguiente aparece en otra columna; sin esto el primer
            // movimiento lateral se comeria el sonido.
            lastAnchorX = GetAnchorX();
        }

        // --- Lineas y nivel ----------------------------------------------

        private void HandleLinesChanged(int totalLines)
        {
            int cleared = totalLines - lastLines;
            lastLines = totalLines;

            // Las cuatro lineas de golpe las anuncia TetrisScored con su
            // propio jingle; aqui solo van los cortes de 1 a 3.
            if (cleared > 0 && cleared < 4)
                Play(lineClearClip);
        }

        private void HandleTetrisScored()
        {
            Play(tetrisClip);
        }

        private void HandleLevelChanged(int level)
        {
            // ResetDifficulty tambien dispara el evento al empezar la partida,
            // asi que solo suena cuando el nivel sube de verdad.
            bool wentUp = level > lastLevel;
            lastLevel = level;

            if (wentUp)
                Play(levelUpClip);
        }

        private void HandleGameEnded()
        {
            Play(gameOverClip);
        }

        // -----------------------------------------------------------------

        private int GetAnchorX()
        {
            Tetromino piece = game != null && game.Spawner != null
                ? game.Spawner.CurrentPiece
                : null;

            return piece != null ? piece.AnchorPosition.x : lastAnchorX;
        }

        private void Play(AudioClip clip)
        {
            if (clip == null)
                return;

            audioManager ??= AudioManager.Instance;

            if (audioManager != null)
                audioManager.PlaySfx(clip);
        }
    }
}
