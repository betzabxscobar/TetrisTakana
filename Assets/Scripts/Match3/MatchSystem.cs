using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace TetrisTakana.Match3
{
    public class MatchSystem : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private Gravity gravity;
        [SerializeField] private Spawner spawner;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private ComboSystem comboSystem;
        [SerializeField] private DifficultySystem difficulty;
        [SerializeField] private SwapSystem swapSystem;
        [SerializeField] private bool rejectSwapWithoutMatch = true;

        private bool resolving;
        private bool reverting;

        public bool IsResolving => resolving;
        public event Action<int> MatchResolved;

        private void Awake()
        {
            board ??= GetComponent<Board>();
            gravity ??= GetComponent<Gravity>();
            spawner ??= GetComponent<Spawner>();
            scoreManager ??= GetComponent<ScoreManager>();
            comboSystem ??= GetComponent<ComboSystem>();
            difficulty ??= GetComponent<DifficultySystem>();
            swapSystem ??= GetComponent<SwapSystem>();
        }

        private void OnEnable()
        {
            if (board != null)
                board.BlocksSwapped += HandleSwap;
        }

        private void OnDisable()
        {
            if (board != null)
                board.BlocksSwapped -= HandleSwap;
        }

        private void HandleSwap(Vector2Int first, Vector2Int second)
        {
            if (reverting || resolving)
                return;

            StartCoroutine(ResolveAfterSwap(first, second));
        }

        private IEnumerator ResolveAfterSwap(Vector2Int first, Vector2Int second)
        {
            resolving = true;
            if (swapSystem != null)
                yield return new WaitForSeconds(swapSystem.AnimationDuration);

            HashSet<Vector2Int> matches = FindMatches();
            if (matches.Count == 0)
            {
                comboSystem?.ResetCombo();

                if (rejectSwapWithoutMatch)
                {
                    reverting = true;
                    if (swapSystem != null)
                        swapSystem.TrySwap(first, second);
                    else
                        board.TrySwap(first, second);
                    reverting = false;
                    if (swapSystem != null)
                        yield return new WaitForSeconds(swapSystem.AnimationDuration);
                }

                resolving = false;
                yield break;
            }

            comboSystem?.ResetCombo();
            yield return ResolveCascades(matches);
            resolving = false;
        }

        private IEnumerator ResolveCascades(HashSet<Vector2Int> matches, bool award = true)
        {
            while (matches.Count > 0)
            {
                int combo = comboSystem != null ? comboSystem.RegisterCascade() : 1;

                foreach (Vector2Int position in matches)
                    board.RemoveBlock(position);

                // El tablero recien generado puede traer combinaciones hechas;
                // se limpian sin regalar puntos ni subir de nivel.
                if (award)
                {
                    scoreManager?.AddMatch(matches.Count, combo);
                    difficulty?.NotifyClear(matches.Count);
                    MatchResolved?.Invoke(matches.Count);
                }

                yield return null;

                if (gravity != null)
                    yield return gravity.ApplyGravity();
                if (spawner != null)
                    yield return spawner.FillEmpty();

                yield return null;
                matches = FindMatches();
            }
        }

        /// <summary>
        /// Resuelve las combinaciones que ya haya en el tablero, sin venir de
        /// un intercambio. Lo usa el giro del reloj de arena: al rotar la
        /// rejilla pueden quedar tres en raya que nadie ha buscado.
        /// </summary>
        public IEnumerator ResolveExisting(bool award = true)
        {
            if (board == null || resolving)
                yield break;

            HashSet<Vector2Int> matches = FindMatches();

            if (matches.Count == 0)
                yield break;

            resolving = true;
            comboSystem?.ResetCombo();
            yield return ResolveCascades(matches, award);
            resolving = false;
        }

        public HashSet<Vector2Int> FindMatches()
        {
            HashSet<Vector2Int> matches = new HashSet<Vector2Int>();

            if (board == null)
                return matches;

            // Barrido lineal: se recorre cada fila y cada columna una sola vez
            // acumulando la racha del tipo actual. La version anterior usaba un
            // bucle anidado que no avanzaba el indice cuando la celda estaba
            // vacia, y como las celdas se vacian justo al eliminar una
            // combinacion, el juego se colgaba en el primer match.
            for (int y = 0; y < board.Height; y++)
                ScanLine(matches, new Vector2Int(0, y), Vector2Int.right, board.Width);

            for (int x = 0; x < board.Width; x++)
                ScanLine(matches, new Vector2Int(x, 0), Vector2Int.up, board.Height);

            return matches;
        }

        /// <summary>
        /// Recorre una fila o columna acumulando fichas del mismo tipo y marca
        /// las rachas de tres o mas. Una celda vacia corta la racha.
        /// </summary>
        private void ScanLine(
            HashSet<Vector2Int> matches,
            Vector2Int origin,
            Vector2Int step,
            int length)
        {
            int runType = -1;
            int runStart = 0;

            for (int index = 0; index <= length; index++)
            {
                int type = -1;

                if (index < length)
                {
                    BoardBlock block = board.GetBlock(origin + step * index);
                    type = block != null ? block.BlockType : -1;
                }

                if (type == runType && runType >= 0)
                    continue;

                if (runType >= 0 && index - runStart >= 3)
                    for (int marked = runStart; marked < index; marked++)
                        matches.Add(origin + step * marked);

                runType = type;
                runStart = index;
            }
        }
    }
}
