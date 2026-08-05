using System;
using System.Collections;
using UnityEngine;

namespace TetrisTakana.Match3
{
    public class Gravity : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField, Min(0f)] private float cellMoveDuration = 0.06f;

        public event Action GravityCompleted;

        private void Awake()
        {
            if (board == null)
                board = GetComponent<Board>();
        }

        public IEnumerator ApplyGravity()
        {
            if (board == null)
                yield break;

            for (int x = 0; x < board.Width; x++)
            {
                int destinationY = 0;

                for (int y = 0; y < board.Height; y++)
                {
                    Vector2Int source = new Vector2Int(x, y);
                    BoardBlock block = board.GetBlock(source);

                    if (block == null)
                        continue;

                    Vector2Int destination = new Vector2Int(x, destinationY++);

                    if (destination != source)
                    {
                        board.SetBlock(source, null);
                        board.SetBlock(destination, block);
                        yield return MoveBlock(block, destination);
                    }
                }
            }

            GravityCompleted?.Invoke();
        }

        private IEnumerator MoveBlock(BoardBlock block, Vector2Int destination)
        {
            if (block == null || cellMoveDuration <= 0f)
                yield break;

            Vector3 start = block.transform.position;
            Vector3 end = board.GridToWorld(destination);
            float elapsed = 0f;
            block.SetMoving(true);

            while (elapsed < cellMoveDuration)
            {
                elapsed += Time.deltaTime;
                block.transform.position = Vector3.Lerp(
                    start,
                    end,
                    Mathf.Clamp01(elapsed / cellMoveDuration));
                yield return null;
            }

            block.transform.position = end;
            block.SetMoving(false);
        }
    }
}
