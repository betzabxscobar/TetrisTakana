using System;
using System.Collections;
using UnityEngine;

namespace TetrisTakana.Match3
{
    public class Spawner : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private BoardBlock blockPrefab;
        [SerializeField] private Sprite blockSprite;
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

        public event Action<BoardBlock> BlockSpawned;

        private void Awake()
        {
            if (board == null)
                board = GetComponent<Board>();
            if (difficulty == null)
                difficulty = GetComponent<DifficultySystem>();
        }

        private IEnumerator Start()
        {
            yield return FillEmpty();
        }

        public IEnumerator FillEmpty()
        {
            if (board == null)
                yield break;

            for (int y = board.Height - 1; y >= 0; y--)
            for (int x = 0; x < board.Width; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                if (board.IsOccupied(position))
                    continue;

                BoardBlock block = CreateBlock(position);
                board.SetBlock(position, block);

                Vector3 target = board.GridToWorld(position);
                block.transform.position = target + Vector3.up * board.Height * board.CellSize;
                yield return MoveTo(block, target);
            }
        }

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
                if (renderer.sprite == null)
                    renderer.sprite = blockSprite;
                renderer.color = GetColor(type);
            }

            BlockSpawned?.Invoke(block);
            return block;
        }

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

        private bool CreatesMatch(Vector2Int position, int type)
        {
            return SameType(position + Vector2Int.left, type) &&
                   SameType(position + Vector2Int.left * 2, type) ||
                   SameType(position + Vector2Int.down, type) &&
                   SameType(position + Vector2Int.down * 2, type);
        }

        private bool SameType(Vector2Int position, int type)
        {
            BoardBlock block = board.GetBlock(position);
            return block != null && block.BlockType == type;
        }

        private Color GetColor(int type)
        {
            return blockColors[Mathf.Clamp(type, 0, blockColors.Length - 1)];
        }

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
