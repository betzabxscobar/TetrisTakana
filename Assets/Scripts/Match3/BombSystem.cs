using System.Collections.Generic;
using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// Las bombas del tablero: las coloca, les va sumando cargas y las hace
    /// reventar llevandose todo lo que tengan alrededor.
    ///
    /// La regla es que cada combinacion que el jugador encaje pegada a una
    /// bomba le suma una carga, y a la tercera explota en un cuadro de 3x3.
    /// Una bomba que caiga dentro del cuadro de otra revienta en el acto, asi
    /// que se pueden encadenar.
    ///
    /// Trabaja sobre el conjunto de celdas que <see cref="MatchSystem"/> esta a
    /// punto de romper, y le añade las suyas. Se hace asi y no borrando por su
    /// cuenta para que todo caiga en la misma pasada: una sola gravedad, una
    /// sola puntuacion y un solo combo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BombSystem : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] private Board board;
        [SerializeField] private RisingStack risingStack;

        [Header("Aparicion")]
        [Tooltip("Probabilidad de que una fila nueva traiga bomba, de 0 a 1.")]
        [SerializeField, Range(0f, 1f)] private float bombChance = 0.18f;
        [Tooltip("Bombas que puede haber a la vez en el tablero.")]
        [SerializeField, Min(1)] private int maxBombs = 3;
        [Tooltip("Apagado: la bomba solo aparece en la fila que acaba de entrar.")]
        [SerializeField] private bool spawnAnywhere = true;
        [Tooltip("Filas de abajo que se evitan: ahi la bomba dura muy poco.")]
        [SerializeField, Min(0)] private int avoidBottomRows = 1;

        [Header("Explosion")]
        [Tooltip("Celdas a cada lado. 1 da el cuadro de 3x3.")]
        [SerializeField, Min(1)] private int blastRadius = 1;
        [Tooltip("Fotogramas del fogonazo, en orden.")]
        [SerializeField] private Sprite[] blastFrames;
        [SerializeField, Min(0.01f)] private float blastFrameTime = 0.045f;
        [SerializeField] private int blastSortingOrder = 20;

        [Header("Poses de la bomba")]
        [SerializeField] private Sprite restingPose;
        [Tooltip("Una por carga, en orden. Su numero decide cuantas aguanta.")]
        [SerializeField] private Sprite[] chargePoses;
        [SerializeField] private Sprite[] spawnPoses;
        [SerializeField, Min(0.01f)] private float spawnFrameTime = 0.05f;
        [Tooltip("Tamaño de la bomba respecto a la celda. 1 la deja como un bloque.")]
        [SerializeField, Range(0.5f, 2f)] private float bombFill = 1.15f;

        private readonly List<BombBlock> live = new List<BombBlock>();

        /// <summary>Bombas que hay ahora mismo sobre el tablero.</summary>
        public int LiveBombs
        {
            get
            {
                live.RemoveAll(b => b == null);
                return live.Count;
            }
        }

        private void Awake()
        {
            board ??= GetComponent<Board>();
            risingStack ??= GetComponent<RisingStack>();
        }

        private void OnEnable()
        {
            if (risingStack != null)
                risingStack.RowPushed += HandleRowPushed;
        }

        private void OnDisable()
        {
            if (risingStack != null)
                risingStack.RowPushed -= HandleRowPushed;
        }

        // --- Aparicion --------------------------------------------------------

        /// <summary>
        /// Ha entrado una fila por abajo: puede que salga bomba. Se planta
        /// sobre una ficha ya creada en vez de crear una pieza aparte, asi la
        /// bomba se comporta como cualquier otra ficha del tablero.
        ///
        /// El sitio se sortea entre todas las fichas del tablero, no solo entre
        /// las de la fila nueva: apareciendo siempre abajo la bomba salia
        /// siempre por el mismo borde y se veia repetitivo. Se saltan las
        /// primeras filas porque ahi la pila se la lleva enseguida y al jugador
        /// no le da tiempo a cargarla.
        /// </summary>
        private void HandleRowPushed()
        {
            if (board == null || LiveBombs >= maxBombs)
                return;

            if (Random.value > bombChance)
                return;

            List<Vector2Int> candidates = new List<Vector2Int>();
            int firstRow = spawnAnywhere ? Mathf.Min(avoidBottomRows, board.Height - 1) : 0;
            int lastRow = spawnAnywhere ? board.Height - 1 : 0;

            for (int y = firstRow; y <= lastRow; y++)
            for (int x = 0; x < board.Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                BoardBlock block = board.GetBlock(cell);

                if (block != null && block.GetComponent<BombBlock>() == null)
                    candidates.Add(cell);
            }

            if (candidates.Count == 0)
                return;

            Plant(candidates[Random.Range(0, candidates.Count)]);
        }

        /// <summary>Convierte la ficha de una celda en bomba.</summary>
        public BombBlock Plant(Vector2Int cell)
        {
            BoardBlock block = board != null ? board.GetBlock(cell) : null;

            if (block == null || block.GetComponent<BombBlock>() != null)
                return null;

            BombBlock bomb = block.gameObject.AddComponent<BombBlock>();
            bomb.Configure(
                restingPose,
                chargePoses,
                spawnPoses,
                spawnFrameTime,
                board.CellSize,
                bombFill);
            live.Add(bomb);
            return bomb;
        }

        // --- Cargas y explosion ----------------------------------------------

        /// <summary>
        /// Le añade al conjunto de celdas que se van a romper las que se lleven
        /// las bombas. Devuelve cuantas han reventado.
        ///
        /// Lo llama <see cref="MatchSystem"/> justo antes de borrar, con la
        /// combinacion ya encontrada.
        /// </summary>
        public int ExpandWithBombs(HashSet<Vector2Int> cells)
        {
            if (board == null || cells == null || cells.Count == 0)
                return 0;

            HashSet<BombBlock> detonated = new HashSet<BombBlock>();
            Queue<BombBlock> pending = new Queue<BombBlock>();
            HashSet<BombBlock> touched = new HashSet<BombBlock>();

            // Una carga por combinacion y no una por ficha rota: si no, una
            // jugada que rozase tres celdas de la bomba la volaba de golpe.
            foreach (Vector2Int cell in cells)
            {
                BombBlock own = BombAt(cell);

                if (own != null)
                {
                    // La combinacion se lleva la propia bomba: revienta ya.
                    if (detonated.Add(own))
                        pending.Enqueue(own);

                    continue;
                }

                foreach (Vector2Int around in Around(cell))
                {
                    if (cells.Contains(around))
                        continue;

                    BombBlock neighbour = BombAt(around);

                    if (neighbour != null)
                        touched.Add(neighbour);
                }
            }

            foreach (BombBlock bomb in touched)
            {
                if (detonated.Contains(bomb))
                    continue;

                if (bomb.AddCharge() && detonated.Add(bomb))
                    pending.Enqueue(bomb);
            }

            // Encadenado: una bomba que pille el cuadro de otra no espera cargas.
            int exploded = 0;

            while (pending.Count > 0)
            {
                BombBlock bomb = pending.Dequeue();
                Vector2Int center = bomb.GridPosition;
                exploded++;
                PlayBlast(center);

                for (int dx = -blastRadius; dx <= blastRadius; dx++)
                for (int dy = -blastRadius; dy <= blastRadius; dy++)
                {
                    Vector2Int cell = new Vector2Int(center.x + dx, center.y + dy);

                    if (!board.IsInside(cell))
                        continue;

                    BombBlock caught = BombAt(cell);

                    if (caught != null && caught != bomb && detonated.Add(caught))
                        pending.Enqueue(caught);

                    cells.Add(cell);
                }
            }

            if (exploded > 0)
                live.RemoveAll(b => b == null || detonated.Contains(b));

            return exploded;
        }

        /// <summary>La bomba que haya en esa celda, o null.</summary>
        private BombBlock BombAt(Vector2Int cell)
        {
            BoardBlock block = board.GetBlock(cell);
            return block != null ? block.GetComponent<BombBlock>() : null;
        }

        /// <summary>Las ocho celdas que rodean a una.</summary>
        private static IEnumerable<Vector2Int> Around(Vector2Int cell)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                yield return new Vector2Int(cell.x + dx, cell.y + dy);
            }
        }

        /// <summary>Suelta el fogonazo encima de la celda que ha reventado.</summary>
        private void PlayBlast(Vector2Int center)
        {
            if (blastFrames == null || blastFrames.Length == 0)
                return;

            float size = (blastRadius * 2f + 1f) * board.CellSize;

            BombBlast.Play(
                blastFrames,
                board.GridToWorld(center),
                size,
                blastFrameTime,
                blastSortingOrder,
                board.BlocksRoot);
        }
    }
}
