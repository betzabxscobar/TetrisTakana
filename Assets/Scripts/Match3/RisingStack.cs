using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// La pila que sube. Cada cierto tiempo entra una fila nueva por abajo y
    /// todo lo demas se desplaza una celda hacia arriba. Si algo llega al
    /// techo, se acabo la partida. El ritmo lo marca el nivel, que sube con
    /// los puntos, asi que cuanto mejor juegas mas te aprieta.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RisingStack : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] private Board board;
        [SerializeField] private Spawner spawner;
        [SerializeField] private MatchSystem matchSystem;
        [SerializeField] private DifficultySystem difficulty;
        [SerializeField] private Match3Game game;

        [Header("Ritmo")]
        [Tooltip("Se usa si no hay DifficultySystem asignado.")]
        [SerializeField, Min(0.2f)] private float fallbackInterval = 6f;
        [SerializeField, Min(0f)] private float pushDuration = 0.22f;

        private readonly List<Moving> moving = new List<Moving>();
        private Coroutine pushRoutine;
        private float timer;

        /// <summary>Segundos que faltan para la proxima fila.</summary>
        public float Remaining => Mathf.Max(0f, CurrentInterval - timer);

        public float CurrentInterval =>
            difficulty != null ? difficulty.RiseInterval : fallbackInterval;

        /// <summary>La pila toco el techo.</summary>
        public event Action ToppedOut;

        public event Action RowPushed;

        private struct Moving
        {
            public BoardBlock Block;
            public Vector3 From;
            public Vector3 To;
        }

        private void Awake()
        {
            board ??= GetComponent<Board>();
            spawner ??= GetComponent<Spawner>();
            matchSystem ??= GetComponent<MatchSystem>();
            difficulty ??= GetComponent<DifficultySystem>();
            game ??= GetComponent<Match3Game>();
        }

        private void Update()
        {
            if (board == null || spawner == null || game == null)
                return;

            if (game.State != BoardGame.GameState.Playing || pushRoutine != null)
                return;

            // Mientras caen fichas o gira el tablero, el reloj de la pila se
            // detiene: empujar a media cascada dejaria celdas incoherentes.
            if (game.IsBusy)
                return;

            timer += Time.deltaTime;

            if (timer < CurrentInterval)
                return;

            timer = 0f;
            pushRoutine = StartCoroutine(PushRow());
        }

        public void ResetTimer()
        {
            timer = 0f;

            if (pushRoutine == null)
                return;

            StopCoroutine(pushRoutine);
            pushRoutine = null;
        }

        private IEnumerator PushRow()
        {
            // SetHold y no SetBusy: Match3Game reescribe busy cada frame con el
            // estado de las cascadas, y borraria la retencion a media subida.
            game.SetHold(true);

            // Si la fila de arriba ya tiene algo, subir la sacaria del tablero.
            if (TopRowOccupied())
            {
                game.SetHold(false);
                pushRoutine = null;
                ToppedOut?.Invoke();
                yield break;
            }

            ShiftEverythingUp();
            BuildBottomRow();

            yield return AnimatePush();

            RowPushed?.Invoke();

            // La fila nueva puede cerrar combinaciones; estas si puntuan.
            if (matchSystem != null)
                yield return matchSystem.ResolveExisting();

            game.SetHold(false);
            pushRoutine = null;
        }

        private bool TopRowOccupied()
        {
            int top = board.Height - 1;

            for (int x = 0; x < board.Width; x++)
                if (board.IsOccupied(new Vector2Int(x, top)))
                    return true;

            return false;
        }

        /// <summary>
        /// Desplaza de arriba abajo para no pisar celdas todavia sin leer.
        /// </summary>
        private void ShiftEverythingUp()
        {
            moving.Clear();

            for (int y = board.Height - 2; y >= 0; y--)
            for (int x = 0; x < board.Width; x++)
            {
                Vector2Int source = new Vector2Int(x, y);
                BoardBlock block = board.GetBlock(source);

                if (block == null)
                    continue;

                Vector2Int destination = new Vector2Int(x, y + 1);
                Vector3 from = block.transform.position;

                board.SetBlock(source, null);
                board.SetBlock(destination, block, false);

                moving.Add(new Moving
                {
                    Block = block,
                    From = from,
                    To = board.GridToWorld(destination)
                });
            }
        }

        private void BuildBottomRow()
        {
            for (int x = 0; x < board.Width; x++)
            {
                Vector2Int position = new Vector2Int(x, 0);

                if (board.IsOccupied(position))
                    continue;

                BoardBlock block = spawner.CreateLooseBlock(position);

                if (block == null)
                    continue;

                board.SetBlock(position, block, false);

                Vector3 target = board.GridToWorld(position);
                Vector3 from = target - Vector3.up * board.CellSize;
                block.transform.position = from;

                moving.Add(new Moving
                {
                    Block = block,
                    From = from,
                    To = target
                });
            }
        }

        private IEnumerator AnimatePush()
        {
            if (moving.Count == 0 || pushDuration <= 0f)
            {
                SnapAll();
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < pushDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / pushDuration);

                foreach (Moving move in moving)
                    if (move.Block != null)
                        move.Block.transform.position =
                            Vector3.Lerp(move.From, move.To, progress);

                yield return null;
            }

            SnapAll();
        }

        private void SnapAll()
        {
            foreach (Moving move in moving)
                if (move.Block != null)
                    move.Block.transform.position = move.To;

            moving.Clear();
        }
    }
}
