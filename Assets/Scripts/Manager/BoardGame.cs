using System;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Contrato comun de los dos modos de juego. El Tetris y el match-3 se
    /// juegan de forma muy distinta, pero la pausa, el fin de partida, el HUD
    /// y el reloj de arena solo necesitan saber en que estado esta la partida
    /// y si el tablero admite jugadas ahora mismo.
    ///
    /// Es una clase abstracta y no una interfaz a proposito: Unity serializa
    /// referencias a MonoBehaviour abstractos en el inspector, pero no sabe
    /// serializar interfaces.
    /// </summary>
    public abstract class BoardGame : MonoBehaviour
    {
        public enum GameState
        {
            Ready,
            Playing,
            Paused,
            GameOver
        }

        private bool busy;
        private bool held;

        public GameState State { get; private set; } = GameState.Ready;

        /// <summary>Los controles solo responden cuando esto es cierto.</summary>
        public bool AcceptsInput => State == GameState.Playing && !IsBusy;

        /// <summary>El tablero se esta resolviendo y no admite jugadas.</summary>
        public bool IsBusy => busy || held;

        public event Action<GameState> StateChanged;
        public event Action GameEnded;

        /// <summary>Empieza una partida desde cero.</summary>
        public abstract void StartGame();

        /// <summary>
        /// Lo usa el propio bucle del modo mientras resuelve una jugada.
        /// </summary>
        public virtual void SetBusy(bool value)
        {
            busy = value;
        }

        /// <summary>
        /// Congela el juego mientras otro sistema reorganiza el tablero, como
        /// hace el giro del reloj de arena. No pasa por el estado de pausa: la
        /// partida sigue en Playing pero ni el bucle ni los controles actuan.
        /// Se guarda aparte de <see cref="SetBusy"/> para que el modo pueda
        /// seguir refrescando su propio estado sin soltar la retencion.
        /// </summary>
        public virtual void SetHold(bool value)
        {
            held = value;
        }

        public virtual void TogglePause()
        {
            // Pausar a medio resolver dejaria el tablero incoherente.
            if (busy)
                return;

            if (State == GameState.Playing)
                SetState(GameState.Paused);
            else if (State == GameState.Paused)
                SetState(GameState.Playing);
        }

        protected void SetState(GameState next)
        {
            if (State == next)
                return;

            State = next;
            StateChanged?.Invoke(State);
        }

        protected void RaiseGameEnded()
        {
            GameEnded?.Invoke();
        }
    }
}
