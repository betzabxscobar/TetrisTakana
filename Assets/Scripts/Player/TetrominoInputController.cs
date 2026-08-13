using UnityEngine;
using UnityEngine.InputSystem;

namespace TetrisTakana
{
    /// <summary>
    /// Controles clásicos: flechas o mando para mover, arriba/X o B para rotar,
    /// Z/Q/Ctrl o X para rotar al revés, espacio/A para soltar de golpe y escape o
    /// Start para pausar.
    /// El repetido lateral imita el DAS del original.
    /// </summary>
    public class TetrominoInputController : MonoBehaviour
    {
        [SerializeField] private TetrisGame game;

        [Header("Repetición lateral (DAS)")]
        [Tooltip("Espera antes de que el movimiento lateral empiece a repetirse.")]
        [SerializeField, Min(0f)] private float initialRepeatDelay = 0.27f;
        [SerializeField, Min(0.01f)] private float repeatInterval = 0.1f;

        [Header("Bajada suave")]
        [SerializeField, Min(0.01f)] private float softDropInterval = 0.05f;

        private int heldDirection;
        private float nextRepeatTime;
        private float nextSoftDropTime;

        /// <summary>Busca la partida a la que mandar las ordenes.</summary>
        private void Awake()
        {
            if (game == null)
                game = FindAnyObjectByType<TetrisGame>();
        }

        /// <summary>Lee el teclado cada fotograma y reparte cada tecla a lo suyo.</summary>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            if ((keyboard == null && gamepad == null) || game == null)
                return;

            if (game.State == TetrisGame.GameState.GameOver)
            {
                if ((keyboard != null &&
                     (keyboard.enterKey.wasPressedThisFrame ||
                      keyboard.numpadEnterKey.wasPressedThisFrame)) ||
                    (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
                    game.StartGame();

                heldDirection = 0;
                return;
            }

            if ((keyboard != null &&
                 (keyboard.escapeKey.wasPressedThisFrame ||
                  keyboard.pKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.startButton.wasPressedThisFrame))
                game.TogglePause();

            if (!game.AcceptsInput)
            {
                heldDirection = 0;
                return;
            }

            HandleRotation(keyboard, gamepad);
            HandleHardDrop(keyboard, gamepad);
            HandleHorizontal(keyboard, gamepad);
            HandleSoftDrop(keyboard, gamepad);
        }

        /// <summary>Gira la pieza con la flecha arriba, W o X.</summary>
        private void HandleRotation(Keyboard keyboard, Gamepad gamepad)
        {
            if ((keyboard != null &&
                 (keyboard.upArrowKey.wasPressedThisFrame ||
                  keyboard.wKey.wasPressedThisFrame ||
                  keyboard.xKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
                game.Rotate(true);

            if ((keyboard != null &&
                 (keyboard.zKey.wasPressedThisFrame ||
                  keyboard.qKey.wasPressedThisFrame ||
                  keyboard.leftCtrlKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonWest.wasPressedThisFrame))
                game.Rotate(false);
        }

        /// <summary>Tira la pieza en picado con la barra espaciadora.</summary>
        private void HandleHardDrop(Keyboard keyboard, Gamepad gamepad)
        {
            if ((keyboard != null && keyboard.spaceKey.wasPressedThisFrame) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
                game.HardDrop();
        }

        /// <summary>Mueve la pieza de lado, con repeticion al mantener la tecla.</summary>
        private void HandleHorizontal(Keyboard keyboard, Gamepad gamepad)
        {
            int direction = ReadHorizontal(keyboard, gamepad);

            if (direction == 0)
            {
                heldDirection = 0;
                return;
            }

            // Al cambiar de dirección se mueve ya y se rearma la espera.
            if (direction != heldDirection)
            {
                heldDirection = direction;
                nextRepeatTime = Time.unscaledTime + initialRepeatDelay;
                game.MoveHorizontal(direction);
                return;
            }

            if (Time.unscaledTime < nextRepeatTime)
                return;

            nextRepeatTime = Time.unscaledTime + repeatInterval;
            game.MoveHorizontal(direction);
        }

        /// <summary>Baja la pieza mas rapido mientras se mantenga la tecla.</summary>
        private void HandleSoftDrop(Keyboard keyboard, Gamepad gamepad)
        {
            bool pressed = keyboard != null &&
                           (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed);

            if (gamepad != null)
                pressed |= gamepad.dpad.down.isPressed || gamepad.leftStick.ReadValue().y < -0.5f;

            if (!pressed)
            {
                nextSoftDropTime = 0f;
                return;
            }

            if (Time.unscaledTime < nextSoftDropTime)
                return;

            nextSoftDropTime = Time.unscaledTime + softDropInterval;
            game.SoftDrop();
        }

        /// <summary>Lee si el jugador pide izquierda, derecha o nada.</summary>
        private static int ReadHorizontal(Keyboard keyboard, Gamepad gamepad)
        {
            bool left = keyboard != null &&
                        (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed);
            bool right = keyboard != null &&
                         (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed);

            if (gamepad != null)
            {
                left |= gamepad.dpad.left.isPressed;
                right |= gamepad.dpad.right.isPressed;

                Vector2 stick = gamepad.leftStick.ReadValue();

                if (Mathf.Abs(stick.x) > 0.5f && Mathf.Abs(stick.x) >= Mathf.Abs(stick.y))
                {
                    left = stick.x < 0f;
                    right = stick.x > 0f;
                }
            }

            if (left == right)
                return 0;

            return left ? -1 : 1;
        }
    }
}
