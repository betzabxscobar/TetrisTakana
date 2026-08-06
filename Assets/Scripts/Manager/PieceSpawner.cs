using System;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Genera las piezas y guarda cuál viene después. Usa el sorteo del Tetris
    /// de Nintendo: elige al azar y, si repite la anterior, tira una segunda
    /// vez. Reduce las rachas sin llegar a ser una bolsa de siete.
    /// </summary>
    public class PieceSpawner : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private Tetromino[] piecePrefabs;

        public Tetromino CurrentPiece { get; private set; }

        /// <summary>Prefab de la pieza que saldrá en el siguiente turno.</summary>
        public Tetromino NextPrefab { get; private set; }

        public event Action<Tetromino> PieceSpawned;
        public event Action<Tetromino> NextPrefabChanged;
        public event Action SpawnFailed;

        private int lastIndex = -1;

        /// <summary>Elige ya la primera pieza para que el aviso de siguiente tenga algo que enseñar.</summary>
        private void Awake()
        {
            PickNextPrefab();
        }

        public bool HasPieces => piecePrefabs != null && piecePrefabs.Length > 0;

        /// <summary>
        /// Instancia la pieza pendiente y sortea la siguiente. Devuelve falso
        /// si no cabe en el tablero, que es la condición de fin de partida.
        /// </summary>
        public bool SpawnNext()
        {
            if (board == null || !HasPieces)
                return false;

            Tetromino prefab = NextPrefab;
            PickNextPrefab();

            if (prefab == null)
                return false;

            Tetromino piece = Instantiate(prefab);
            piece.name = prefab.name;

            if (!piece.Initialize(board, GetSpawnPosition(piece)))
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

        /// <summary>Tira la pieza en juego sin llegar a fijarla.</summary>
        public void ClearCurrentPiece()
        {
            if (CurrentPiece == null)
                return;

            CurrentPiece.Locked -= HandlePieceLocked;
            Destroy(CurrentPiece.gameObject);
            CurrentPiece = null;
        }

        /// <summary>Reinicia el sorteo; se llama al empezar una partida nueva.</summary>
        public void ResetSpawner()
        {
            ClearCurrentPiece();
            lastIndex = -1;
            PickNextPrefab();
        }

        /// <summary>Sortea cual sera la proxima pieza.</summary>
        private void PickNextPrefab()
        {
            if (!HasPieces)
                return;

            int index = UnityEngine.Random.Range(0, piecePrefabs.Length);

            // Una sola segunda tirada si repite: es como sortea el original.
            if (index == lastIndex)
                index = UnityEngine.Random.Range(0, piecePrefabs.Length);

            lastIndex = index;
            NextPrefab = piecePrefabs[index];
            NextPrefabChanged?.Invoke(NextPrefab);
        }

        /// <summary>Calcula en que celda aparece la pieza, centrada arriba del tablero.</summary>
        private Vector2Int GetSpawnPosition(Tetromino piece)
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

        /// <summary>La pieza se fijo: deja hueco para la siguiente.</summary>
        private void HandlePieceLocked(Tetromino piece)
        {
            piece.Locked -= HandlePieceLocked;

            if (CurrentPiece == piece)
                CurrentPiece = null;
        }
    }
}
