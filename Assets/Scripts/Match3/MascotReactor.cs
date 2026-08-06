using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// La mascota que acompaña la partida: cambia de pose y pega un brinco
    /// cuando el jugador rompe fichas, se encoge cuando la pila empuja una
    /// fila y se queda aturdida al perder.
    ///
    /// La hoja de poses no son fotogramas de una animacion, son dibujos
    /// sueltos, asi que el movimiento no sale de la imagen: se pone aqui con
    /// un salto y un aplastado calculados. Con eso una pose fija se lee como
    /// un personaje vivo, y de paso no hace falta ni Animator ni clips.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MascotReactor : MonoBehaviour
    {
        [Header("Sistemas")]
        [Tooltip("De donde salen las combinaciones. Vacio: se busca en la escena.")]
        [SerializeField] private MatchSystem matchSystem;
        [SerializeField] private ComboSystem comboSystem;
        [SerializeField] private RisingStack risingStack;
        [SerializeField] private BoardGame game;

        [Header("Poses")]
        [Tooltip("Como esta cuando no pasa nada.")]
        [SerializeField] private Sprite idlePose;
        [Tooltip("Al romper fichas.")]
        [SerializeField] private Sprite celebratePose;
        [Tooltip("Con una combinacion grande o un combo largo.")]
        [SerializeField] private Sprite cheerPose;
        [Tooltip("Al perder la partida.")]
        [SerializeField] private Sprite hurtPose;

        [Header("Reaccion")]
        [Tooltip("Fichas rotas a partir de las cuales celebra a lo grande.")]
        [SerializeField, Min(3)] private int bigMatchSize = 5;
        [Tooltip("Combo a partir del cual celebra a lo grande.")]
        [SerializeField, Min(2)] private int bigCombo = 3;
        [Tooltip("Lo que aguanta una pose antes de volver al reposo.")]
        [SerializeField, Min(0.1f)] private float poseDuration = 0.7f;

        [Header("Movimiento")]
        [Tooltip("Altura del brinco, en unidades de mundo.")]
        [SerializeField, Min(0f)] private float hopHeight = 0.35f;
        [Tooltip("Lo que dura el brinco.")]
        [SerializeField, Min(0.05f)] private float hopDuration = 0.35f;
        [Tooltip("Cuanto se estira y se aplasta al saltar, en tanto por uno.")]
        [SerializeField, Range(0f, 0.5f)] private float squash = 0.18f;
        [Tooltip("Cuanto tiembla al recibir una fila nueva.")]
        [SerializeField, Min(0f)] private float shakeStrength = 0.12f;
        [Tooltip("Respiracion en reposo: subida lenta para que no parezca un cromo.")]
        [SerializeField, Min(0f)] private float breathAmount = 0.03f;
        [SerializeField, Min(0.1f)] private float breathSpeed = 2f;

        private SpriteRenderer spriteRenderer;
        private Vector3 restPosition;
        private Vector3 restScale;

        private float poseTimer;
        private float hopTimer = -1f;
        private float hopScale = 1f;
        private float shakeTimer = -1f;

        /// <summary>Coge lo que le falte y se guarda su sitio de reposo.</summary>
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            matchSystem ??= FindAnyObjectByType<MatchSystem>();
            comboSystem ??= FindAnyObjectByType<ComboSystem>();
            risingStack ??= FindAnyObjectByType<RisingStack>();
            game ??= FindAnyObjectByType<BoardGame>();

            // El sitio de reposo se guarda una vez: todo el movimiento es un
            // desvio sobre el, asi la mascota nunca se va quedando torcida.
            restPosition = transform.localPosition;
            restScale = transform.localScale;

            if (idlePose == null)
                idlePose = spriteRenderer.sprite;
        }

        /// <summary>Se pone a escuchar lo que pasa en el tablero.</summary>
        private void OnEnable()
        {
            if (matchSystem != null)
                matchSystem.MatchResolved += HandleMatchResolved;

            if (comboSystem != null)
                comboSystem.ComboChanged += HandleComboChanged;

            if (risingStack != null)
            {
                risingStack.RowPushed += HandleRowPushed;
                risingStack.ToppedOut += HandleToppedOut;
            }

            if (game != null)
                game.GameEnded += HandleToppedOut;

            ShowPose(idlePose);
        }

        /// <summary>Se da de baja de todo lo que escuchaba.</summary>
        private void OnDisable()
        {
            if (matchSystem != null)
                matchSystem.MatchResolved -= HandleMatchResolved;

            if (comboSystem != null)
                comboSystem.ComboChanged -= HandleComboChanged;

            if (risingStack != null)
            {
                risingStack.RowPushed -= HandleRowPushed;
                risingStack.ToppedOut -= HandleToppedOut;
            }

            if (game != null)
                game.GameEnded -= HandleToppedOut;
        }

        /// <summary>Lleva el brinco, el temblor y la vuelta al reposo.</summary>
        private void Update()
        {
            // Con Time.deltaTime, no con el sin escalar: en pausa la mascota
            // tiene que quedarse tan quieta como el tablero.
            float delta = Time.deltaTime;

            AdvancePose(delta);
            AdvanceHop(delta);
            AdvanceShake(delta);
            ApplyTransform();
        }

        // --- Lo que pasa en el tablero --------------------------------------

        /// <summary>
        /// Se han roto fichas. Cuantas mas caen de una vez, mas se celebra:
        /// un tres en raya cualquiera no puede leerse igual que una cascada.
        /// </summary>
        private void HandleMatchResolved(int blockCount)
        {
            bool big = blockCount >= bigMatchSize;

            ShowPose(big && cheerPose != null ? cheerPose : celebratePose);
            Hop(big ? 1.4f : 1f);
        }

        /// <summary>La racha ha subido lo bastante como para festejarlo.</summary>
        private void HandleComboChanged(int combo)
        {
            if (combo < bigCombo)
                return;

            ShowPose(cheerPose != null ? cheerPose : celebratePose);
            Hop(1.6f);
        }

        /// <summary>Ha entrado una fila nueva: un temblor y a seguir.</summary>
        private void HandleRowPushed()
        {
            shakeTimer = 0f;
        }

        /// <summary>Se acabo la partida.</summary>
        private void HandleToppedOut()
        {
            if (hurtPose == null)
                return;

            ShowPose(hurtPose);

            // Sin temporizador: la pose de perder se queda hasta que empiece
            // otra partida, que es cuando OnEnable vuelve a poner el reposo.
            poseTimer = -1f;
        }

        // --- Movimiento ------------------------------------------------------

        /// <summary>Pone una pose y arranca su cuenta atras.</summary>
        private void ShowPose(Sprite pose)
        {
            if (pose == null || spriteRenderer == null)
                return;

            spriteRenderer.sprite = pose;
            poseTimer = poseDuration;
        }

        /// <summary>Devuelve la pose de reposo cuando se acaba el tiempo.</summary>
        private void AdvancePose(float delta)
        {
            if (poseTimer < 0f)
                return;

            poseTimer -= delta;

            if (poseTimer > 0f)
                return;

            poseTimer = -1f;

            if (idlePose != null && spriteRenderer != null)
                spriteRenderer.sprite = idlePose;
        }

        /// <summary>Lanza un brinco, mas alto cuanto mayor sea la fuerza.</summary>
        private void Hop(float strength)
        {
            hopTimer = 0f;
            hopScale = Mathf.Max(0.1f, strength);
        }

        /// <summary>Sube y baja la mascota describiendo el salto.</summary>
        private void AdvanceHop(float delta)
        {
            if (hopTimer < 0f)
                return;

            hopTimer += delta;

            if (hopTimer >= hopDuration)
                hopTimer = -1f;
        }

        /// <summary>Va apagando el temblor.</summary>
        private void AdvanceShake(float delta)
        {
            if (shakeTimer < 0f)
                return;

            shakeTimer += delta;

            if (shakeTimer >= 0.3f)
                shakeTimer = -1f;
        }

        /// <summary>
        /// Junta todos los desvios y los aplica de una vez. Se hace en un solo
        /// sitio porque el brinco, el temblor y la respiracion mueven lo mismo,
        /// y aplicandolos por separado el ultimo pisaria a los demas.
        /// </summary>
        private void ApplyTransform()
        {
            float height = 0f;
            float stretch = 0f;

            if (hopTimer >= 0f)
            {
                float progress = Mathf.Clamp01(hopTimer / hopDuration);

                // Media vuelta de seno: sube, llega arriba y baja.
                float arc = Mathf.Sin(progress * Mathf.PI);
                height = arc * hopHeight * hopScale;

                // Estirado al despegar y aplastado al caer, que es lo que hace
                // que un salto se lea como un salto y no como un ascensor.
                stretch = squash * Mathf.Cos(progress * Mathf.PI) * hopScale;
            }
            else
            {
                // En reposo respira, para que no parezca una calcomania pegada.
                height = Mathf.Sin(Time.time * breathSpeed) * breathAmount;
            }

            float sideways = 0f;

            if (shakeTimer >= 0f)
            {
                // Se apaga solo: empieza fuerte y se va quedando en nada.
                float fade = 1f - Mathf.Clamp01(shakeTimer / 0.3f);
                sideways = Mathf.Sin(shakeTimer * 60f) * shakeStrength * fade;
            }

            transform.localPosition = restPosition + new Vector3(sideways, height, 0f);
            transform.localScale = new Vector3(
                restScale.x * (1f - stretch),
                restScale.y * (1f + stretch),
                restScale.z);
        }
    }
}
