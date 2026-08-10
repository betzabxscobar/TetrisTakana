using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Controlador decorativo para la mascota o personaje estático en la escena.
    /// Escucha eventos de líneas completadas y fin de partida para hacer llamadas
    /// directas al Animator ('celebracion', 'derrota', 'idle') sin gestionar duraciones.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class MascotAnimationController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Animator animator;
        [SerializeField] private LineClearSystem lineClearSystem;
        [SerializeField] private Match3.MatchSystem matchSystem;
        [SerializeField] private BoardGame game;

        [Header("Animaciones")]
        [SerializeField] private string idleStateName = "idle";
        [SerializeField] private string celebrateStateName = "celebracion";
        [SerializeField] private string defeatStateName = "derrota";

        private bool isDefeated;

        private void Awake()
        {
            FetchReferences();
        }

        private void OnEnable()
        {
            FetchReferences();
            Subscribe();

            if (game != null && game.State == BoardGame.GameState.GameOver)
            {
                PlayDefeat();
            }
            else
            {
                PlayIdle();
            }
        }

        private void Start()
        {
            FetchReferences();
            Subscribe();

            if (game != null && game.State == BoardGame.GameState.GameOver)
            {
                PlayDefeat();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void FetchReferences()
        {
            animator ??= GetComponent<Animator>();
            lineClearSystem ??= FindAnyObjectByType<LineClearSystem>();
            matchSystem ??= FindAnyObjectByType<Match3.MatchSystem>();
            game ??= FindAnyObjectByType<TetrisGame>();
            game ??= FindAnyObjectByType<Match3.Match3Game>();
            game ??= FindAnyObjectByType<BoardGame>();
        }

        private void Subscribe()
        {
            Unsubscribe();

            if (lineClearSystem != null)
                lineClearSystem.LinesCleared += HandleLinesCleared;

            if (matchSystem != null)
                matchSystem.MatchResolved += HandleLinesCleared;

            if (game != null)
            {
                game.StateChanged += HandleStateChanged;
                game.GameEnded += HandleGameEnded;
            }
        }

        private void Unsubscribe()
        {
            if (lineClearSystem != null)
                lineClearSystem.LinesCleared -= HandleLinesCleared;

            if (matchSystem != null)
                matchSystem.MatchResolved -= HandleLinesCleared;

            if (game != null)
            {
                game.StateChanged -= HandleStateChanged;
                game.GameEnded -= HandleGameEnded;
            }
        }

        private void HandleLinesCleared(int count)
        {
            if (count <= 0 || isDefeated)
                return;

            PlayCelebration();
        }

        private void HandleGameEnded()
        {
            PlayDefeat();
        }

        private void HandleStateChanged(BoardGame.GameState state)
        {
            if (state == BoardGame.GameState.GameOver)
            {
                PlayDefeat();
            }
            else if (state == BoardGame.GameState.Playing)
            {
                isDefeated = false;
                PlayIdle();
            }
        }

        public void PlayCelebration()
        {
            if (animator == null || isDefeated)
                return;

            animator.Play(celebrateStateName, 0, 0f);
            animator.Update(0f);
        }

        public void PlayDefeat()
        {
            if (animator == null)
                return;

            isDefeated = true;
            animator.Play(defeatStateName, 0, 0f);
            animator.Update(0f);
        }

        public void PlayIdle()
        {
            if (animator == null)
                return;

            isDefeated = false;
            animator.Play(idleStateName, 0, 0f);
            animator.Update(0f);
        }
    }
}
