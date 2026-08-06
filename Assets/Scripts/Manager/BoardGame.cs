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
        /// <summary>Los estados por los que pasa una partida.</summary>
        public enum GameState
        {
            Ready,
            Playing,
            Paused,
            GameOver
        }

        private bool busy;
        private bool held;
        private float matchStartTime;

        public GameState State { get; private set; } = GameState.Ready;

        /// <summary>De que modo son las partidas de este bucle.</summary>
        public abstract GameMode Mode { get; }

        /// <summary>
        /// Lo que lleva jugada la partida. Va con Time.time y no con el reloj
        /// sin escalar a proposito: la pausa congela el tiempo del juego, y el
        /// rato que la tarjeta esta en pantalla no es rato jugado. El ranking
        /// compara la puntuacion contra esta duracion, asi que contar las
        /// pausas seria regalar margen a quien envie un resultado inventado.
        /// </summary>
        public float MatchDurationSeconds => Mathf.Max(0f, Time.time - matchStartTime);

        /// <summary>Los controles solo responden cuando esto es cierto.</summary>
        public bool AcceptsInput => State == GameState.Playing && !IsBusy;

        /// <summary>El tablero se esta resolviendo y no admite jugadas.</summary>
        public bool IsBusy => busy || held;

        public event Action<GameState> StateChanged;
        public event Action GameEnded;

        /// <summary>Empieza una partida desde cero.</summary>
        public abstract void StartGame();

        /// <summary>
        /// Pone el cronometro a cero. Lo llama cada modo al empezar partida, y
        /// no SetState, porque volver a empezar estando ya en Playing no cambia
        /// de estado y el cronometro se quedaria contando desde la anterior.
        /// </summary>
        protected void ResetMatchClock()
        {
            matchStartTime = Time.time;
        }

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

        /// <summary>Pausa o reanuda, salvo que el tablero este a medio resolver.</summary>
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

        /// <summary>Cambia de estado y avisa a quien escuche.</summary>
        protected void SetState(GameState next)
        {
            if (State == next)
                return;

            State = next;
            StateChanged?.Invoke(State);
        }

        /// <summary>Avisa de que la partida ha terminado.</summary>
        protected void RaiseGameEnded()
        {
            GameEnded?.Invoke();
        }
    }
}
