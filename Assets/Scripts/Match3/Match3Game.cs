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

        private int[,] types;
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
        /// Trabaja sobre una copia de los tipos para no tocar el tablero real.
        /// </summary>
        public bool HasAvailableMoves()
        {
            if (board == null)
                return true;

            int width = board.Width;
            int height = board.Height;

            if (types == null ||
                types.GetLength(0) != width ||
                types.GetLength(1) != height)
                types = new int[width, height];

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                BoardBlock block = board.GetBlock(new Vector2Int(x, y));

                // Una celda vacia significa que aun estan cayendo fichas;
                // en ese caso no es momento de dar la partida por perdida.
                if (block == null)
                    return true;

                types[x, y] = block.BlockType;
            }

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (x + 1 < width && SwapMakesMatch(x, y, x + 1, y))
                    return true;

                if (y + 1 < height && SwapMakesMatch(x, y, x, y + 1))
                    return true;
            }

            return false;
        }

        /// <summary>Prueba un intercambio sobre la copia y mira si formaria linea.</summary>
        private bool SwapMakesMatch(int firstX, int firstY, int secondX, int secondY)
        {
            (types[firstX, firstY], types[secondX, secondY]) =
                (types[secondX, secondY], types[firstX, firstY]);

            bool match = MakesLine(firstX, firstY) || MakesLine(secondX, secondY);

            (types[firstX, firstY], types[secondX, secondY]) =
                (types[secondX, secondY], types[firstX, firstY]);

            return match;
        }

        /// <summary>Dice si esa celda queda dentro de tres o mas iguales.</summary>
        private bool MakesLine(int x, int y)
        {
            int type = types[x, y];
            int horizontal = 1 + CountSame(x, y, -1, 0, type) + CountSame(x, y, 1, 0, type);

            if (horizontal >= 3)
                return true;

            int vertical = 1 + CountSame(x, y, 0, -1, type) + CountSame(x, y, 0, 1, type);
            return vertical >= 3;
        }

        /// <summary>Cuenta cuantas fichas iguales seguidas hay en una direccion.</summary>
        private int CountSame(int x, int y, int stepX, int stepY, int type)
        {
            int count = 0;
            int currentX = x + stepX;
            int currentY = y + stepY;

            while (currentX >= 0 && currentX < types.GetLength(0) &&
                   currentY >= 0 && currentY < types.GetLength(1) &&
                   types[currentX, currentY] == type)
            {
                count++;
                currentX += stepX;
                currentY += stepY;
            }

            return count;
        }
    }
}
