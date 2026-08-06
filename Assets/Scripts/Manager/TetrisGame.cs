using System;
using System.Collections;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Bucle de la partida: hace bajar la pieza al ritmo del nivel, la fija
    /// cuando toca fondo, resuelve las líneas y saca la siguiente.
    /// Es el único punto de entrada para los controles.
    /// </summary>
    public class TetrisGame : BoardGame
    {
        [SerializeField] private Board board;
        [SerializeField] private PieceSpawner spawner;
        [SerializeField] private LineClearSystem lineClear;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private DifficultySystem difficulty;
        [SerializeField] private bool startOnAwake = true;

        private float fallTimer;

        public ScoreManager Score => scoreManager;
        public DifficultySystem Difficulty => difficulty;
        public PieceSpawner Spawner => spawner;

        public event Action PieceLocked;
        public event Action PieceMoved;
        public event Action PieceRotated;
        public event Action HardDropped;

        /// <summary>Busca en el propio objeto los sistemas que no vengan asignados.</summary>
        private void Awake()
        {
            board ??= GetComponent<Board>();
            spawner ??= GetComponent<PieceSpawner>();
            lineClear ??= GetComponent<LineClearSystem>();
            scoreManager ??= GetComponent<ScoreManager>();
            difficulty ??= GetComponent<DifficultySystem>();
        }

        /// <summary>Empieza la partida sola si esta configurado asi.</summary>
        private void Start()
        {
            if (startOnAwake)
                StartGame();
        }

        /// <summary>Lleva la cuenta de la caida automatica de la pieza.</summary>
        private void Update()
        {
            if (!AcceptsInput)
                return;

            if (spawner.CurrentPiece == null)
                return;

            fallTimer += Time.deltaTime;

            if (fallTimer < CurrentFallInterval)
                return;

            fallTimer = 0f;
            StepDown(false);
        }

        private float CurrentFallInterval =>
            difficulty != null ? Mathf.Max(0.01f, difficulty.FallInterval) : 0.5f;

        /// <inheritdoc />
        public override GameMode Mode => GameMode.Tetris;

        /// <summary>Vacia el tablero, pone los contadores a cero y saca la primera pieza.</summary>
        public override void StartGame()
        {
            if (board == null || spawner == null)
            {
                Debug.LogError("TetrisGame necesita un Board y un PieceSpawner.", this);
                return;
            }

            ResetMatchClock();
            StopAllCoroutines();
            SetBusy(false);
            fallTimer = 0f;

            board.ClearBoard();
            spawner.ResetSpawner();
            scoreManager?.ResetScore();
            difficulty?.ResetDifficulty();

            SetState(GameState.Playing);

            if (!spawner.SpawnNext())
                EndGame();
        }

        /// <summary>Al reanudar, la pieza no debe caer de golpe.</summary>
        public override void SetBusy(bool value)
        {
            base.SetBusy(value);

            if (!value)
                fallTimer = 0f;
        }

        /// <summary>
        /// Saca la siguiente pieza y, si ya no cabe, da la partida por
        /// terminada. Lo usa el giro del tablero para reanudar el juego.
        /// </summary>
        public void SpawnOrEnd()
        {
            if (spawner == null)
                return;

            if (!spawner.SpawnNext())
                EndGame();
        }

        // --- Acciones que invocan los controles -------------------------

        /// <summary>Mueve la pieza a izquierda o derecha.</summary>
        public bool MoveHorizontal(int direction)
        {
            if (!AcceptsInput || direction == 0)
                return false;

            Tetromino piece = spawner.CurrentPiece;
            bool moved = piece != null &&
                         piece.TryMove(new Vector2Int((int)Mathf.Sign(direction), 0));

            if (moved)
                PieceMoved?.Invoke();

            return moved;
        }

        /// <summary>Gira la pieza en juego.</summary>
        public bool Rotate(bool clockwise)
        {
            if (!AcceptsInput)
                return false;

            Tetromino piece = spawner.CurrentPiece;
            bool rotated = piece != null && piece.TryRotate(clockwise);

            if (rotated)
                PieceRotated?.Invoke();

            return rotated;
        }

        /// <summary>Baja una celda por orden del jugador y suma su punto.</summary>
        public void SoftDrop()
        {
            if (!AcceptsInput)
                return;

            fallTimer = 0f;
            StepDown(true);
        }

        /// <summary>Deja caer la pieza hasta el fondo y la fija de inmediato.</summary>
        public void HardDrop()
        {
            if (!AcceptsInput)
                return;

            Tetromino piece = spawner.CurrentPiece;

            if (piece == null)
                return;

            int cells = 0;

            while (piece.TryMove(Vector2Int.down))
                cells++;

            scoreManager?.AddHardDrop(cells);
            HardDropped?.Invoke();

            fallTimer = 0f;
            StartCoroutine(LockAndContinue());
        }

        // ----------------------------------------------------------------

        /// <summary>Baja la pieza una celda, y la fija si ya no puede caer mas.</summary>
        private void StepDown(bool fromPlayer)
        {
            Tetromino piece = spawner.CurrentPiece;

            if (piece == null)
                return;

            if (piece.TryMove(Vector2Int.down))
            {
                if (fromPlayer)
                    scoreManager?.AddSoftDrop(1);

                PieceMoved?.Invoke();
                return;
            }

            // No puede bajar más: se fija donde está.
            StartCoroutine(LockAndContinue());
        }

        /// <summary>Fija la pieza, resuelve las lineas y saca la siguiente.</summary>
        private IEnumerator LockAndContinue()
        {
            SetBusy(true);

            Tetromino piece = spawner.CurrentPiece;

            if (piece != null)
                piece.TryLock();

            PieceLocked?.Invoke();

            if (lineClear != null)
            {
                yield return lineClear.ClearFullLines();

                int cleared = lineClear.LastClearedCount;

                if (cleared > 0)
                {
                    scoreManager?.AddLines(cleared, difficulty != null ? difficulty.Level : 1);
                    difficulty?.NotifyLinesCleared(cleared);
                }
            }

            SetBusy(false);

            if (State != GameState.Playing)
                yield break;

            if (!spawner.SpawnNext())
                EndGame();
        }

        /// <summary>Da la partida por perdida y apunta la puntuacion.</summary>
        private void EndGame()
        {
            if (State == GameState.GameOver)
                return;

            int score = scoreManager != null ? scoreManager.Score : 0;
            int lines = scoreManager != null ? scoreManager.TotalLines : 0;
            int level = difficulty != null ? difficulty.Level : 1;

            HighScoreManager.SubmitScore(
                Mode,
                score,
                lines,
                level,
                MatchDurationSeconds);
            SetState(GameState.GameOver);
            RaiseGameEnded();
        }

    }
}
