using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>Fabrica las fichas del tablero y reparte sus tipos evitando combinaciones regaladas.</summary>
    public class Spawner : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private BoardBlock blockPrefab;
        [SerializeField] private Sprite blockSprite;
        [Tooltip("Un sprite por tipo. Si esta relleno manda sobre blockColors.")]
        [SerializeField] private Sprite[] blockSprites;
        [SerializeField] private DifficultySystem difficulty;
        [SerializeField, Min(1)] private int blockTypeCount = 5;
        [SerializeField] private Color[] blockColors =
        {
            new Color(0.95f, 0.25f, 0.25f),
            new Color(0.25f, 0.55f, 1f),
            new Color(0.35f, 0.9f, 0.4f),
            new Color(1f, 0.8f, 0.2f),
            new Color(0.8f, 0.35f, 1f),
            new Color(1f, 0.45f, 0.15f),
            new Color(0.2f, 0.9f, 0.9f)
        };

        [Tooltip("Lo que tardan en caer todas las fichas del rellenado.")]
        [SerializeField, Min(0.01f)] private float fillDuration = 0.35f;

        [Tooltip("Parte de la celda que ocupa la ficha. Menos de 1 deja aire entre fichas.")]
        [SerializeField, Range(0.5f, 1f)] private float cellFill = 0.92f;

        private readonly List<FallingBlock> falling = new List<FallingBlock>();

        /// <summary>Una ficha cayendo al llenar: de donde sale y a donde va.</summary>
        private struct FallingBlock
        {
            public BoardBlock Block;
            public Vector3 From;
            public Vector3 To;
        }

        public event Action<BoardBlock> BlockSpawned;

        /// <summary>Busca tablero y dificultad en el propio objeto si faltan.</summary>
        private void Awake()
        {
            if (board == null)
                board = GetComponent<Board>();
            if (difficulty == null)
                difficulty = GetComponent<DifficultySystem>();
        }

        /// <summary>
        /// Rellena el tablero entero. Con la pila que sube esto solo vale para
        /// un modo sin cuenta atras: quien lo llame deja la rejilla llena hasta
        /// el techo, que es justo la condicion de derrota.
        /// </summary>
        public IEnumerator FillEmpty()
        {
            yield return FillUpTo(board != null ? board.Height : 0);
        }

        /// <summary>
        /// Rellena solo hasta la fila indicada. La partida arranca con el
        /// tablero medio lleno y el resto lo va empujando la pila.
        /// </summary>
        public IEnumerator FillUpTo(int rows)
        {
            if (board == null)
                yield break;

            int limit = Mathf.Clamp(rows, 0, board.Height);
            falling.Clear();

            // De abajo arriba: CreatesMatch mira los vecinos de la izquierda y
            // de abajo, y si se llenara desde arriba esos vecinos estarian
            // vacios y la prevencion de combinaciones no haria nada.
            for (int y = 0; y < limit; y++)
            for (int x = 0; x < board.Width; x++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (board.IsOccupied(position))
                    continue;

                BoardBlock block = CreateBlock(position);
                board.SetBlock(position, block);

                Vector3 target = board.GridToWorld(position);
                block.transform.position = target + Vector3.up * board.Height * board.CellSize;
                falling.Add(new FallingBlock
                {
                    Block = block,
                    From = block.transform.position,
                    To = target
                });
            }

            // Todos a la vez: encadenar una corrutina por bloque hacia 200
            // celdas eran unos 16 segundos de espera antes de poder jugar.
            yield return DropAll();
        }

        /// <summary>Deja caer de golpe todas las fichas del relleno.</summary>
        private IEnumerator DropAll()
        {
            if (falling.Count == 0)
                yield break;

            float elapsed = 0f;

            while (elapsed < fillDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fillDuration);
                float eased = progress * progress;

                foreach (FallingBlock fall in falling)
                    if (fall.Block != null)
                        fall.Block.transform.position =
                            Vector3.Lerp(fall.From, fall.To, eased);

                yield return null;
            }

            foreach (FallingBlock fall in falling)
                if (fall.Block != null)
                    fall.Block.transform.position = fall.To;

            falling.Clear();
        }

        /// <summary>
        /// Crea una ficha suelta para la fila que entra por abajo. Todavia no
        /// se registra en el tablero: de eso se encarga quien la empuja.
        /// </summary>
        public BoardBlock CreateLooseBlock(Vector2Int position)
        {
            return CreateBlock(position);
        }

        /// <summary>Crea una ficha, le pone tipo y sprite y la escala a la celda.</summary>
        private BoardBlock CreateBlock(Vector2Int position)
        {
            BoardBlock block;

            if (blockPrefab != null)
                block = Instantiate(blockPrefab, board.BlocksRoot);
            else
            {
                GameObject instance = new GameObject("MatchBlock");
                instance.transform.SetParent(board.BlocksRoot, false);
                block = instance.AddComponent<BoardBlock>();
                instance.AddComponent<SpriteRenderer>();
            }

            int type = ChooseType(position);
            block.SetBlockType(type);

            SpriteRenderer renderer = block.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                // Con el arte del pack cada tipo tiene su propio sprite y no
                // hay que teñir nada; el tinte solo es el respaldo.
                Sprite typeSprite = GetSprite(type);

                if (typeSprite != null)
                {
                    renderer.sprite = typeSprite;
                    renderer.color = Color.white;
                }
                else
                {
                    if (renderer.sprite == null)
                        renderer.sprite = blockSprite;

                    renderer.color = GetColor(type);
                }

                FitToCell(block, renderer);
            }

            BlockSpawned?.Invoke(block);
            return block;
        }

        /// <summary>Sortea un tipo intentando que no forme tres en raya de salida.</summary>
        private int ChooseType(Vector2Int position)
        {
            int types = difficulty != null
                ? difficulty.BlockTypeCount
                : Mathf.Clamp(blockTypeCount, 1, blockColors.Length);

            int type = UnityEngine.Random.Range(0, types);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                type = UnityEngine.Random.Range(0, types);
                if (!CreatesMatch(position, type))
                    return type;
            }

            return type;
        }

        /// <summary>Dice si poner ese tipo ahi cerraria tres iguales.</summary>
        private bool CreatesMatch(Vector2Int position, int type)
        {
            return SameType(position + Vector2Int.left, type) &&
                   SameType(position + Vector2Int.left * 2, type) ||
                   SameType(position + Vector2Int.down, type) &&
                   SameType(position + Vector2Int.down * 2, type);
        }

        /// <summary>Dice si la ficha de esa celda es del tipo indicado.</summary>
        private bool SameType(Vector2Int position, int type)
        {
            BoardBlock block = board.GetBlock(position);
            return block != null && block.BlockType == type;
        }

        /// <summary>Devuelve el sprite que le toca a un tipo de ficha.</summary>
        private Sprite GetSprite(int type)
        {
            if (blockSprites == null || blockSprites.Length == 0)
                return null;

            return blockSprites[Mathf.Clamp(type, 0, blockSprites.Length - 1)];
        }

        /// <summary>Devuelve el color de respaldo de un tipo de ficha.</summary>
        private Color GetColor(int type)
        {
            return blockColors[Mathf.Clamp(type, 0, blockColors.Length - 1)];
        }

        /// <summary>
        /// Escala la ficha para que ocupe exactamente una celda, sea cual sea
        /// el tamaño en pixeles del sprite que traiga el pack.
        /// </summary>
        private void FitToCell(BoardBlock block, SpriteRenderer renderer)
        {
            Sprite sprite = renderer.sprite;

            if (sprite == null)
                return;

            Vector2 size = sprite.bounds.size;

            if (size.x <= 0f || size.y <= 0f)
                return;

            // Algo menos de una celda: con las fichas pegadas borde con borde
            // los marcos de unas y otras se tocan y el tablero se lee como una
            // sola mancha. El aire de en medio es lo que las separa.
            float scale = board.CellSize * cellFill / Mathf.Max(size.x, size.y);
            block.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Lleva una ficha hasta un punto con una animacion corta.</summary>
        private IEnumerator MoveTo(BoardBlock block, Vector3 target)
        {
            if (block == null)
                yield break;

            Vector3 start = block.transform.position;
            float elapsed = 0f;
            const float duration = 0.08f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                block.transform.position = Vector3.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            block.transform.position = target;
        }
    }
}
