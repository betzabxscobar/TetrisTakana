using System;
using UnityEngine;

namespace TetrisTakana
{
    public class PieceSpawner : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private Tetromino[] piecePrefabs;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool spawnAfterLock = true;

        public Tetromino CurrentPiece { get; private set; }

        public event Action<Tetromino> PieceSpawned;
        public event Action SpawnFailed;

        private void Start()
        {
            if (spawnOnStart)
                SpawnNext();
        }

        public bool SpawnNext()
        {
            if (board == null || piecePrefabs == null || piecePrefabs.Length == 0)
                return false;

            Tetromino prefab = piecePrefabs[UnityEngine.Random.Range(0, piecePrefabs.Length)];

            if (prefab == null)
                return false;

            Tetromino piece = Instantiate(prefab);
            Vector2Int spawnPosition = GetCenteredSpawnPosition(piece);

            if (!piece.Initialize(board, spawnPosition))
            {
                Destroy(piece.gameObject);
                SpawnFailed?.Invoke();
                return false;
            }

            CurrentPiece = piece;
            CurrentPiece.Locked += HandlePieceLocked;
            PieceSpawned?.Invoke(CurrentPiece);
            return true;
        }

        public bool LockCurrentPiece()
        {
            return CurrentPiece != null && CurrentPiece.TryLock();
        }

        private Vector2Int GetCenteredSpawnPosition(Tetromino piece)
        {
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int i = 0; i < piece.BlockCount; i++)
            {
                Vector2Int offset = piece.GetCellOffset(i);
                minX = Mathf.Min(minX, offset.x);
                maxX = Mathf.Max(maxX, offset.x);
                maxY = Mathf.Max(maxY, offset.y);
            }

            int pieceWidth = maxX - minX + 1;
            int anchorX = (board.Width - pieceWidth) / 2 - minX;
            int anchorY = board.Height - 1 - maxY;

            return new Vector2Int(anchorX, anchorY);
        }

        private void HandlePieceLocked(Tetromino piece)
        {
            piece.Locked -= HandlePieceLocked;

            if (CurrentPiece == piece)
                CurrentPiece = null;

            if (spawnAfterLock)
                SpawnNext();
        }
    }
}
