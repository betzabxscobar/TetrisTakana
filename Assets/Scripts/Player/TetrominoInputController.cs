using UnityEngine;
using UnityEngine.InputSystem;

namespace TetrisTakana
{
    /// <summary>
    /// Controles clásicos: flechas para mover, arriba o X para rotar, Z para
    /// rotar al revés, espacio para soltar de golpe y escape para pausar.
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

            if (keyboard == null || game == null)
                return;

            if (game.State == TetrisGame.GameState.GameOver)
            {
                if (keyboard.enterKey.wasPressedThisFrame ||
                    keyboard.numpadEnterKey.wasPressedThisFrame)
                    game.StartGame();

                heldDirection = 0;
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame ||
                keyboard.pKey.wasPressedThisFrame)
                game.TogglePause();

            if (!game.AcceptsInput)
            {
                heldDirection = 0;
                return;
            }

            HandleRotation(keyboard);
            HandleHardDrop(keyboard);
            HandleHorizontal(keyboard);
            HandleSoftDrop(keyboard);
        }

        /// <summary>Gira la pieza con la flecha arriba, W o X.</summary>
        private void HandleRotation(Keyboard keyboard)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame ||
                keyboard.wKey.wasPressedThisFrame ||
                keyboard.xKey.wasPressedThisFrame)
                game.Rotate(true);

            if (keyboard.zKey.wasPressedThisFrame ||
                keyboard.qKey.wasPressedThisFrame ||
                keyboard.leftCtrlKey.wasPressedThisFrame)
                game.Rotate(false);
        }

        /// <summary>Tira la pieza en picado con la barra espaciadora.</summary>
        private void HandleHardDrop(Keyboard keyboard)
        {
            if (keyboard.spaceKey.wasPressedThisFrame)
                game.HardDrop();
        }

        /// <summary>Mueve la pieza de lado, con repeticion al mantener la tecla.</summary>
        private void HandleHorizontal(Keyboard keyboard)
        {
            int direction = ReadHorizontal(keyboard);

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
        private void HandleSoftDrop(Keyboard keyboard)
        {
            bool pressed = keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;

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
        private static int ReadHorizontal(Keyboard keyboard)
        {
            bool left = keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed;
            bool right = keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed;

            if (left == right)
                return 0;

            return left ? -1 : 1;
        }
    }
}
