using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>Encuentra las combinaciones de tres o mas y las resuelve en cascada.</summary>
    public class MatchSystem : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private Gravity gravity;
        [SerializeField] private Spawner spawner;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private ComboSystem comboSystem;
        [SerializeField] private DifficultySystem difficulty;
        [SerializeField] private SwapSystem swapSystem;
        [Tooltip("Las bombas del tablero. Vacio: el modo va sin bombas.")]
        [SerializeField] private BombSystem bombSystem;
        [Tooltip("El reventon de las fichas y la sacudida. Vacio: desaparecen secas.")]
        [SerializeField] private BoardJuice juice;
        [SerializeField] private bool rejectSwapWithoutMatch = true;
        [Tooltip("Rellena por arriba tras cada combinacion. Con la pila que sube va apagado: los huecos los cierra la fila nueva.")]
        [SerializeField] private bool refillAfterMatch;

        private bool resolving;
        private bool reverting;

        public bool IsResolving => resolving;
        public event Action<int> MatchResolved;

        /// <summary>Busca en el propio objeto los sistemas que no vengan asignados.</summary>
        private void Awake()
        {
            board ??= GetComponent<Board>();
            gravity ??= GetComponent<Gravity>();
            spawner ??= GetComponent<Spawner>();
            scoreManager ??= GetComponent<ScoreManager>();
            comboSystem ??= GetComponent<ComboSystem>();
            difficulty ??= GetComponent<DifficultySystem>();
            swapSystem ??= GetComponent<SwapSystem>();
            bombSystem ??= GetComponent<BombSystem>();
            juice ??= GetComponent<BoardJuice>();
        }

        /// <summary>Se pone a escuchar los intercambios del tablero.</summary>
        private void OnEnable()
        {
            if (board != null)
                board.BlocksSwapped += HandleSwap;
        }

        /// <summary>Deja de escuchar los intercambios del tablero.</summary>
        private void OnDisable()
        {
            if (board != null)
                board.BlocksSwapped -= HandleSwap;
        }

        /// <summary>Alguien intercambio dos fichas: toca comprobar si sale combinacion.</summary>
        private void HandleSwap(Vector2Int first, Vector2Int second)
        {
            if (reverting || resolving)
                return;

            StartCoroutine(ResolveAfterSwap(first, second));
        }

        /// <summary>Resuelve la jugada, y deshace el intercambio si no formo nada.</summary>
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

        /// <summary>Borra las combinaciones, deja caer y repite mientras sigan saliendo.</summary>
        private IEnumerator ResolveCascades(HashSet<Vector2Int> matches, bool award = true)
        {
            while (matches.Count > 0)
            {
                int combo = comboSystem != null ? comboSystem.RegisterCascade() : 1;

                // Las bombas se resuelven antes de borrar: cargan con esta
                // combinacion y, si les toca reventar, meten sus celdas en el
                // mismo conjunto. Asi todo cae de una vez y la explosion puntua
                // dentro de la jugada en vez de como una cascada aparte.
                int exploded = 0;

                if (bombSystem != null)
                    exploded = bombSystem.ExpandWithBombs(matches);

                // Centro de lo que se rompe, para sacar ahi el texto de puntos.
                // Se calcula ya expandido con las bombas y antes de borrar, que
                // despues las celdas estan vacias.
                Vector3 center = Vector3.zero;

                foreach (Vector2Int position in matches)
                    center += board.GridToWorld(position);

                center /= matches.Count;

                // Con efectos la ficha no se destruye aqui: se le entrega al
                // BoardJuice, que la revienta y ya la borra el. El tablero la
                // suelta igualmente en este mismo instante, asi que la gravedad
                // y la siguiente busqueda no esperan a la animacion.
                int order = 0;

                foreach (Vector2Int position in matches)
                {
                    BoardBlock cleared = board.RemoveBlock(position, juice == null);

                    if (juice != null)
                        juice.PopBlock(cleared, order++);
                }

                if (juice != null)
                {
                    // La sacudida sube con lo gordo que sea el golpe: un tres en
                    // raya apenas se nota y una cascada con bombas zarandea.
                    float force = Mathf.Min(2.5f, 0.35f + matches.Count * 0.06f + combo * 0.12f);
                    juice.Shake(exploded > 0 ? force + 0.8f : force);
                }

                // El tablero recien generado puede traer combinaciones hechas;
                // se limpian sin regalar puntos ni subir de nivel.
                if (award)
                {
                    // Se mira el marcador antes y despues para saber cuantos
                    // puntos ha dado justo esta jugada: AddMatch no lo devuelve,
                    // y el texto flotante tiene que enseñar lo que se acaba de
                    // ganar, no el total.
                    int before = scoreManager != null ? scoreManager.Score : 0;
                    scoreManager?.AddMatch(matches.Count, combo);
                    difficulty?.NotifyClear(matches.Count);
                    MatchResolved?.Invoke(matches.Count);

                    if (juice != null && scoreManager != null)
                        juice.ShowScore(center, scoreManager.Score - before, combo);
                }

                yield return null;

                if (gravity != null)
                    yield return gravity.ApplyGravity();

                if (refillAfterMatch && spawner != null)
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

        /// <summary>Devuelve todas las celdas que forman parte de una combinacion.</summary>
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
