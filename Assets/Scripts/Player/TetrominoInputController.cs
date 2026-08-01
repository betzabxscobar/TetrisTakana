using UnityEngine;
using UnityEngine.InputSystem;

namespace TetrisTakana
{
    [RequireComponent(typeof(PieceSpawner))]
    public class TetrominoInputController : MonoBehaviour
    {
        [SerializeField] private PieceSpawner pieceSpawner;

        [Header("Repetición de movimiento")]
        [SerializeField, Min(0f)] private float initialRepeatDelay = 0.2f;
        [SerializeField, Min(0.01f)] private float repeatInterval = 0.06f;

        private Vector2Int heldDirection;
        private float nextRepeatTime;

        private void Awake()
        {
            if (pieceSpawner == null)
                pieceSpawner = GetComponent<PieceSpawner>();
        }

        public void Initialize(PieceSpawner targetSpawner)
        {
            pieceSpawner = targetSpawner;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || pieceSpawner == null)
                return;

            HandleRotation(keyboard);
            HandleHardDrop(keyboard);
            HandleMovement(keyboard);
        }

        private void HandleRotation(Keyboard keyboard)
        {
            Tetromino piece = pieceSpawner.CurrentPiece;

            if (piece == null)
                return;

            if (keyboard.upArrowKey.wasPressedThisFrame ||
                keyboard.wKey.wasPressedThisFrame ||
                keyboard.xKey.wasPressedThisFrame)
                piece.TryRotate(true);

            if (keyboard.qKey.wasPressedThisFrame ||
                keyboard.zKey.wasPressedThisFrame)
                piece.TryRotate(false);
        }

        private void HandleHardDrop(Keyboard keyboard)
        {
            if (!keyboard.spaceKey.wasPressedThisFrame)
                return;

            Tetromino piece = pieceSpawner.CurrentPiece;

            if (piece == null)
                return;

            while (piece.TryMove(Vector2Int.down))
            {
            }

            pieceSpawner.LockCurrentPiece();
        }

        private void HandleMovement(Keyboard keyboard)
        {
            Vector2Int direction = ReadHeldDirection(keyboard);

            if (direction == Vector2Int.zero)
            {
                heldDirection = Vector2Int.zero;
                return;
            }

            bool directionChanged = direction != heldDirection;
            bool pressedThisFrame = WasDirectionPressed(keyboard, direction);

            if (directionChanged || pressedThisFrame)
            {
                heldDirection = direction;
                nextRepeatTime = Time.unscaledTime + initialRepeatDelay;
                TryMoveCurrentPiece(direction);
                return;
            }

            if (Time.unscaledTime < nextRepeatTime)
                return;

            nextRepeatTime = Time.unscaledTime + repeatInterval;
            TryMoveCurrentPiece(direction);
        }

        private void TryMoveCurrentPiece(Vector2Int direction)
        {
            Tetromino piece = pieceSpawner.CurrentPiece;

            if (piece == null)
                return;

            bool moved = piece.TryMove(direction);

            if (!moved && direction == Vector2Int.down)
                pieceSpawner.LockCurrentPiece();
        }

        private static Vector2Int ReadHeldDirection(Keyboard keyboard)
        {
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
                return Vector2Int.down;

            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
                return Vector2Int.left;

            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
                return Vector2Int.right;

            return Vector2Int.zero;
        }

        private static bool WasDirectionPressed(
            Keyboard keyboard,
            Vector2Int direction
        )
        {
            if (direction == Vector2Int.down)
                return keyboard.downArrowKey.wasPressedThisFrame ||
                       keyboard.sKey.wasPressedThisFrame;

            if (direction == Vector2Int.left)
                return keyboard.leftArrowKey.wasPressedThisFrame ||
                       keyboard.aKey.wasPressedThisFrame;

            return keyboard.rightArrowKey.wasPressedThisFrame ||
                   keyboard.dKey.wasPressedThisFrame;
        }
    }
}
