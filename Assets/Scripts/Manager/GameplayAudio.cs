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

        /// <summary>Localiza la partida y los sistemas de los que saca los avisos.</summary>
        private void Awake()
        {
            game ??= FindAnyObjectByType<TetrisGame>();
            scoreManager ??= game != null ? game.Score : FindAnyObjectByType<ScoreManager>();
            difficulty ??= game != null ? game.Difficulty : FindAnyObjectByType<DifficultySystem>();
        }

        /// <summary>Arranca la musica ya con el AudioManager del menu presente.</summary>
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

        /// <summary>Se suscribe a los eventos y apunta el recuento de lineas de partida.</summary>
        private void OnEnable()
        {
            Subscribe();
            lastLines = scoreManager != null ? scoreManager.TotalLines : 0;
            lastLevel = difficulty != null ? difficulty.Level : 1;
            lastAnchorX = GetAnchorX();
        }

        /// <summary>Se da de baja de todos los eventos.</summary>
        private void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>Engancha cada evento de la partida con su sonido.</summary>
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

        /// <summary>Suelta todos los eventos enganchados.</summary>
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

        /// <summary>Suena el movimiento solo si la pieza cambio de columna.</summary>
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

        /// <summary>Suena el giro de la pieza.</summary>
        private void HandlePieceRotated()
        {
            lastAnchorX = GetAnchorX();
            Play(rotateClip);
        }

        /// <summary>Suena la caida en picado.</summary>
        private void HandleHardDropped()
        {
            Play(hardDropClip);
        }

        /// <summary>Suena la pieza al fijarse en la pila.</summary>
        private void HandlePieceLocked()
        {
            Play(lockClip);

            // La pieza siguiente aparece en otra columna; sin esto el primer
            // movimiento lateral se comeria el sonido.
            lastAnchorX = GetAnchorX();
        }

        // --- Lineas y nivel ----------------------------------------------

        /// <summary>Suena la limpieza de lineas segun cuantas hayan caido.</summary>
        private void HandleLinesChanged(int totalLines)
        {
            int cleared = totalLines - lastLines;
            lastLines = totalLines;

            // Las cuatro lineas de golpe las anuncia TetrisScored con su
            // propio jingle; aqui solo van los cortes de 1 a 3.
            if (cleared > 0 && cleared < 4)
                Play(lineClearClip);
        }

        /// <summary>Suena el aviso especial de cuatro lineas de golpe.</summary>
        private void HandleTetrisScored()
        {
            Play(tetrisClip);
        }

        /// <summary>Suena la subida de nivel, no el reinicio del contador.</summary>
        private void HandleLevelChanged(int level)
        {
            // ResetDifficulty tambien dispara el evento al empezar la partida,
            // asi que solo suena cuando el nivel sube de verdad.
            bool wentUp = level > lastLevel;
            lastLevel = level;

            if (wentUp)
                Play(levelUpClip);
        }

        /// <summary>Suena el final de la partida.</summary>
        private void HandleGameEnded()
        {
            Play(gameOverClip);
        }

        // -----------------------------------------------------------------

        /// <summary>Columna en la que esta ahora mismo la pieza en juego.</summary>
        private int GetAnchorX()
        {
            Tetromino piece = game != null && game.Spawner != null
                ? game.Spawner.CurrentPiece
                : null;

            return piece != null ? piece.AnchorPosition.x : lastAnchorX;
        }

        /// <summary>Reproduce un efecto de sonido si hay clip y fuente.</summary>
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
