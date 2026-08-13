using UnityEngine;
using UnityEngine.InputSystem;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// La mascota que acompaña la partida: cambia de pose y pega un brinco
    /// cuando el jugador rompe fichas, se encoge cuando la pila empuja una
    /// fila y se queda aturdida al perder.
    ///
    /// Ademas es la que reparte las bombas. Cuando el jugador lleva unos
    /// segundos sin tocar una tecla, Pixel se mete en el tablero, suelta una
    /// bomba donde mas fichas se lleve por delante y se vuelve a su sitio. Las
    /// bombas solo entran al tablero asi: mientras el jugador este jugando no
    /// se le toca la partida, y en cuanto vuelve a pulsar algo la mascota da
    /// media vuelta sin gastar nada.
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
        [Tooltip("El tablero al que se mete a soltar la bomba.")]
        [SerializeField] private Board board;
        [Tooltip("El arsenal. Sin esto la mascota no entra nunca al tablero.")]
        [SerializeField] private BombSystem bombSystem;

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

        [Header("Bombas")]
        [Tooltip("Segundos sin tocar una tecla antes de que Pixel entre al tablero.")]
        [SerializeField, Min(0.5f)] private float idleBeforeBomb = 4f;
        [Tooltip("Lo que tarda en cruzar hasta la celda, y en volverse.")]
        [SerializeField, Min(0.05f)] private float travelDuration = 0.45f;
        [Tooltip("Lo que se queda plantada en la celda antes de soltarla.")]
        [SerializeField, Min(0f)] private float aimDuration = 0.25f;
        [Tooltip("Descanso entre dos bombas seguidas si el jugador sigue quieto.")]
        [SerializeField, Min(0f)] private float bombCooldown = 2f;
        [Tooltip("Cuanto se arquea el salto con el que cruza hasta el tablero.")]
        [SerializeField, Min(0f)] private float travelArc = 1.2f;

        /// <summary>Por donde va la mascota en su viaje a soltar la bomba.</summary>
        private enum Errand
        {
            None,
            GoingIn,
            Aiming,
            ComingBack
        }

        private SpriteRenderer spriteRenderer;
        private Vector3 restPosition;
        private Vector3 restScale;

        private float poseTimer;
        private float hopTimer = -1f;
        private float hopScale = 1f;
        private float shakeTimer = -1f;

        private Errand errand = Errand.None;
        private Vector2Int errandCell;
        private Vector3 errandPosition;
        private float errandTimer;
        private float idleTimer;
        private float cooldownTimer;

        /// <summary>Coge lo que le falte y se guarda su sitio de reposo.</summary>
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            matchSystem ??= FindAnyObjectByType<MatchSystem>();
            comboSystem ??= FindAnyObjectByType<ComboSystem>();
            risingStack ??= FindAnyObjectByType<RisingStack>();
            game ??= FindAnyObjectByType<BoardGame>();
            board ??= FindAnyObjectByType<Board>();
            bombSystem ??= FindAnyObjectByType<BombSystem>();

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

            // Una partida nueva empieza con la mascota en su sitio, aunque la
            // anterior la dejara a medio camino del tablero.
            errand = Errand.None;
            errandTimer = 0f;
            idleTimer = 0f;
            cooldownTimer = 0f;
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
            AdvanceErrand(delta);
            ApplyTransform();
        }

        // --- El recado de la bomba -------------------------------------------

        /// <summary>
        /// Cuenta el rato que el jugador lleva quieto y, si se pasa, manda a la
        /// mascota al tablero. Una vez en marcha, lleva el viaje de ida, la
        /// puntería y la vuelta.
        /// </summary>
        private void AdvanceErrand(float delta)
        {
            bool interacting = IsPlayerInteracting();

            if (interacting)
                idleTimer = 0f;
            else
                idleTimer += delta;

            cooldownTimer = Mathf.Max(0f, cooldownTimer - delta);

            switch (errand)
            {
                case Errand.None:
                    if (!interacting)
                        TryStartErrand();
                    return;

                case Errand.GoingIn:
                    errandTimer += delta;

                    // El jugador ha vuelto: la bomba no llega a entrar. Es la
                    // regla de la mecanica, la ayuda es solo para quien no esta
                    // jugando.
                    if (interacting)
                    {
                        GoBack();
                        return;
                    }

                    if (errandTimer >= travelDuration)
                    {
                        errand = Errand.Aiming;
                        errandTimer = 0f;
                    }

                    return;

                case Errand.Aiming:
                    errandTimer += delta;

                    if (interacting)
                    {
                        GoBack();
                        return;
                    }

                    if (errandTimer < aimDuration)
                        return;

                    // Ya no se comprueba nada mas: si el tablero se ha movido
                    // mientras cruzaba, TryDetonate lo rechaza solo.
                    DropBomb();
                    return;

                case Errand.ComingBack:
                    errandTimer += delta;

                    if (errandTimer >= travelDuration)
                    {
                        errand = Errand.None;
                        errandTimer = 0f;
                    }

                    return;
            }
        }

        /// <summary>Manda a la mascota a por la celda que mas fichas se lleve.</summary>
        private void TryStartErrand()
        {
            if (bombSystem == null ||
                board == null ||
                cooldownTimer > 0f ||
                idleTimer < idleBeforeBomb ||
                !bombSystem.CanDetonate ||
                !bombSystem.TryFindTarget(out errandCell))
                return;

            errand = Errand.GoingIn;
            errandTimer = 0f;
            errandPosition = ToLocal(board.GridToWorld(errandCell));

            ShowPose(cheerPose != null ? cheerPose : celebratePose);
        }

        /// <summary>Suelta la bomba y emprende la vuelta.</summary>
        private void DropBomb()
        {
            if (bombSystem.TryDetonate(errandCell))
                Hop(1.5f);

            GoBack();
        }

        /// <summary>Da media vuelta y arranca el descanso.</summary>
        private void GoBack()
        {
            // Desde donde este ahora mismo, que si la vuelta empieza a mitad de
            // la ida la mascota daria un salto hasta el tablero para volver.
            errandPosition = CurrentErrandPosition();
            errand = Errand.ComingBack;
            errandTimer = 0f;
            idleTimer = 0f;
            cooldownTimer = bombCooldown;
        }

        /// <summary>
        /// Dice si el jugador esta a lo suyo. Cuenta cualquier tecla, tambien
        /// mantenida: recorrer el tablero con el cursor pulsado es jugar tanto
        /// como intercambiar. Fuera de la partida en marcha se da por ocupado,
        /// que en pausa o en el fin de juego la mascota no pinta nada dentro
        /// del tablero.
        /// </summary>
        private bool IsPlayerInteracting()
        {
            if (game != null && !game.AcceptsInput)
                return true;

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.isPressed;
        }

        /// <summary>Pasa un punto del mundo al espacio en el que se mueve la mascota.</summary>
        private Vector3 ToLocal(Vector3 worldPosition)
        {
            return transform.parent != null
                ? transform.parent.InverseTransformPoint(worldPosition)
                : worldPosition;
        }

        /// <summary>Donde cae la mascota ahora mismo dentro de su viaje.</summary>
        private Vector3 CurrentErrandPosition()
        {
            switch (errand)
            {
                case Errand.GoingIn:
                    return Vector3.Lerp(
                        restPosition,
                        errandPosition,
                        Mathf.Clamp01(errandTimer / travelDuration));

                case Errand.Aiming:
                    return errandPosition;

                case Errand.ComingBack:
                    return Vector3.Lerp(
                        errandPosition,
                        restPosition,
                        Mathf.Clamp01(errandTimer / travelDuration));

                default:
                    return restPosition;
            }
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
            else if (errand == Errand.None)
            {
                // En reposo respira, para que no parezca una calcomania pegada.
                height = Mathf.Sin(Time.time * breathSpeed) * breathAmount;
            }

            // Cruzar hasta el tablero es un salto largo, no un deslizamiento:
            // el arco es lo que hace que se lea que va y viene ella sola.
            if (errand == Errand.GoingIn || errand == Errand.ComingBack)
                height += Mathf.Sin(
                    Mathf.Clamp01(errandTimer / travelDuration) * Mathf.PI) * travelArc;

            float sideways = 0f;

            if (shakeTimer >= 0f)
            {
                // Se apaga solo: empieza fuerte y se va quedando en nada.
                float fade = 1f - Mathf.Clamp01(shakeTimer / 0.3f);
                sideways = Mathf.Sin(shakeTimer * 60f) * shakeStrength * fade;
            }

            transform.localPosition =
                CurrentErrandPosition() + new Vector3(sideways, height, 0f);
            transform.localScale = new Vector3(
                restScale.x * (1f - stretch),
                restScale.y * (1f + stretch),
                restScale.z);
        }
    }
}
