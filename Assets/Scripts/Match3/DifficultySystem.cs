using System;
using UnityEngine;

namespace TetrisTakana.Match3
{
    public class DifficultySystem : MonoBehaviour
    {
        [SerializeField, Min(1)] private int startingBlockTypes = 5;
        [SerializeField, Min(1)] private int maximumBlockTypes = 7;
        [SerializeField, Min(1)] private int clearsPerLevel = 5;
        [SerializeField, Min(0.1f)] private float startingFallSpeed = 1f;
        [SerializeField, Min(0.1f)] private float speedIncreasePerLevel = 0.1f;

        public int Level { get; private set; } = 1;
        public int BlockTypeCount => Mathf.Clamp(
            startingBlockTypes + Level - 1,
            1,
            maximumBlockTypes);
        public float FallSpeed => startingFallSpeed + (Level - 1) * speedIncreasePerLevel;
        public event Action<int> DifficultyChanged;

        private int clearedBlocks;

        public void NotifyClear(int amount)
        {
            clearedBlocks += Mathf.Max(0, amount);
            int nextLevel = clearedBlocks / Mathf.Max(1, clearsPerLevel) + 1;

            if (nextLevel == Level)
                return;

            Level = nextLevel;
            DifficultyChanged?.Invoke(Level);
        }
    }
}
