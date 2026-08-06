using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// El reloj de arena: cada cierto tiempo el tablero da media vuelta. La
    /// pila que descansaba en el suelo queda arriba y vuelve a caer, igual que
    /// la arena cuando se le da la vuelta al reloj.
    ///
    /// El giro se hace en dos tiempos. Primero rota el transform del tablero
    /// 180 grados sobre su centro, que es puro efecto visual porque la matriz
    /// de celdas vive en coordenadas de rejilla. Al llegar al final se
    /// enderaza el transform y se reflejan las celdas en el mismo fotograma:
    /// los bloques no se mueven ni un pixel en pantalla, pero ahora el tablero
    /// esta derecho con la pila arriba, lista para caer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardFlipSystem : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private BoardGame game;
        [SerializeField] private Board board;
        [SerializeField] private BoardCameraFitter cameraFitter;

        [Header("Modo Tetris")]
        [SerializeField] private LineClearSystem lineClear;

        [Header("Modo match-3")]
        [SerializeField] private Match3.MatchSystem matchSystem;
        [Tooltip("Filas que deben quedar libres sobre la pila despues de girar.")]
        [SerializeField, Min(0)] private int freeRowsAfterFlip = 3;

        [Header("Reloj")]
        [Tooltip("Segundos de cada vuelta del reloj.")]
        [SerializeField, Min(1f)] private float secondsPerFlip = 30f;
        [SerializeField] private bool enabledFromStart = true;

        [Header("Animacion")]
        [SerializeField, Min(0.05f)] private float rotationDuration = 0.7f;
        [SerializeField, Min(0f)] private float settleDuration = 0.35f;
        [Tooltip("Pausa entre el giro y la caida, para que se lea el cambio.")]
        [SerializeField, Min(0f)] private float pauseBeforeSettle = 0.15f;

        private readonly List<BoardBlock> column = new List<BoardBlock>();
        private readonly List<FallingBlock> falling = new List<FallingBlock>();

        private BoardBlock[,] snapshot;
        private Coroutine flipRoutine;
        private Vector3 worldCenter;
        private Quaternion restRotation;
        private TetrisGame.GameState lastState = TetrisGame.GameState.Ready;
        private int originalWidth;
        private int originalHeight;
        private float remaining;

        /// <summary>Segundos que faltan para el proximo giro.</summary>
        public float Remaining => remaining;

        /// <summary>Cuenta atras normalizada: 1 recien girado, 0 a punto de girar.</summary>
        public float RemainingNormalized =>
            secondsPerFlip > 0f ? Mathf.Clamp01(remaining / secondsPerFlip) : 0f;

        public bool IsFlipping => flipRoutine != null;

        public event Action<float> TimeChanged;
        public event Action FlipStarted;
        public event Action FlipFinished;

        private struct FallingBlock
        {
            public BoardBlock Block;
            public Vector3 From;
            public Vector3 To;
        }

        private void Awake()
        {
            game ??= FindAnyObjectByType<BoardGame>();
            board ??= GetComponent<Board>() ?? FindAnyObjectByType<Board>();
            lineClear ??= GetComponent<LineClearSystem>();
            matchSystem ??= GetComponent<Match3.MatchSystem>();
            cameraFitter ??= FindAnyObjectByType<BoardCameraFitter>();

            if (board != null)
            {
                // El centro del tablero es el ancla de todo: al girar 90 grados
                // el alto y el ancho se intercambian, asi que la esquina de
                // origen se mueve pero el centro debe quedarse donde estaba.
                restRotation = board.transform.rotation;
                worldCenter = board.transform.position + restRotation * LocalCenter();
                originalWidth = board.Width;
                originalHeight = board.Height;
            }

            ResetTimer();
        }

        private void OnEnable()
        {
            if (game != null)
                game.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (game != null)
                game.StateChanged -= HandleStateChanged;

            StopFlip();
        }

        private void Update()
        {
            if (!enabledFromStart || game == null || board == null)
                return;

            if (game.State != TetrisGame.GameState.Playing || IsFlipping)
                return;

            // Girar con lineas a medio resolver dejaria el tablero incoherente,
            // igual que pasa con la pausa.
            if (game.IsBusy)
                return;

            // Time.deltaTime vale cero con la partida en pausa, asi que el
            // reloj se congela solo mientras la tarjeta esta en pantalla.
            remaining -= Time.deltaTime;
            TimeChanged?.Invoke(RemainingNormalized);

            if (remaining <= 0f)
                flipRoutine = StartCoroutine(FlipRoutine());
        }

        private void HandleStateChanged(TetrisGame.GameState state)
        {
            if (state == TetrisGame.GameState.GameOver ||
                state == TetrisGame.GameState.Ready)
            {
                StopFlip();
                ResetTimer();
            }
            // Volver de la pausa no es empezar de nuevo: ahi el reloj sigue
            // donde estaba. Cualquier otra entrada en Playing si es partida
            // nueva y merece el reloj lleno.
            else if (state == TetrisGame.GameState.Playing &&
                     lastState != TetrisGame.GameState.Paused)
            {
                StopFlip();
                ResetTimer();

                // Partida nueva: el tablero vuelve a su orientacion original.
                // Se hace aqui y no al perder porque StartGame ya ha vaciado la
                // rejilla, y redimensionar con bloques dentro los dejaria
                // sueltos en la escena.
                ResetOrientation();
            }

            lastState = state;
        }

        public void ResetTimer()
        {
            remaining = secondsPerFlip;
            TimeChanged?.Invoke(RemainingNormalized);
        }

        /// <summary>Devuelve el tablero a las medidas con las que empezo.</summary>
        public void ResetOrientation()
        {
            if (board == null ||
                (board.Width == originalWidth && board.Height == originalHeight))
                return;

            board.SetDimensions(originalWidth, originalHeight);
            RestoreTransform();
            RefitCamera();
        }

        private void StopFlip()
        {
            if (flipRoutine != null)
            {
                StopCoroutine(flipRoutine);
                flipRoutine = null;
            }

            RestoreTransform();

            if (game != null)
                game.SetHold(false);
        }

        private Vector3 LocalCenter()
        {
            return board == null
                ? Vector3.zero
                : new Vector3(
                    board.Width * board.CellSize * 0.5f,
                    board.Height * board.CellSize * 0.5f,
                    0f);
        }

        /// <summary>
        /// Endereza el tablero dejando su centro en el mismo punto del mundo,
        /// sean cuales sean las dimensiones que tenga en ese momento.
        /// </summary>
        private void RestoreTransform()
        {
            if (board == null)
                return;

            board.transform.SetPositionAndRotation(
                worldCenter - restRotation * LocalCenter(),
                restRotation);
        }

        // --- El giro -----------------------------------------------------

        private IEnumerator FlipRoutine()
        {
            game.SetHold(true);
            FlipStarted?.Invoke();

            SinkCurrentPiece();

            yield return AnimateRotation();

            // Todo esto pasa en el mismo fotograma, antes de dibujar, asi que
            // en pantalla no se ve ningun salto: el tablero queda derecho con
            // el alto y el ancho intercambiados y cada bloque en su celda nueva.
            RotateCells();
            RefitCamera();

            if (pauseBeforeSettle > 0f)
                yield return new WaitForSeconds(pauseBeforeSettle);

            yield return SettleStack();

            if (game is TetrisGame tetris)
            {
                yield return ResolveLinesAfterFlip(tetris);

                // La pieza que se hundio al empezar el giro ya es parte de la
                // pila, asi que hace falta una nueva para seguir jugando.
                tetris.SpawnOrEnd();
            }
            else
            {
                yield return ResolveMatchesAfterFlip();
            }

            remaining = secondsPerFlip;
            TimeChanged?.Invoke(RemainingNormalized);
            flipRoutine = null;
            game.SetHold(false);
            FlipFinished?.Invoke();
        }

        /// <summary>
        /// Gira el tablero sobre su centro. Como el pivote del transform esta
        /// en la esquina inferior izquierda, hay que recolocar la posicion en
        /// cada paso para que el centro no se mueva.
        /// </summary>
        private IEnumerator AnimateRotation()
        {
            Transform boardTransform = board.transform;
            Vector3 localCenter = LocalCenter();
            float elapsed = 0f;

            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / rotationDuration);
                float eased = progress * progress * (3f - 2f * progress);
                Quaternion rotation =
                    restRotation * Quaternion.Euler(0f, 0f, -90f * eased);

                boardTransform.SetPositionAndRotation(
                    worldCenter - rotation * localCenter,
                    rotation);
                yield return null;
            }

            Quaternion quarter = restRotation * Quaternion.Euler(0f, 0f, -90f);
            boardTransform.SetPositionAndRotation(
                worldCenter - quarter * localCenter,
                quarter);
        }

        /// <summary>
        /// Gira la rejilla un cuarto de vuelta en el sentido de las agujas del
        /// reloj: lo que estaba en (x, y) pasa a (y, ancho-1-x), y el tablero
        /// intercambia alto y ancho. Combinado con enderezar el transform, los
        /// bloques se quedan justo donde el jugador los estaba viendo.
        /// </summary>
        private void RotateCells()
        {
            int width = board.Width;
            int height = board.Height;

            if (snapshot == null ||
                snapshot.GetLength(0) != width ||
                snapshot.GetLength(1) != height)
                snapshot = new BoardBlock[width, height];

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                snapshot[x, y] = board.GetBlock(cell);

                // Sin destruir: los bloques se reutilizan en su celda nueva.
                if (snapshot[x, y] != null)
                    board.RemoveBlock(cell, false);
            }

            // La rejilla nueva es alto x ancho, y hay que enderezar el tablero
            // con esas medidas antes de recolocar nada: SetBlock posiciona los
            // bloques a traves del transform.
            board.SetDimensions(height, width);
            RestoreTransform();

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                BoardBlock block = snapshot[x, y];
                snapshot[x, y] = null;

                if (block == null)
                    continue;

                board.SetBlock(new Vector2Int(y, width - 1 - x), block, true);
            }

            snapshot = null;
        }

        /// <summary>Deja caer toda la pila hasta el nuevo suelo.</summary>
        private IEnumerator SettleStack()
        {
            falling.Clear();

            for (int x = 0; x < board.Width; x++)
            {
                column.Clear();

                for (int y = 0; y < board.Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    BoardBlock block = board.GetBlock(cell);

                    if (block == null)
                        continue;

                    column.Add(block);
                    board.SetBlock(cell, null);
                }

                for (int index = 0; index < column.Count; index++)
                {
                    BoardBlock block = column[index];
                    Vector2Int destination = new Vector2Int(x, index);
                    Vector3 from = block.transform.position;

                    board.SetBlock(destination, block, false);

                    Vector3 to = board.GridToWorld(destination);

                    if ((to - from).sqrMagnitude > 0.0001f)
                        falling.Add(new FallingBlock
                        {
                            Block = block,
                            From = from,
                            To = to
                        });
                    else
                        block.transform.position = to;
                }
            }

            yield return AnimateFall();
        }

        private IEnumerator AnimateFall()
        {
            if (falling.Count == 0)
                yield break;

            if (settleDuration > 0f)
            {
                float elapsed = 0f;

                while (elapsed < settleDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / settleDuration);

                    // Cuadratica: arranca lento y acelera, como una caida.
                    foreach (FallingBlock fall in falling)
                        if (fall.Block != null)
                            fall.Block.transform.position = Vector3.Lerp(
                                fall.From,
                                fall.To,
                                progress * progress);

                    yield return null;
                }
            }

            foreach (FallingBlock fall in falling)
                if (fall.Block != null)
                    fall.Block.transform.position = fall.To;

            falling.Clear();
        }

        /// <summary>
        /// La pieza en juego no vive en la matriz, y tras el cuarto de vuelta
        /// sus coordenadas ya no significan lo mismo. En lugar de recolocarla a
        /// ciegas se fija donde esta: al volcar el reloj, lo que tenias en la
        /// mano se vuelve arena y gira con el resto de la pila.
        /// </summary>
        private void SinkCurrentPiece()
        {
            if (game is not TetrisGame tetris)
                return;

            PieceSpawner spawner = tetris.Spawner;
            Tetromino piece = spawner != null ? spawner.CurrentPiece : null;

            if (piece == null)
                return;

            // Si no puede fijarse donde esta, se descarta para no dejar bloques
            // sueltos fuera de la rejilla.
            if (!piece.TryLock())
                spawner.ClearCurrentPiece();
        }

        /// <summary>
        /// La pila recolocada puede haber completado filas. Se resuelven aqui
        /// mismo y puntuan igual que las de una jugada normal.
        /// </summary>
        private IEnumerator ResolveLinesAfterFlip(TetrisGame tetris)
        {
            if (lineClear == null)
                yield break;

            yield return lineClear.ClearFullLines();

            int cleared = lineClear.LastClearedCount;

            if (cleared <= 0)
                yield break;

            int level = tetris.Difficulty != null ? tetris.Difficulty.Level : 1;
            tetris.Score?.AddLines(cleared, level);
            tetris.Difficulty?.NotifyLinesCleared(cleared);
        }

        /// <summary>
        /// En match-3 el giro no reparte fichas nuevas: rellenar aqui las
        /// celdas vacias dejaba el tablero lleno hasta el techo y la partida se
        /// perdia sola en el primer empujon de la pila. Los huecos los cierra
        /// la fila que entra por abajo; el giro solo resuelve las
        /// combinaciones que la rotacion haya dejado alineadas.
        /// </summary>
        private IEnumerator ResolveMatchesAfterFlip()
        {
            SpillOverflow();

            if (matchSystem != null)
                yield return matchSystem.ResolveExisting();
        }

        /// <summary>
        /// El cuarto de vuelta intercambia alto y ancho, asi que una pila que
        /// iba holgada en vertical puede rozar el techo en horizontal. Lo que
        /// no cabe se derrama, como la arena que se sale al volcar el reloj, y
        /// asi siempre queda sitio para seguir jugando.
        /// </summary>
        private void SpillOverflow()
        {
            if (board == null || freeRowsAfterFlip <= 0)
                return;

            int limit = board.Height - freeRowsAfterFlip;

            if (limit <= 0)
                return;

            for (int x = 0; x < board.Width; x++)
            for (int y = limit; y < board.Height; y++)
                board.RemoveBlock(new Vector2Int(x, y));
        }

        private void RefitCamera()
        {
            // El encuadre solo se recalcula al cambiar el tamaño de ventana,
            // asi que hay que pedirselo a mano despues de tocar el tablero.
            if (cameraFitter != null)
                cameraFitter.Fit();
        }
    }
}
