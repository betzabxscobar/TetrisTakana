using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// Marca una ficha del tablero como bomba y lleva su carga. Cada
    /// combinacion que el jugador encaje pegada a ella le suma una, y al
    /// llegar al tope revienta llevandose las celdas de alrededor.
    ///
    /// La carga se ve en el propio dibujo: la ficha va cogiendo aura y
    /// terminando agrietada. Asi el jugador sabe cuanto le falta sin ningun
    /// contador en pantalla.
    ///
    /// Va como componente aparte y no dentro de <see cref="BoardBlock"/> para
    /// que el resto del juego siga tratandola como una ficha normal: se
    /// intercambia, cae con la gravedad y hasta se puede combinar por color.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoardBlock))]
    public sealed class BombBlock : MonoBehaviour
    {
        [Header("Poses de la carga")]
        [Tooltip("Sin cargas todavia.")]
        [SerializeField] private Sprite restingPose;
        [Tooltip("Una pose por carga, en orden. La ultima es la de reventar.")]
        [SerializeField] private Sprite[] chargePoses;

        [Header("Entrada")]
        [Tooltip("Fotogramas de la bomba creciendo al aparecer.")]
        [SerializeField] private Sprite[] spawnPoses;
        [Tooltip("Lo que dura cada fotograma de la entrada.")]
        [SerializeField, Min(0.01f)] private float spawnFrameTime = 0.05f;

        [Header("Aviso")]
        [Tooltip("Cuanto late cuando ya solo le falta una carga.")]
        [SerializeField, Range(0f, 0.5f)] private float primedPulse = 0.12f;
        [SerializeField, Min(0.1f)] private float primedPulseSpeed = 6f;

        private SpriteRenderer spriteRenderer;
        private BoardBlock block;
        private Vector3 restScale;

        private int spawnFrame = -1;
        private float spawnTimer;

        /// <summary>Cargas acumuladas.</summary>
        public int Charge { get; private set; }

        /// <summary>Cargas que aguanta antes de reventar.</summary>
        public int ChargesToExplode => Mathf.Max(1, chargePoses != null ? chargePoses.Length : 3);

        /// <summary>Ya no le caben mas cargas: la siguiente la revienta.</summary>
        public bool IsPrimed => Charge >= ChargesToExplode;

        /// <summary>La celda que ocupa ahora mismo.</summary>
        public Vector2Int GridPosition => block != null ? block.GridPosition : Vector2Int.zero;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            block = GetComponent<BoardBlock>();
            restScale = transform.localScale;

            // Arranca la entrada solo si hay fotogramas; si no, aparece puesta.
            if (spawnPoses != null && spawnPoses.Length > 0)
            {
                spawnFrame = 0;
                spawnTimer = 0f;
            }

            ApplyPose();
        }

        private void Update()
        {
            float delta = Time.deltaTime;

            AdvanceSpawn(delta);
            AdvancePulse(delta);
        }

        /// <summary>
        /// Le pasa las poses desde fuera. Hace falta porque la bomba no se pone
        /// en el editor: <see cref="BombSystem"/> la añade a una ficha ya creada
        /// con AddComponent, y asi sus campos llegarian vacios.
        /// </summary>
        public void Configure(
            Sprite resting,
            Sprite[] charges,
            Sprite[] spawn,
            float frameTime)
        {
            restingPose = resting;
            chargePoses = charges;
            spawnPoses = spawn;
            spawnFrameTime = Mathf.Max(0.01f, frameTime);

            // AddComponent ya ha corrido el Awake, asi que hay que rearrancar la
            // entrada aqui con las poses ya puestas.
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            block ??= GetComponent<BoardBlock>();

            if (spawnPoses != null && spawnPoses.Length > 0)
            {
                spawnFrame = 0;
                spawnTimer = 0f;
            }

            ApplyPose();
        }

        /// <summary>
        /// Le suma una carga. Devuelve si con esta ya toca reventar.
        ///
        /// Se llama una vez por combinacion, no una por ficha rota: si no, una
        /// sola jugada que le tocase tres celdas la volaba de golpe y el jugador
        /// nunca llegaba a ver la bomba cargarse.
        /// </summary>
        public bool AddCharge()
        {
            if (IsPrimed)
                return true;

            Charge++;
            ApplyPose();
            return IsPrimed;
        }

        /// <summary>Pone la pose que toca segun las cargas que lleve.</summary>
        private void ApplyPose()
        {
            if (spriteRenderer == null)
                return;

            // Mientras dura la entrada manda la animacion de crecer.
            if (spawnFrame >= 0)
                return;

            if (Charge <= 0)
            {
                if (restingPose != null)
                    spriteRenderer.sprite = restingPose;

                return;
            }

            if (chargePoses == null || chargePoses.Length == 0)
                return;

            int index = Mathf.Clamp(Charge - 1, 0, chargePoses.Length - 1);

            if (chargePoses[index] != null)
                spriteRenderer.sprite = chargePoses[index];
        }

        /// <summary>Va pasando los fotogramas de la entrada y luego se quita.</summary>
        private void AdvanceSpawn(float delta)
        {
            if (spawnFrame < 0)
                return;

            spawnTimer += delta;

            if (spawnTimer < spawnFrameTime)
                return;

            spawnTimer = 0f;

            if (spriteRenderer != null && spawnPoses[spawnFrame] != null)
                spriteRenderer.sprite = spawnPoses[spawnFrame];

            spawnFrame++;

            if (spawnFrame < spawnPoses.Length)
                return;

            spawnFrame = -1;
            ApplyPose();
        }

        /// <summary>
        /// Late cuando ya esta a punto. Es el unico aviso de que la proxima
        /// combinacion de al lado la revienta, y se nota sin mirar el dibujo.
        /// </summary>
        private void AdvancePulse(float delta)
        {
            if (!IsPrimed || primedPulse <= 0f)
            {
                transform.localScale = restScale;
                return;
            }

            float beat = 1f + Mathf.Abs(Mathf.Sin(Time.time * primedPulseSpeed)) * primedPulse;
            transform.localScale = restScale * beat;
        }
    }
}
