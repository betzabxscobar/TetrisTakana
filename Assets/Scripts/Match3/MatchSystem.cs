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

        private IEnumerator ResolveCascades(HashSet<Vector2Int> matches)
        {
            while (matches.Count > 0)
            {
                int combo = comboSystem != null ? comboSystem.RegisterCascade() : 1;

                foreach (Vector2Int position in matches)
                    board.RemoveBlock(position);

                scoreManager?.AddMatch(matches.Count, combo);
                difficulty?.NotifyClear(matches.Count);
                MatchResolved?.Invoke(matches.Count);
                yield return null;

                if (gravity != null)
                    yield return gravity.ApplyGravity();
                if (spawner != null)
                    yield return spawner.FillEmpty();

                yield return null;
                matches = FindMatches();
            }
        }

        public HashSet<Vector2Int> FindMatches()
        {
            HashSet<Vector2Int> matches = new HashSet<Vector2Int>();

            for (int y = 0; y < board.Height; y++)
            {
                int x = 0;
                while (x < board.Width)
                {
                    int start = x;
                    BoardBlock first = board.GetBlock(new Vector2Int(x, y));
                    int type = first != null ? first.BlockType : -1;

                    while (x < board.Width &&
                           board.GetBlock(new Vector2Int(x, y)) != null &&
                           board.GetBlock(new Vector2Int(x, y)).BlockType == type)
                        x++;

                    if (type >= 0 && x - start >= 3)
                        for (int matchX = start; matchX < x; matchX++)
                            matches.Add(new Vector2Int(matchX, y));
                }
            }

            for (int x = 0; x < board.Width; x++)
            {
                int y = 0;
                while (y < board.Height)
                {
                    int start = y;
                    BoardBlock first = board.GetBlock(new Vector2Int(x, y));
                    int type = first != null ? first.BlockType : -1;

                    while (y < board.Height &&
                           board.GetBlock(new Vector2Int(x, y)) != null &&
                           board.GetBlock(new Vector2Int(x, y)).BlockType == type)
                        y++;

                    if (type >= 0 && y - start >= 3)
                        for (int matchY = start; matchY < y; matchY++)
                            matches.Add(new Vector2Int(x, matchY));
                }
            }

            return matches;
        }
    }
}
