using System.Collections;
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
        private int idleStateHash;
        private int celebrateStateHash;
        private int defeatStateHash;
        private AnimationClip defeatClip;
        private Coroutine defeatRoutine;

        private void Awake()
        {
            FetchReferences();
            CacheAnimationStates();
            FindDefeatClip();

            // La mascota debe seguir animandose aunque otro sistema haya
            // congelado el tiempo del juego (por ejemplo, una pausa).
            if (animator != null)
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
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

            if (defeatRoutine != null)
            {
                StopCoroutine(defeatRoutine);
                defeatRoutine = null;
            }

            isDefeated = false;

            if (animator != null)
                animator.enabled = true;
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

        private void CacheAnimationStates()
        {
            idleStateHash = ResolveStateHash(idleStateName);
            celebrateStateHash = ResolveStateHash(celebrateStateName);
            defeatStateHash = ResolveStateHash(defeatStateName);
        }

        private void FindDefeatClip()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == defeatStateName)
                {
                    defeatClip = clip;
                    return;
                }
            }

            Debug.LogError(
                $"No se encontro el clip '{defeatStateName}' en el Animator de la mascota.",
                this);
        }

        private int ResolveStateHash(string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
                return 0;

            int shortHash = Animator.StringToHash(stateName);

            if (animator.HasState(0, shortHash))
                return shortHash;

            int fullPathHash = Animator.StringToHash($"Base Layer.{stateName}");

            if (animator.HasState(0, fullPathHash))
                return fullPathHash;

            Debug.LogError(
                $"La animacion '{stateName}' no existe en la capa base del Animator de la mascota.",
                this);
            return 0;
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
            if (animator == null || isDefeated || celebrateStateHash == 0)
                return;

            animator.enabled = true;
            animator.Play(celebrateStateHash, 0, 0f);
            animator.Update(0f);
        }

        public void PlayDefeat()
        {
            // EndGame comunica primero StateChanged y luego GameEnded. Sin
            // esta guarda la segunda notificacion reiniciaba el clip.
            if (animator == null || isDefeated)
                return;

            isDefeated = true;

            if (defeatClip != null)
            {
                defeatRoutine = StartCoroutine(PlayDefeatClip());
                return;
            }

            if (defeatStateHash == 0)
                return;

            animator.Play(defeatStateHash, 0, 0f);
            animator.Update(0f);
        }

        private IEnumerator PlayDefeatClip()
        {
            // SampleAnimation aplica directamente las curvas de sprites. Asi la
            // reaccion no depende de transiciones ni del estado interno del Animator.
            animator.enabled = false;
            float elapsed = 0f;

            while (isDefeated && elapsed < defeatClip.length)
            {
                defeatClip.SampleAnimation(gameObject, elapsed);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (isDefeated)
                defeatClip.SampleAnimation(gameObject, defeatClip.length);

            defeatRoutine = null;
        }

        public void PlayIdle()
        {
            if (animator == null || idleStateHash == 0)
                return;

            isDefeated = false;

            if (defeatRoutine != null)
            {
                StopCoroutine(defeatRoutine);
                defeatRoutine = null;
            }

            animator.enabled = true;
            animator.Play(idleStateHash, 0, 0f);
            animator.Update(0f);
        }
    }
}
