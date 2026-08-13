using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// La mascota que acompaña la partida. Tiene una vida propia por encima de
    /// reaccionar al tablero: entra mirando a los lados sin enterarse de nada,
    /// se queda observando, y si el jugador se atasca se acerca al tablero, le
    /// señala una jugada y se enfada si aun asi no le hacen caso. Cuando caen
    /// fichas lo celebra con un brinco.
    ///
    /// La hoja de poses no son fotogramas de una animacion, son dibujos
    /// sueltos, asi que el movimiento no sale de la imagen: se pone aqui con
    /// saltos, carreras y aplastados calculados. Con eso una pose fija se lee
    /// como un personaje vivo, y de paso no hace falta ni Animator ni clips.
    ///
    /// Todo pasa por una maquina de estados y no por temporizadores sueltos
    /// porque las conductas se pisan entre ellas: sin un estado unico, un combo
    /// a mitad de camino dejaba a la mascota celebrando mientras seguia
    /// andando, y el enfado se disparaba con la pista ya resuelta.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MascotReactor : MonoBehaviour
    {
        /// <summary>En que anda la mascota ahora mismo.</summary>
        private enum State
        {
            PowerUp,
            Confused,
            Watching,
            Approaching,
            Pointing,
            Annoyed,
            Returning,
            Celebrating,
            Hurt
        }

        [Header("Sistemas")]
        [Tooltip("De donde salen las combinaciones. Vacio: se busca en la escena.")]
        [SerializeField] private MatchSystem matchSystem;
        [SerializeField] private ComboSystem comboSystem;
        [SerializeField] private RisingStack risingStack;
        [SerializeField] private BoardGame game;
        [Tooltip("El tablero al que se acerca a señalar. Vacio: se busca en la escena.")]
        [SerializeField] private Board board;
        [Tooltip("Para sacudir la camara al soltar el aura. Vacio: no sacude.")]
        [SerializeField] private BoardJuice juice;

        [Header("Poses")]
        [Tooltip("Como esta cuando no pasa nada.")]
        [SerializeField] private Sprite idlePose;
        [Tooltip("Segundo fotograma del reposo. Se alterna con el anterior.")]
        [SerializeField] private Sprite idlePoseB;
        [Tooltip("Cada cuanto cambia de fotograma en reposo.")]
        [SerializeField, Min(0.05f)] private float idleFrameInterval = 0.8f;
        [Tooltip("Al romper fichas.")]
        [SerializeField] private Sprite celebratePose;
        [Tooltip("Con una combinacion grande o un combo largo.")]
        [SerializeField] private Sprite cheerPose;
        [Tooltip("Al perder la partida.")]
        [SerializeField] private Sprite hurtPose;
        [Tooltip("Mirando de lado, para la entrada confundida.")]
        [SerializeField] private Sprite confusedPose;
        [Tooltip("De espaldas, para el otro lado de la entrada confundida.")]
        [SerializeField] private Sprite lookBackPose;
        [Tooltip("Señalando la jugada.")]
        [SerializeField] private Sprite pointPose;
        [Tooltip("De brazos cruzados, cuando le ignoran la pista.")]
        [SerializeField] private Sprite angryPose;
        [Tooltip("Carrera, primer paso.")]
        [SerializeField] private Sprite runPoseA;
        [Tooltip("Carrera, segundo paso.")]
        [SerializeField] private Sprite runPoseB;
        [Tooltip("Marcar si los dibujos miran hacia la derecha.")]
        [SerializeField] private bool posesFaceRight = true;

        [Header("Reaccion")]
        [Tooltip("Fichas rotas a partir de las cuales celebra a lo grande.")]
        [SerializeField, Min(3)] private int bigMatchSize = 5;
        [Tooltip("Combo a partir del cual celebra a lo grande.")]
        [SerializeField, Min(2)] private int bigCombo = 3;
        [Tooltip("Lo que aguanta una celebracion antes de volver a lo suyo.")]
        [SerializeField, Min(0.1f)] private float poseDuration = 0.7f;

        [Header("Entrada con aura")]
        [Tooltip("Al empezar la partida se concentra y suelta un aura.")]
        [SerializeField] private bool powerUpAtStart = true;
        [Tooltip("Lo que dura la carga entera, con su estallido final.")]
        [SerializeField, Min(0.2f)] private float powerUpDuration = 2.2f;
        [Tooltip("Pose mientras se concentra. La agachada va muy bien.")]
        [SerializeField] private Sprite chargePose;
        [Tooltip("Encadena la entrada confundida despues del aura.")]
        [SerializeField] private bool confusedAfterPowerUp;

        [Header("Aura")]
        [Tooltip("Fotogramas del aura, en orden. Vacio: se usa una copia de la pose.")]
        [SerializeField] private Sprite[] auraFrames;
        [Tooltip("Cada cuanto pasa de fotograma. Muy corto para que llamee.")]
        [SerializeField, Min(0.01f)] private float auraFrameTime = 0.06f;
        [Tooltip("Cuanto mas alta es el aura que la mascota.")]
        [SerializeField, Min(1f)] private float auraHeightFactor = 1.75f;
        [Tooltip("Cuanto sube el aura respecto al centro de la mascota.")]
        [SerializeField] private float auraOffsetY = 0.1f;
        [Tooltip("Color del aura mientras carga.")]
        [SerializeField] private Color auraColor = new Color(0.35f, 0.8f, 1f, 1f);
        [Tooltip("Color al soltarla del todo.")]
        [SerializeField] private Color auraPeakColor = new Color(1f, 0.85f, 0.35f, 1f);
        [Tooltip("Cuanto se pasa de grande el aura respecto a la mascota.")]
        [SerializeField, Range(0f, 0.6f)] private float auraScale = 0.22f;
        [Tooltip("Lo rapido que vibra el aura.")]
        [SerializeField, Min(0.1f)] private float auraPulseSpeed = 14f;
        [Tooltip("Lo que se queda el aura encendida despues del estallido.")]
        [SerializeField, Min(0f)] private float auraHoldDuration = 3f;
        [Tooltip("Lo que tarda en apagarse cuando termina.")]
        [SerializeField, Min(0.1f)] private float auraFadeDuration = 0.6f;
        [Tooltip("Cuanto tiñe a la propia mascota mientras arde.")]
        [SerializeField, Range(0f, 1f)] private float auraBodyTint = 0.45f;
        [Tooltip("Onda expansiva al soltar el aura. Vacio: no sale.")]
        [SerializeField] private Sprite[] burstFrames;
        [SerializeField, Min(0.01f)] private float burstFrameTime = 0.06f;
        [Tooltip("Diametro de la onda, en unidades de mundo.")]
        [SerializeField, Min(0.5f)] private float burstSize = 7f;

        [Header("Entrada confundida")]
        [Tooltip("Lo que dura el desconcierto del principio.")]
        [SerializeField, Min(0f)] private float introDuration = 2.6f;
        [Tooltip("Lo que aguanta mirando a un lado antes de moverse.")]
        [SerializeField, Min(0.05f)] private float lookHold = 0.75f;
        [Tooltip("Lo que dura cada pasito entre mirada y mirada.")]
        [SerializeField, Min(0.05f)] private float lookInterval = 0.45f;
        [Tooltip("Cuanto se balancea de lado a lado al estar confundida.")]
        [SerializeField, Min(0f)] private float confusedSway = 0.09f;
        [Tooltip("Lo que se desplaza en cada pasito de la busqueda inicial.")]
        [SerializeField, Min(0f)] private float confusedStep = 0.55f;

        [Header("Pistas")]
        [Tooltip("Apagar para que la mascota solo mire, sin señalar jugadas.")]
        [SerializeField] private bool giveHints = true;
        [Tooltip("Lo que espera sin ver jugadas del jugador antes de ir a señalar.")]
        [SerializeField, Min(0.5f)] private float idleBeforeHint = 5f;
        [Tooltip("Lo que se queda señalando antes de darse por vencida.")]
        [SerializeField, Min(0.5f)] private float pointDuration = 3f;
        [Tooltip("Lo que dura el enfado cuando le ignoran la pista.")]
        [SerializeField, Min(0.1f)] private float annoyedDuration = 1.3f;
        [Tooltip("Descanso despues de una pista antes de ofrecer la siguiente.")]
        [SerializeField, Min(0f)] private float hintCooldown = 4f;
        [Tooltip("Cuanto se aclaran las dos fichas señaladas.")]
        [SerializeField, Range(0f, 1f)] private float highlightStrength = 0.55f;
        [Tooltip("Latidos por segundo de las fichas señaladas.")]
        [SerializeField, Min(0.1f)] private float highlightSpeed = 2.2f;

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
        [Tooltip("Lo rapido que corre hacia el tablero, en unidades por segundo.")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 5f;
        [Tooltip("Cada cuanto cambia de paso mientras corre.")]
        [SerializeField, Min(0.02f)] private float stepInterval = 0.12f;
        [Tooltip("Hueco que deja entre ella y las fichas al ponerse a señalar.")]
        [SerializeField, Min(0f)] private float hintSideGap = 0.15f;
        [Tooltip("Cuanto sube y baja al andar, en unidades de mundo.")]
        [SerializeField, Min(0f)] private float walkBob = 0.07f;
        [Tooltip("Velocidad a la que estan medidos los pasos; por debajo van mas lentos.")]
        [SerializeField, Min(0.1f)] private float stepReferenceSpeed = 5f;

        [Header("Paseo")]
        [Tooltip("Se pasea por su zona en vez de quedarse plantada en un sitio.")]
        [SerializeField] private bool roam = true;
        [Tooltip("Esquina inferior izquierda de su zona, relativa a su sitio.")]
        [SerializeField] private Vector2 roamMin = new Vector2(-0.7f, -2f);
        [Tooltip("Esquina superior derecha de su zona, relativa a su sitio.")]
        [SerializeField] private Vector2 roamMax = new Vector2(2.4f, 1.8f);
        [Tooltip("Lo rapido que pasea. Mas lento que la carrera de las pistas.")]
        [SerializeField, Min(0.1f)] private float roamSpeed = 2.2f;
        [Tooltip("Lo que descansa entre paseo y paseo, minimo y maximo.")]
        [SerializeField, Min(0f)] private float roamPauseMin = 0.7f;
        [SerializeField, Min(0f)] private float roamPauseMax = 2.4f;

        private SpriteRenderer spriteRenderer;
        private readonly HintFinder hintFinder = new HintFinder();
        private readonly HintHighlighter highlighter = new HintHighlighter();

        private Vector3 restPosition;
        private Vector3 restScale;

        // De donde sale el movimiento este fotograma. En reposo es restPosition,
        // y mientras corre lo va llevando el propio desplazamiento.
        private Vector3 basePosition;
        private Vector3 walkTarget;

        private State state = State.Watching;
        private float stateTimer;

        // En que estado estaba la partida la ultima vez que aviso. Hace falta
        // para distinguir una partida nueva de volver de la pausa: las dos
        // llegan aqui como un cambio a Playing.
        private BoardGame.GameState lastGameState = BoardGame.GameState.Ready;
        private float idleTimer;
        private float cooldownTimer;
        private float stepTimer;
        private bool stepToggle;

        // Andando este fotograma, y en que punto del paso va: los usa el bote
        // para que suba y baje con el pie en vez de flotar a su aire.
        private bool walkingThisFrame;
        private float stepPhase;

        // Pataditas dadas en el enfado actual.
        private int angryStomps;

        // Reposo de dos fotogramas.
        private float idleFrameTimer;
        private bool idleFrameToggle;

        // Fase de la entrada confundida: par mira, impar da un paso.
        private float confusedTimer;
        private int confusedPhase;

        // El aura: una copia del propio sprite, detras y mas grande. Se hace
        // asi y no con un dibujo aparte porque sigue a la pose que lleve puesta
        // sin necesidad de arte nuevo para cada una.
        private SpriteRenderer auraRenderer;
        private float auraIntensity;
        private bool burst;

        // Color original de la mascota, para devolverselo cuando el aura se
        // apaga: mientras arde se le tiñe encima.
        private Color restColor = Color.white;
        private bool warnedAboutAura;

        private HintFinder.Hint currentHint;

        // Paseo: a donde va ahora, cuanto le queda de descanso, y si esta
        // andando o parada.
        private Vector3 roamTarget;
        private float roamPauseTimer;
        private bool roaming;

        // Si la celebracion la pillo fuera de casa por una pista, al terminar
        // tiene que volverse; si estaba paseando, sigue desde donde este.
        private bool returnAfterCelebrate;

        // Las dos fichas concretas que se estan señalando. Se guardan las
        // fichas y no solo las celdas porque la pila que sube desplaza todo el
        // tablero: la celda sigue teniendo ficha, pero ya es otra, y la jugada
        // que la mascota esta enseñando ha dejado de existir.
        private BoardBlock hintFirstBlock;
        private BoardBlock hintSecondBlock;

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
            board ??= FindAnyObjectByType<Board>();
            juice ??= FindAnyObjectByType<BoardJuice>();

            // El sitio de reposo se guarda una vez: todo el movimiento es un
            // desvio sobre el, asi la mascota nunca se va quedando torcida.
            restPosition = transform.localPosition;
            restScale = transform.localScale;
            basePosition = restPosition;
            restColor = spriteRenderer.color;

            if (idlePose == null)
                idlePose = spriteRenderer.sprite;

            CreateAura();
        }

        /// <summary>
        /// Monta el aura: un objeto hijo que repite el sprite de la mascota,
        /// un poco mas grande y por detras. Se crea aqui y no en la escena para
        /// que nadie tenga que mantenerlo sincronizado con las poses.
        /// </summary>
        private void CreateAura()
        {
            GameObject instance = new GameObject("Aura");
            instance.transform.SetParent(transform, false);

            auraRenderer = instance.AddComponent<SpriteRenderer>();
            auraRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            auraRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            auraRenderer.enabled = false;
        }

        /// <summary>
        /// Pone el aura al dia: copia la pose actual, la agranda y la hace
        /// vibrar. Con intensidad cero se apaga del todo para no gastar dibujado
        /// el resto de la partida.
        /// </summary>
        /// <summary>
        /// Fotogramas del aura que han llegado de verdad. Avisa una sola vez si
        /// estan asignados pero vacios, que es lo que pasa cuando la hoja no
        /// importa bien: sin este aviso el aura simplemente no salia y no habia
        /// forma de saber por que.
        /// </summary>
        private int CountUsableFrames()
        {
            if (auraFrames == null)
                return 0;

            int usable = 0;

            foreach (Sprite frame in auraFrames)
                if (frame != null)
                    usable++;

            if (usable == 0 && auraFrames.Length > 0 && !warnedAboutAura)
            {
                warnedAboutAura = true;
                Debug.LogWarning(
                    "El aura tiene " + auraFrames.Length + " huecos asignados pero ningun " +
                    "sprite dentro. Revisa que Aura.png este importado como Multiple.", this);
            }

            return usable;
        }

        private void AdvanceAura()
        {
            if (auraRenderer == null)
                return;

            if (auraIntensity <= 0.01f)
            {
                auraRenderer.enabled = false;

                if (spriteRenderer != null)
                    spriteRenderer.color = restColor;

                return;
            }

            auraRenderer.enabled = true;
            auraRenderer.flipX = false;

            // El aura es su propio dibujo y no una copia de la pose: pasando
            // fotogramas deprisa llamea de verdad, mientras que agrandar la
            // silueta de la mascota solo daba un contorno mas gordo.
            float scaleToSprite;

            int usable = CountUsableFrames();

            if (usable > 0)
            {
                // Solo se cuentan los fotogramas que de verdad han llegado: si
                // la hoja no importa bien, el array queda lleno de huecos y
                // leerlos reventaba en silencio en cada fotograma.
                int step = Mathf.Abs((int)(Time.time / auraFrameTime)) % usable;
                Sprite chosen = null;

                foreach (Sprite candidate in auraFrames)
                {
                    if (candidate == null)
                        continue;

                    if (step-- == 0)
                    {
                        chosen = candidate;
                        break;
                    }
                }

                auraRenderer.sprite = chosen;

                // Se mide contra la mascota para que quede bien sea cual sea la
                // escala del objeto en la escena.
                float mascotHeight = spriteRenderer.sprite != null
                    ? spriteRenderer.sprite.bounds.size.y
                    : 1f;
                float auraHeight = chosen != null ? chosen.bounds.size.y : 0f;
                scaleToSprite = auraHeight > 0f
                    ? mascotHeight * auraHeightFactor / auraHeight
                    : 1f;
            }
            else
            {
                // Respaldo: si no hay dibujo, el contorno agrandado de antes.
                auraRenderer.sprite = spriteRenderer.sprite;
                auraRenderer.flipX = spriteRenderer.flipX;
                scaleToSprite = 1f;
            }

            // Vibra rapido: un aura quieta parece un dibujo pegado.
            float flicker = 1f + Mathf.Sin(Time.time * auraPulseSpeed) * 0.05f;
            float lick =
                Mathf.Abs(Mathf.Sin(Time.time * auraPulseSpeed * 0.63f)) * 0.6f +
                Mathf.Abs(Mathf.Sin(Time.time * auraPulseSpeed * 1.41f)) * 0.4f;

            float grow = scaleToSprite * (1f + auraScale * auraIntensity * flicker);
            auraRenderer.transform.localScale = new Vector3(
                grow,
                grow * (1f + auraScale * auraIntensity * lick * 0.35f),
                1f);

            auraRenderer.transform.localPosition =
                Vector3.up * (auraOffsetY + auraScale * auraIntensity * lick * 0.15f);

            Color tint = Color.Lerp(auraColor, auraPeakColor, auraIntensity);
            tint.a = Mathf.Clamp01(auraIntensity) * 0.85f;
            auraRenderer.color = tint;

            // La propia mascota se dora mientras arde.
            if (spriteRenderer != null && auraBodyTint > 0f)
                spriteRenderer.color = Color.Lerp(
                    restColor,
                    auraPeakColor,
                    auraIntensity * auraBodyTint);
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
            {
                game.GameEnded += HandleToppedOut;
                game.StateChanged += HandleStateChanged;
            }

            // El jugador mueve fichas aunque no encaje ninguna; eso tambien es
            // estar jugando, y no cuenta como estar atascado.
            if (board != null)
                board.BlocksSwapped += HandleSwap;

            lastGameState = game != null ? game.State : BoardGame.GameState.Ready;

            // La partida puede llevar ya rato empezada si la mascota se
            // enciende despues, y entonces nadie va a avisar del cambio.
            if (game != null && game.State == BoardGame.GameState.Playing)
                EnterIntro();
            else
                EnterWatching();
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
            {
                game.GameEnded -= HandleToppedOut;
                game.StateChanged -= HandleStateChanged;
            }

            if (board != null)
                board.BlocksSwapped -= HandleSwap;

            // Las fichas marcadas no son suyas: se devuelven a su color antes
            // de irse, o se quedan encendidas el resto de la partida.
            ClearHint();
        }

        /// <summary>Lleva el estado, el brinco, el temblor y la vuelta al reposo.</summary>
        private void Update()
        {
            // Con Time.deltaTime, no con el sin escalar: en pausa la mascota
            // tiene que quedarse tan quieta como el tablero.
            float delta = Time.deltaTime;

            // Lo pone a cierto el propio Walk cuando de verdad se mueve, asi
            // que hay que limpiarlo antes de repartir el fotograma.
            walkingThisFrame = false;

            AdvanceState(delta);
            AdvanceHop(delta);
            AdvanceShake(delta);
            AdvanceAura();
            highlighter.Tick(delta, highlightStrength, highlightSpeed);
            ApplyTransform();
        }

        // --- Lo que pasa en el tablero --------------------------------------

        /// <summary>
        /// Se han roto fichas. Cuantas mas caen de una vez, mas se celebra:
        /// un tres en raya cualquiera no puede leerse igual que una cascada.
        /// </summary>
        private void HandleMatchResolved(int blockCount)
        {
            idleTimer = 0f;

            bool big = blockCount >= bigMatchSize;

            Celebrate(
                big && cheerPose != null ? cheerPose : celebratePose,
                big ? 1.4f : 1f);
        }

        /// <summary>La racha ha subido lo bastante como para festejarlo.</summary>
        private void HandleComboChanged(int combo)
        {
            if (combo < bigCombo)
                return;

            idleTimer = 0f;
            Celebrate(cheerPose != null ? cheerPose : celebratePose, 1.6f);
        }

        /// <summary>El jugador ha movido algo: no esta atascado.</summary>
        private void HandleSwap(Vector2Int first, Vector2Int second)
        {
            idleTimer = 0f;

            // Si estaba señalando y el jugador se ha puesto a mover fichas, la
            // pista sobra: se recoge y le deja jugar en paz.
            if (state == State.Approaching || state == State.Pointing)
                EnterReturning();
        }

        /// <summary>Ha entrado una fila nueva: un temblor y a seguir.</summary>
        private void HandleRowPushed()
        {
            shakeTimer = 0f;
        }

        /// <summary>Se acabo la partida.</summary>
        private void HandleToppedOut()
        {
            ClearHint();
            state = State.Hurt;
            basePosition = restPosition;

            if (hurtPose != null)
                SetSprite(hurtPose);
        }

        /// <summary>
        /// Una partida nueva devuelve a la mascota a la entrada confundida.
        ///
        /// Entra por cualquier paso a Playing y no solo despues de perder: en la
        /// primera partida el modo llama a StartGame desde su Start, que corre
        /// despues de todos los OnEnable, asi que cuando la mascota se enciende
        /// la partida aun esta en Ready y nunca llegaba a desconcertarse.
        /// Volver de la pausa se descarta aparte, que no es una partida nueva.
        /// </summary>
        private void HandleStateChanged(BoardGame.GameState next)
        {
            BoardGame.GameState previous = lastGameState;
            lastGameState = next;

            if (next != BoardGame.GameState.Playing ||
                previous == BoardGame.GameState.Paused)
                return;

            EnterIntro();
        }

        /// <summary>
        /// La entrada de partida: se concentra y suelta el aura, y solo despues
        /// se pone a mirar. Con el power-up apagado vuelve a la entrada
        /// confundida de antes.
        /// </summary>
        private void EnterIntro()
        {
            if (powerUpAtStart)
                EnterPowerUp();
            else
                EnterConfused();
        }

        // --- Maquina de estados ---------------------------------------------

        /// <summary>Reparte el fotograma al estado que toque.</summary>
        private void AdvanceState(float delta)
        {
            stateTimer += delta;

            if (cooldownTimer > 0f)
                cooldownTimer -= delta;

            switch (state)
            {
                case State.PowerUp:
                    AdvancePowerUp(delta);
                    break;

                case State.Confused:
                    AdvanceConfused(delta);
                    break;

                case State.Watching:
                    AdvanceWatching(delta);
                    break;

                case State.Approaching:
                    AdvanceApproaching(delta);
                    break;

                case State.Pointing:
                    AdvancePointing(delta);
                    break;

                case State.Annoyed:
                    AdvanceAnnoyed();
                    break;

                case State.Returning:
                    AdvanceReturning(delta);
                    break;

                case State.Celebrating:
                    AdvanceCelebrating();
                    break;
            }
        }

        /// <summary>Se planta y empieza a concentrar energia.</summary>
        private void EnterPowerUp()
        {
            state = State.PowerUp;
            stateTimer = 0f;
            idleTimer = 0f;
            cooldownTimer = 0f;
            auraIntensity = 0f;
            burst = false;
            basePosition = restPosition;
            ClearHint();
            SetFacing(true);
            SetSprite(chargePose != null ? chargePose : idlePose);
        }

        /// <summary>
        /// La carga: el aura va subiendo mientras tiembla cada vez mas, y al
        /// final lo suelta de golpe con un salto y un fogonazo. Los ultimos
        /// instantes son los que valen, asi que el aura crece al cuadrado en vez
        /// de a ritmo constante: asi el estallido se siente ganado.
        /// </summary>
        private void AdvancePowerUp(float delta)
        {
            float t = Mathf.Clamp01(stateTimer / powerUpDuration);
            const float BurstAt = 0.78f;

            if (t < BurstAt)
            {
                float charge = t / BurstAt;
                auraIntensity = charge * charge;

                // Tiembla mas cuanto mas cargada va, y hacia el final se
                // levanta un poco del suelo: la carga se lee como que le cuesta
                // contenerla, no como que esta esperando.
                shakeTimer = -1f;
                basePosition = restPosition + new Vector3(
                    Mathf.Sin(Time.time * 55f) * 0.075f * charge,
                    Mathf.Max(0f, charge - 0.55f) * 0.5f,
                    0f);

                // La camara acompaña desde la mitad, subiendo con la carga.
                if (charge > 0.45f)
                    juice?.Shake(0.35f + charge * 0.9f);

                SetSprite(chargePose != null ? chargePose : idlePose);
                return;
            }

            if (!burst)
            {
                burst = true;
                auraIntensity = 1f;
                basePosition = restPosition;

                SetSprite(cheerPose != null ? cheerPose : celebratePose);
                Hop(2.2f);

                // La pantalla acompaña: es el unico momento de la partida en
                // que la mascota manda sobre la camara.
                juice?.Shake(2.4f);
                juice?.Flash(new Color(1f, 0.95f, 0.75f, 0.55f), 0.28f);
                ReleaseShockwave();
            }

            // Despues del estallido el aura se queda ardiendo un rato: es la
            // parte que se ve, y apagandola en cuanto revienta el momento
            // duraba dos fotogramas.
            float sinceBurst = stateTimer - powerUpDuration * BurstAt;

            if (sinceBurst < auraHoldDuration)
            {
                auraIntensity = 1f;
                return;
            }

            auraIntensity = Mathf.Clamp01(
                1f - (sinceBurst - auraHoldDuration) / auraFadeDuration);

            if (auraIntensity > 0f)
                return;

            if (confusedAfterPowerUp)
                EnterConfused();
            else
                EnterWatching();
        }

        /// <summary>
        /// Suelta la onda expansiva del estallido. Reaprovecha los anillos de
        /// la bomba: son el unico dibujo del proyecto que sirve para esto y
        /// pintarlos otra vez seria repetir trabajo hecho.
        /// </summary>
        private void ReleaseShockwave()
        {
            if (burstFrames == null || burstFrames.Length == 0)
                return;

            BombBlast.Play(
                burstFrames,
                ToWorld(basePosition),
                burstSize,
                burstFrameTime,
                spriteRenderer != null ? spriteRenderer.sortingOrder - 2 : 0);
        }

        /// <summary>Entra desconcertada, mirando a un lado y a otro.</summary>
        private void EnterConfused()
        {
            state = State.Confused;
            stateTimer = 0f;
            idleTimer = 0f;
            cooldownTimer = 0f;
            confusedTimer = 0f;
            confusedPhase = 0;
            basePosition = restPosition;
            ClearHint();
            SetSprite(confusedPose != null ? confusedPose : idlePose);
        }

        /// <summary>
        /// Busca algo sin encontrarlo. Alterna mirar con dar un par de pasos
        /// hacia ese lado: quedarse clavada girando el sprite se leia como un
        /// dibujo que parpadea, y andando un poco se lee como que no sabe donde
        /// esta. Cada tres vueltas se gira de espaldas, que remata el gesto.
        /// </summary>
        private void AdvanceConfused(float delta)
        {
            confusedTimer += delta;

            // Mirar dura mas que andar: con las dos fases iguales el gesto
            // pasaba tan rapido que no daba tiempo a leer hacia donde mira.
            bool looking = confusedPhase % 2 == 0;
            float phaseLength = looking ? lookHold : lookInterval;

            if (confusedTimer >= phaseLength)
            {
                confusedTimer = 0f;
                confusedPhase++;
                looking = confusedPhase % 2 == 0;
            }

            bool towardsRight = (confusedPhase / 2) % 2 == 0;
            SetFacing(towardsRight);

            if (looking)
            {
                bool back = (confusedPhase / 2) % 3 == 2;
                SetSprite(back && lookBackPose != null
                    ? lookBackPose
                    : confusedPose != null ? confusedPose : idlePose);
            }
            else
            {
                Vector3 target = restPosition +
                    new Vector3(towardsRight ? confusedStep : -confusedStep, 0f, 0f);
                Walk(target, delta, roamSpeed);
            }

            if (stateTimer >= introDuration)
            {
                // Un respingo al final: se da por vencida y se pone a mirar la
                // partida. Cierra el gesto en vez de cortarlo de golpe.
                Hop(0.55f);
                EnterWatching();
            }
        }

        /// <summary>
        /// Se queda mirando como juega y cuenta cuanto lleva sin jugadas. No
        /// vuelve de golpe a su sitio: si venia paseando sigue desde donde
        /// este, que teletransportarla al rincon se ve fatal.
        /// </summary>
        private void EnterWatching()
        {
            state = State.Watching;
            stateTimer = 0f;
            auraIntensity = 0f;
            ClearHint();
            SetSprite(idlePose);

            roaming = false;
            roamPauseTimer = Random.Range(roamPauseMin, roamPauseMax);
        }

        /// <summary>
        /// Observa y se pasea. Si el jugador lleva demasiado sin mover nada,
        /// busca una jugada y va a enseñarsela.
        /// </summary>
        private void AdvanceWatching(float delta)
        {
            AdvanceRoam(delta);

            if (!giveHints || !CanAct)
                return;

            idleTimer += delta;

            if (idleTimer < idleBeforeHint || cooldownTimer > 0f)
                return;

            if (!hintFinder.TryFind(board, out currentHint))
            {
                // Sin jugadas sobre la mesa no hay nada que señalar; se vuelve
                // a mirar un rato antes de insistir.
                idleTimer = 0f;
                return;
            }

            EnterApproaching();
        }

        /// <summary>
        /// Lleva el paseo: alterna andar hasta un punto al azar de su zona con
        /// pararse un rato. La zona es un rectangulo alrededor de su sitio de
        /// origen, no la pantalla entera, para que nunca se meta por delante
        /// del tablero ni tape el HUD.
        ///
        /// Es asimetrica a proposito: el tablero queda a su izquierda y solo
        /// tiene sitio libre hacia la derecha, asi que con un radio igual por
        /// los dos lados el paseo se le comia el borde de la rejilla.
        /// </summary>
        private void AdvanceRoam(float delta)
        {
            if (!roam)
                return;

            if (roaming)
            {
                if (Walk(roamTarget, delta, roamSpeed))
                {
                    roaming = false;
                    roamPauseTimer = Random.Range(roamPauseMin, roamPauseMax);
                }

                return;
            }

            AdvanceIdlePose(delta);
            roamPauseTimer -= delta;

            if (roamPauseTimer > 0f)
                return;

            Vector3 wanted = restPosition + new Vector3(
                Random.Range(roamMin.x, roamMax.x),
                Random.Range(roamMin.y, roamMax.y),
                0f);

            // Recortado a lo que ve la camara: BoardCameraFitter ajusta el
            // encuadre al arrancar segun la pantalla, asi que la zona buena no
            // se puede dar por sabida desde el inspector.
            roamTarget = ToLocal(ClampToView(ToWorld(wanted)));
            roaming = true;
        }

        /// <summary>
        /// Va cambiando entre los dos dibujos de reposo. La hoja trae dos poses
        /// casi iguales pensadas justo para esto: alternandolas la mascota
        /// parece que respira aunque este parada, y con una sola se quedaba
        /// congelada entre paseo y paseo.
        /// </summary>
        private void AdvanceIdlePose(float delta)
        {
            if (idlePoseB == null)
            {
                SetSprite(idlePose);
                return;
            }

            idleFrameTimer += delta;

            if (idleFrameTimer >= idleFrameInterval)
            {
                idleFrameTimer = 0f;
                idleFrameToggle = !idleFrameToggle;
            }

            SetSprite(idleFrameToggle ? idlePoseB : idlePose);
        }

        /// <summary>Sale corriendo hacia el borde del tablero, a la altura de la jugada.</summary>
        private void EnterApproaching()
        {
            state = State.Approaching;
            stateTimer = 0f;
            walkTarget = GetApproachPosition(currentHint);

            hintFirstBlock = board.GetBlock(currentHint.First);
            hintSecondBlock = board.GetBlock(currentHint.Second);

            highlighter.Begin(hintFirstBlock, hintSecondBlock);
        }

        /// <summary>Corre hasta el sitio y, al llegar, se pone a señalar.</summary>
        private void AdvanceApproaching(float delta)
        {
            // La jugada puede deshacerse mientras va de camino: llegar y
            // señalar un hueco es peor que no haber salido.
            if (!IsHintStillValid())
            {
                EnterReturning();
                return;
            }

            if (Walk(walkTarget, delta, walkSpeed))
                EnterPointing();
        }

        /// <summary>Se planta y señala la jugada.</summary>
        private void EnterPointing()
        {
            state = State.Pointing;
            stateTimer = 0f;
            SetSprite(pointPose != null ? pointPose : idlePose);
            FaceTowards(GetHintWorldPosition(currentHint));
        }

        /// <summary>
        /// Aguanta señalando. Si la jugada se deshace por su cuenta (la pila
        /// empuja, o cae una cascada) deja de tener sentido insistir.
        /// </summary>
        private void AdvancePointing(float delta)
        {
            if (!IsHintStillValid())
            {
                EnterReturning();
                return;
            }

            if (stateTimer >= pointDuration)
                EnterAnnoyed();
        }

        /// <summary>Se cruza de brazos: le han ignorado la pista.</summary>
        private void EnterAnnoyed()
        {
            state = State.Annoyed;
            stateTimer = 0f;
            angryStomps = 0;
            ClearHint();
            SetSprite(angryPose != null ? angryPose : idlePose);
        }

        /// <summary>
        /// Patalea un par de veces y se le pasa el enfado. Los dos saltitos
        /// cortos leen mucho mejor que un temblor solo: se ve que patea el
        /// suelo, no que le tiemble la pantalla.
        /// </summary>
        private void AdvanceAnnoyed()
        {
            if (angryStomps < 2 && stateTimer >= angryStomps * 0.42f)
            {
                angryStomps++;
                Hop(0.45f);
                shakeTimer = 0f;
            }

            if (stateTimer >= annoyedDuration)
                EnterReturning();
        }

        /// <summary>Recoge la pista y se vuelve andando a su rincon.</summary>
        private void EnterReturning()
        {
            state = State.Returning;
            stateTimer = 0f;
            walkTarget = restPosition;
            idleTimer = 0f;
            cooldownTimer = hintCooldown;
            ClearHint();
        }

        /// <summary>Corre de vuelta y se queda mirando otra vez.</summary>
        private void AdvanceReturning(float delta)
        {
            if (Walk(walkTarget, delta, walkSpeed))
                EnterWatching();
        }

        /// <summary>Festeja, pase lo que pase por dentro.</summary>
        private void Celebrate(Sprite pose, float strength)
        {
            if (state == State.Hurt)
                return;

            // Con una pista fuera, celebrar la cancela: el jugador ya ha
            // encajado algo y la mascota no tiene nada que corregirle.
            returnAfterCelebrate = state == State.Approaching || state == State.Pointing;

            if (returnAfterCelebrate)
            {
                ClearHint();
                cooldownTimer = hintCooldown;
            }

            state = State.Celebrating;
            stateTimer = 0f;
            SetSprite(pose != null ? pose : idlePose);
            Hop(strength);
        }

        /// <summary>Cuando se le pasa la alegria vuelve a su sitio o a mirar.</summary>
        private void AdvanceCelebrating()
        {
            if (stateTimer < poseDuration)
                return;

            // Solo se vuelve si la alegria la pillo dando una pista, que es
            // cuando esta lejos de su zona. Si andaba paseando sigue a lo suyo
            // desde donde este.
            if (returnAfterCelebrate)
                EnterReturning();
            else
                EnterWatching();
        }

        // --- Pistas ----------------------------------------------------------

        /// <summary>La partida esta viva y el tablero quieto.</summary>
        private bool CanAct
        {
            get
            {
                if (board == null)
                    return false;

                if (matchSystem != null && matchSystem.IsResolving)
                    return false;

                return game == null ||
                       (game.State == BoardGame.GameState.Playing && !game.IsBusy);
            }
        }

        /// <summary>
        /// La jugada señalada sigue estando ahi. Se comprueba cada fotograma
        /// porque la pila que sube reordena el tablero por debajo: sin esto la
        /// mascota se enfada por una pista que ya no existia.
        ///
        /// Se compara que las celdas sigan teniendo <em>esas mismas</em> fichas
        /// y no solo que tengan alguna: al empujar una fila el tablero entero
        /// se desplaza, y quedarse con la celda daria por buena una jugada que
        /// ahora esta hecha de otras dos fichas cualesquiera.
        /// </summary>
        private bool IsHintStillValid()
        {
            if (board == null || hintFirstBlock == null || hintSecondBlock == null)
                return false;

            // La partida termino o esta en pausa: recoger y a casa.
            if (game != null && game.State != BoardGame.GameState.Playing)
                return false;

            // A proposito NO se mira aqui si el tablero esta ocupado. Estarlo es
            // un estado de un fotograma que salta con cada cascada, con cada
            // fila que empuja la pila y con cada giro del reloj; usarlo para
            // cancelar hacia que la pista se abortase casi siempre a medio
            // camino. Lo que invalida una jugada es que sus fichas se muevan,
            // no que el tablero este resolviendo algo un instante.
            return board.GetBlock(currentHint.First) == hintFirstBlock &&
                   board.GetBlock(currentHint.Second) == hintSecondBlock;
        }

        /// <summary>
        /// Suelta la pista: devuelve las fichas a su color y olvida cuales
        /// eran. Va junto a proposito, que dejar las referencias vivas sin el
        /// resaltado hacia que una pista recogida se diese por buena.
        /// </summary>
        private void ClearHint()
        {
            highlighter.Clear();
            hintFirstBlock = null;
            hintSecondBlock = null;
        }

        /// <summary>El punto del mundo entre las dos fichas de la jugada.</summary>
        private Vector3 GetHintWorldPosition(HintFinder.Hint hint)
        {
            return (board.GridToWorld(hint.First) + board.GridToWorld(hint.Second)) * 0.5f;
        }

        /// <summary>
        /// Media anchura de la mascota en unidades de mundo, sacada del sprite
        /// que lleve puesto. Se calcula y no se guarda porque las poses no
        /// miden todas lo mismo y la escala puede cambiar en la escena.
        /// </summary>
        private float HalfWidth
        {
            get
            {
                if (spriteRenderer == null || spriteRenderer.sprite == null)
                    return 0.5f;

                return spriteRenderer.sprite.bounds.size.x *
                       Mathf.Abs(transform.lossyScale.x) * 0.5f;
            }
        }

        /// <summary>
        /// Donde se planta para señalar: pegada a la jugada, dentro del tablero,
        /// justo al lado de las dos fichas y a su misma altura. Se coloca al
        /// lado y no encima porque el rayo de la pose sale en horizontal, asi
        /// que desde el costado apunta a las fichas en vez de taparlas.
        ///
        /// Elige el lado por el que viene para no cruzar la jugada de largo, y
        /// se recorta a la pantalla por si la combinacion cae contra un borde.
        /// </summary>
        private Vector3 GetApproachPosition(HintFinder.Hint hint)
        {
            Vector3 hintWorld = GetHintWorldPosition(hint);
            float gap = HalfWidth + board.CellSize * 0.5f + hintSideGap;

            // Por el lado del que llega: si esta a la derecha de la jugada se
            // queda a su derecha, y no la rebasa para plantarse al otro lado.
            float side = ToWorld(basePosition).x >= hintWorld.x ? 1f : -1f;

            Vector3 target = new Vector3(
                hintWorld.x + side * gap,
                hintWorld.y,
                ToWorld(restPosition).z);

            return ToLocal(ClampToView(target));
        }

        /// <summary>
        /// Mete un punto dentro de lo que ve la camara, dejando el hueco justo
        /// para que la mascota no asome por el borde.
        /// </summary>
        private Vector3 ClampToView(Vector3 world)
        {
            Camera view = Camera.main;

            if (view == null || !view.orthographic)
                return world;

            float halfHeight = view.orthographicSize;
            float halfWidth = halfHeight * view.aspect;
            Vector3 center = view.transform.position;

            float marginX = HalfWidth + 0.1f;
            float marginY = HalfWidth + 0.1f;

            return new Vector3(
                Mathf.Clamp(world.x, center.x - halfWidth + marginX, center.x + halfWidth - marginX),
                Mathf.Clamp(world.y, center.y - halfHeight + marginY, center.y + halfHeight - marginY),
                world.z);
        }

        // --- Movimiento ------------------------------------------------------

        /// <summary>
        /// Acerca la mascota a un destino y dice si ya ha llegado. Va con
        /// MoveTowards y no con Lerp para que la velocidad sea constante: con
        /// Lerp el ultimo tramo se hace eterno y la carrera pierde la fuerza.
        /// </summary>
        private bool Walk(Vector3 target, float delta, float speed)
        {
            basePosition = Vector3.MoveTowards(basePosition, target, speed * delta);

            bool arrived = (basePosition - target).sqrMagnitude <= 0.0001f;

            if (arrived)
                return true;

            walkingThisFrame = true;
            AdvanceStep(delta, speed);
            FaceTowards(ToWorld(target));
            return false;
        }

        /// <summary>
        /// Alterna los dos pasos de la carrera. La cadencia va con la velocidad:
        /// con un intervalo fijo, paseando despacio los pies patinaban por el
        /// suelo y corriendo se veian dos fotogramas sueltos.
        /// </summary>
        private void AdvanceStep(float delta, float speed)
        {
            if (runPoseA == null && runPoseB == null)
            {
                SetSprite(idlePose);
                return;
            }

            float interval = stepInterval * stepReferenceSpeed / Mathf.Max(0.1f, speed);
            stepTimer += delta;

            if (stepTimer >= interval)
            {
                stepTimer -= interval;
                stepToggle = !stepToggle;
            }

            // De 0 a 1 dentro del paso, para que el bote suba y baje con el pie.
            stepPhase = Mathf.Clamp01(stepTimer / Mathf.Max(0.01f, interval));

            Sprite step = stepToggle ? runPoseB : runPoseA;
            SetSprite(step != null ? step : idlePose);
        }

        /// <summary>Gira la mascota hacia un punto del mundo.</summary>
        private void FaceTowards(Vector3 worldTarget)
        {
            float distance = worldTarget.x - ToWorld(basePosition).x;

            // Casi en la misma columna: girarla ahi seria un tembleque de
            // izquierda a derecha en cada fotograma.
            if (Mathf.Abs(distance) < 0.01f)
                return;

            SetFacing(distance > 0f);
        }

        /// <summary>Pone la mascota mirando a la derecha o a la izquierda.</summary>
        private void SetFacing(bool right)
        {
            if (spriteRenderer != null)
                spriteRenderer.flipX = posesFaceRight ? !right : right;
        }

        /// <summary>Cambia la pose, sin repintar si ya era esa.</summary>
        private void SetSprite(Sprite pose)
        {
            if (pose == null || spriteRenderer == null || spriteRenderer.sprite == pose)
                return;

            spriteRenderer.sprite = pose;
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
        /// sitio porque el brinco, el temblor, el balanceo y la respiracion
        /// mueven lo mismo, y aplicandolos por separado el ultimo pisaria a los
        /// demas.
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
            else if (walkingThisFrame)
            {
                // Un bote por paso: sube al empujar y baja al apoyar. Sin esto
                // la mascota se desliza por el suelo como sobre hielo.
                height = Mathf.Sin(stepPhase * Mathf.PI) * walkBob;
                stretch = -squash * 0.35f * Mathf.Sin(stepPhase * Mathf.PI);
            }
            else if (state == State.Watching || state == State.Confused)
            {
                // En reposo respira, para que no parezca una calcomania pegada.
                height = Mathf.Sin(Time.time * breathSpeed) * breathAmount;
            }

            float sideways = 0f;

            if (state == State.Confused)
                sideways = Mathf.Sin(stateTimer * 3.4f) * confusedSway;

            if (state == State.Pointing)
            {
                // Late mientras apunta y da un retroceso hacia atras, como si el
                // rayo tirase de ella. Una pose quieta con un rayo pegado se ve
                // como una calcomania; con el pulso se lee que esta disparando.
                float beat = Mathf.Sin(stateTimer * 7f);
                stretch = 0.05f * beat;
                sideways = (spriteRenderer != null && spriteRenderer.flipX ? 1f : -1f) *
                           Mathf.Abs(beat) * 0.07f;
            }

            if (shakeTimer >= 0f)
            {
                // Se apaga solo: empieza fuerte y se va quedando en nada.
                float fade = 1f - Mathf.Clamp01(shakeTimer / 0.3f);
                sideways += Mathf.Sin(shakeTimer * 60f) * shakeStrength * fade;
            }

            transform.localPosition = basePosition + new Vector3(sideways, height, 0f);
            transform.localScale = new Vector3(
                restScale.x * (1f - stretch),
                restScale.y * (1f + stretch),
                restScale.z);
        }

        /// <summary>Pasa una posicion local de la mascota a coordenadas de mundo.</summary>
        private Vector3 ToWorld(Vector3 local)
        {
            return transform.parent != null
                ? transform.parent.TransformPoint(local)
                : local;
        }

        /// <summary>Pasa un punto del mundo al espacio local de la mascota.</summary>
        private Vector3 ToLocal(Vector3 world)
        {
            return transform.parent != null
                ? transform.parent.InverseTransformPoint(world)
                : world;
        }
    }
}
