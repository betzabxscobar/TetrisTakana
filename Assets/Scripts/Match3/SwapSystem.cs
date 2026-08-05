using System.Collections;
using UnityEngine;

namespace TetrisTakana.Match3
{
    public class SwapSystem : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField, Min(0f)] private float animationDuration = 0.12f;

        private bool swapping;

        public bool CanSwap => !swapping;
        public float AnimationDuration => animationDuration;

        private void Awake()
        {
            if (board == null)
                board = GetComponent<Board>();
        }

        public bool TrySwap(Vector2Int first, Vector2Int second)
        {
            if (board == null || !CanSwap)
                return false;

            BoardBlock firstBlock = board.GetBlock(first);
            BoardBlock secondBlock = board.GetBlock(second);
            Vector3 firstStart = firstBlock != null ? firstBlock.transform.position : board.GridToWorld(first);
            Vector3 secondStart = secondBlock != null ? secondBlock.transform.position : board.GridToWorld(second);

            swapping = true;
            bool swapped = board.TrySwap(first, second);

            if (!swapped)
            {
                swapping = false;
                return false;
            }

            StartCoroutine(AnimateSwap(
                firstBlock,
                secondBlock,
                first,
                second,
                firstStart,
                secondStart));
            return true;
        }

        public void FinishSwap()
        {
            swapping = false;
        }

        private IEnumerator AnimateSwap(
            BoardBlock firstBlock,
            BoardBlock secondBlock,
            Vector2Int first,
            Vector2Int second,
            Vector3 firstStart,
            Vector3 secondStart)
        {
            Vector3 firstTarget = board.GridToWorld(second);
            Vector3 secondTarget = board.GridToWorld(first);

            if (firstBlock != null)
                firstBlock.transform.position = firstStart;
            if (secondBlock != null)
                secondBlock.transform.position = secondStart;

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = animationDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / animationDuration);

                if (firstBlock != null)
                    firstBlock.transform.position = Vector3.Lerp(firstStart, firstTarget, t);
                if (secondBlock != null)
                    secondBlock.transform.position = Vector3.Lerp(secondStart, secondTarget, t);

                yield return null;
            }

            if (firstBlock != null)
                firstBlock.transform.position = firstTarget;
            if (secondBlock != null)
                secondBlock.transform.position = secondTarget;

            swapping = false;
        }
    }
}
