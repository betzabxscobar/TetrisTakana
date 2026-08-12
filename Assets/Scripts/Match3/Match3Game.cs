using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// Bucle del modo match-3. Los sistemas restaurados se gobiernan solos
    /// (el spawner rellena, el cursor intercambia y MatchSystem resuelve las
    /// cascadas), asi que este componente solo aporta lo que les faltaba: los
    /// estados de partida que la pausa, el fin de juego y el reloj de arena
    /// necesitan, y la condicion de derrota cuando ya no quedan jugadas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Match3Game : BoardGame
    {
        [Header("Sistemas")]
        [SerializeField] private Board board;
        [SerializeField] private Spawner spawner;
        [SerializeField] private MatchSystem matchSystem;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private DifficultySystem difficulty;
        [SerializeField] private ComboSystem comboSystem;
        [SerializeField] private RisingStack risingStack;

        [Header("Partida")]
        [SerializeField] private bool startOnAwake = true;
        [Tooltip("Filas llenas al empezar, contando desde abajo. Cero usa la mitad del tablero.")]
        [SerializeField, Min(0)] private int startingRows;
        [Tooltip("Limpia sin puntuar las combinaciones que traiga el tablero recien generado.")]
        [SerializeField] private bool resolveOnStart = true;
        [Tooltip("Con la pila subiendo esto sobra: quedarse sin jugadas es pasajero.")]
        [SerializeField] private bool endWhenNoMoves;
        [Tooltip("Cada cuantos segundos se comprueba si quedan jugadas.")]
        [SerializeField, Min(0.1f)] private float noMovesCheckInterval = 0.5f;

        private readonly HintFinder hintFinder = new HintFinder();
        private float nextCheckTime;

        public ScoreManager Score => scoreManager;
        public DifficultySystem Difficulty => difficulty;

        /// <summary>Busca en el propio objeto los sistemas que no vengan asignados.</summary>
        private void Awake()
        {
            board ??= GetComponent<Board>();
            spawner ??= GetComponent<Spawner>();
            matchSystem ??= GetComponent<MatchSystem>();
            scoreManager ??= GetComponent<ScoreManager>();
            difficulty ??= GetComponent<DifficultySystem>();
            comboSystem ??= GetComponent<ComboSystem>();
            risingStack ??= GetComponent<RisingStack>();
        }

        /// <summary>Se pone a escuchar si la pila llega al techo.</summary>
        private void OnEnable()
        {
            if (risingStack != null)
                risingStack.ToppedOut += HandleToppedOut;
        }

        /// <summary>Deja de escuchar a la pila.</summary>
        private void OnDisable()
        {
            if (risingStack != null)
                risingStack.ToppedOut -= HandleToppedOut;
        }

        /// <summary>La pila llego al techo: fin de la partida.</summary>
        private void HandleToppedOut()
        {
            EndGame();
        }

        /// <summary>Empieza la partida sola si esta configurado asi.</summary>
        private void Start()
        {
            if (startOnAwake)
                StartGame();
        }

        /// <summary>Atiende la pausa y marca si el tablero esta ocupado resolviendo.</summary>
        private void Update()
        {
            HandlePauseInput();

            if (State != GameState.Playing)
                return;

            // El tablero esta ocupado mientras caen las fichas: asi ni el
            // cursor ni el giro del reloj se cuelan a media cascada.
            SetBusy(matchSystem != null && matchSystem.IsResolving);

            if (!endWhenNoMoves || IsBusy || Time.time < nextCheckTime)
                return;

            nextCheckTime = Time.time + noMovesCheckInterval;

            if (!HasAvailableMoves())
                EndGame();
        }

        /// <summary>Lee las teclas de pausa y la de volver a empezar.</summary>
        private void HandlePauseInput()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            if (State == GameState.GameOver)
            {
                if (keyboard.enterKey.wasPressedThisFrame ||
                    keyboard.numpadEnterKey.wasPressedThisFrame)
                    StartGame();

                return;
            }

            // El cursor ya no guarda seleccion que soltar, asi que Escape vuelve
            // a ser pausa a secas, igual que P.
            if (keyboard.pKey.wasPressedThisFrame ||
                keyboard.escapeKey.wasPressedThisFrame)
                TogglePause();
        }

        /// <inheritdoc />
        public override GameMode Mode => GameMode.Match3;

        /// <summary>Vacia el tablero, pone los contadores a cero y lo vuelve a llenar.</summary>
        public override void StartGame()
        {
            if (board == null || spawner == null)
            {
                Debug.LogError("Match3Game necesita un Board y un Spawner.", this);
                return;
            }

            ResetMatchClock();
            StopAllCoroutines();
            SetBusy(false);
            SetHold(false);

            board.ClearBoard();
            scoreManager?.ResetScore();
            comboSystem?.ResetCombo();
            difficulty?.ResetDifficulty();
            risingStack?.ResetTimer();

            SetState(GameState.Playing);
            nextCheckTime = Time.time + noMovesCheckInterval;
            StartCoroutine(FillBoard());
        }

        /// <summary>Llena el tablero hasta la altura de partida y limpia lo que venga hecho.</summary>
        private IEnumerator FillBoard()
        {
            // Retenido durante todo el llenado: si no, el cursor puede
            // intercambiar mientras aun caen fichas y se lanzan dos corrutinas
            // sobre la misma lista de combinaciones. Va en SetHold porque
            // Update reescribe busy cada frame con el estado de las cascadas.
            SetHold(true);

            // Se arranca a media altura y el resto lo va empujando la pila.
            int rows = startingRows > 0 ? startingRows : board.Height / 2;
            yield return spawner.FillUpTo(rows);

            // La prevencion al generar evita casi todas las combinaciones, pero
            // no las que se cierran por arriba. Se limpian sin puntuar.
            if (resolveOnStart && matchSystem != null)
                yield return matchSystem.ResolveExisting(false);

            SetHold(false);
        }

        /// <summary>Da la partida por perdida y apunta la puntuacion.</summary>
        private void EndGame()
        {
            if (State == GameState.GameOver)
                return;

            int score = scoreManager != null ? scoreManager.Score : 0;
            int level = difficulty != null ? difficulty.Level : 1;

            // El match-3 no cuenta lineas, asi que ese hueco va en cero; el
            // modo viaja con el resultado para que el ranking no mezcle las
            // dos formas de puntuar.
            HighScoreManager.SubmitScore(Mode, score, 0, level, MatchDurationSeconds);
            SetState(GameState.GameOver);
            RaiseGameEnded();
        }

        /// <summary>
        /// Comprueba si algun intercambio entre vecinos formaria tres en raya.
        /// El trabajo lo hace <see cref="HintFinder"/>, que es el mismo que usa
        /// la mascota para saber que jugada señalar: si se calculara aqui
        /// aparte, el bucle podria dar la partida por perdida justo mientras la
        /// mascota apunta a una jugada buena.
        /// </summary>
        public bool HasAvailableMoves()
        {
            return board == null || hintFinder.HasAnyMove(board);
        }
    }
}
