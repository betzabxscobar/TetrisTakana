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

        [Header("Poses")]
        [Tooltip("Como esta cuando no pasa nada.")]
        [SerializeField] private Sprite idlePose;
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

        [Header("Entrada confundida")]
        [Tooltip("Lo que dura el desconcierto del principio.")]
        [SerializeField, Min(0f)] private float introDuration = 2.6f;
        [Tooltip("Cada cuanto mira al otro lado mientras esta confundida.")]
        [SerializeField, Min(0.05f)] private float lookInterval = 0.45f;
        [Tooltip("Cuanto se balancea de lado a lado al estar confundida.")]
        [SerializeField, Min(0f)] private float confusedSway = 0.09f;

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
        [Tooltip("Hueco que deja entre ella y el borde del tablero al señalar.")]
        [SerializeField, Min(0f)] private float approachMargin = 0.9f;

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
        private float idleTimer;
        private float cooldownTimer;
        private float stepTimer;
        private bool stepToggle;

        private HintFinder.Hint currentHint;

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

            // El sitio de reposo se guarda una vez: todo el movimiento es un
            // desvio sobre el, asi la mascota nunca se va quedando torcida.
            restPosition = transform.localPosition;
            restScale = transform.localScale;
            basePosition = restPosition;

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
            {
                game.GameEnded += HandleToppedOut;
                game.StateChanged += HandleStateChanged;
            }

            // El jugador mueve fichas aunque no encaje ninguna; eso tambien es
            // estar jugando, y no cuenta como estar atascado.
            if (board != null)
                board.BlocksSwapped += HandleSwap;

            // La partida puede llevar ya rato empezada si la mascota se
            // enciende despues, y entonces nadie va a avisar del cambio.
            if (game != null && game.State == BoardGame.GameState.Playing)
                EnterConfused();
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

            AdvanceState(delta);
            AdvanceHop(delta);
            AdvanceShake(delta);
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

        /// <summary>Una partida nueva devuelve a la mascota a la entrada confundida.</summary>
        private void HandleStateChanged(BoardGame.GameState next)
        {
            if (next == BoardGame.GameState.Playing && state == State.Hurt)
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

        /// <summary>Entra desconcertada, mirando a un lado y a otro.</summary>
        private void EnterConfused()
        {
            state = State.Confused;
            stateTimer = 0f;
            idleTimer = 0f;
            cooldownTimer = 0f;
            basePosition = restPosition;
            ClearHint();
            SetSprite(confusedPose != null ? confusedPose : idlePose);
        }

        /// <summary>
        /// Va girandose de un lado al otro. Alterna la pose de perfil con la de
        /// espaldas para que el desconcierto se lea como buscar algo, y no como
        /// un sprite que parpadea.
        /// </summary>
        private void AdvanceConfused(float delta)
        {
            int look = Mathf.FloorToInt(stateTimer / Mathf.Max(0.05f, lookInterval));
            bool back = look % 4 == 3;

            SetSprite(back && lookBackPose != null
                ? lookBackPose
                : confusedPose != null ? confusedPose : idlePose);

            SetFacing(look % 2 == 0);

            if (stateTimer >= introDuration)
                EnterWatching();
        }

        /// <summary>Se queda mirando como juega y cuenta cuanto lleva sin jugadas.</summary>
        private void EnterWatching()
        {
            state = State.Watching;
            stateTimer = 0f;
            basePosition = restPosition;
            ClearHint();
            SetSprite(idlePose);
            SetFacing(false);
        }

        /// <summary>
        /// Observa. Si el jugador lleva demasiado sin mover nada, busca una
        /// jugada y va a enseñarsela.
        /// </summary>
        private void AdvanceWatching(float delta)
        {
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

            if (Walk(walkTarget, delta))
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
            ClearHint();
            SetSprite(angryPose != null ? angryPose : idlePose);

            // Una patadita en el suelo para que el enfado se note sin texto.
            shakeTimer = 0f;
        }

        /// <summary>Se le pasa el enfado y se vuelve a su sitio.</summary>
        private void AdvanceAnnoyed()
        {
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
            if (Walk(walkTarget, delta))
                EnterWatching();
        }

        /// <summary>Festeja, pase lo que pase por dentro.</summary>
        private void Celebrate(Sprite pose, float strength)
        {
            if (state == State.Hurt)
                return;

            // Con una pista fuera, celebrar la cancela: el jugador ya ha
            // encajado algo y la mascota no tiene nada que corregirle.
            if (state == State.Approaching || state == State.Pointing)
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

            // Si celebro lejos de casa, se vuelve andando en vez de aparecer
            // de golpe en el rincon.
            if ((basePosition - restPosition).sqrMagnitude > 0.0001f)
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
            if (!CanAct || hintFirstBlock == null || hintSecondBlock == null)
                return false;

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
        /// Donde se planta para señalar: fuera del tablero, por el lado en el
        /// que ya vive, y a la altura de la jugada. Fuera y no encima porque la
        /// mascota es mas alta que dos celdas y taparia justo las fichas que
        /// intenta enseñar.
        ///
        /// Los limites salen de las cuatro esquinas y no de la anchura, porque
        /// el reloj de arena gira el tablero y entonces el ancho y el alto se
        /// intercambian.
        /// </summary>
        private Vector3 GetApproachPosition(HintFinder.Hint hint)
        {
            Vector3 corner00 = board.GridToWorld(new Vector2Int(0, 0));
            Vector3 corner10 = board.GridToWorld(new Vector2Int(board.Width - 1, 0));
            Vector3 corner01 = board.GridToWorld(new Vector2Int(0, board.Height - 1));
            Vector3 corner11 = board.GridToWorld(new Vector2Int(board.Width - 1, board.Height - 1));

            float minX = Mathf.Min(Mathf.Min(corner00.x, corner10.x), Mathf.Min(corner01.x, corner11.x));
            float maxX = Mathf.Max(Mathf.Max(corner00.x, corner10.x), Mathf.Max(corner01.x, corner11.x));
            float minY = Mathf.Min(Mathf.Min(corner00.y, corner10.y), Mathf.Min(corner01.y, corner11.y));
            float maxY = Mathf.Max(Mathf.Max(corner00.y, corner10.y), Mathf.Max(corner01.y, corner11.y));

            Vector3 restWorld = ToWorld(restPosition);
            float centerX = (minX + maxX) * 0.5f;

            // Se queda del lado en el que ya esta: cruzar al otro la obligaria
            // a pasar por delante del tablero entero.
            float laneX = restWorld.x >= centerX
                ? maxX + approachMargin
                : minX - approachMargin;

            float targetY = Mathf.Clamp(GetHintWorldPosition(hint).y, minY, maxY);

            return ToLocal(new Vector3(laneX, targetY, restWorld.z));
        }

        // --- Movimiento ------------------------------------------------------

        /// <summary>
        /// Acerca la mascota a un destino y dice si ya ha llegado. Va con
        /// MoveTowards y no con Lerp para que la velocidad sea constante: con
        /// Lerp el ultimo tramo se hace eterno y la carrera pierde la fuerza.
        /// </summary>
        private bool Walk(Vector3 target, float delta)
        {
            basePosition = Vector3.MoveTowards(basePosition, target, walkSpeed * delta);

            bool arrived = (basePosition - target).sqrMagnitude <= 0.0001f;

            if (arrived)
                return true;

            AdvanceStep(delta);
            FaceTowards(ToWorld(target));
            return false;
        }

        /// <summary>Alterna los dos pasos de la carrera.</summary>
        private void AdvanceStep(float delta)
        {
            if (runPoseA == null && runPoseB == null)
            {
                SetSprite(idlePose);
                return;
            }

            stepTimer += delta;

            if (stepTimer >= stepInterval)
            {
                stepTimer = 0f;
                stepToggle = !stepToggle;
            }

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
            else if (state == State.Watching || state == State.Confused)
            {
                // En reposo respira, para que no parezca una calcomania pegada.
                height = Mathf.Sin(Time.time * breathSpeed) * breathAmount;
            }

            float sideways = 0f;

            if (state == State.Confused)
                sideways = Mathf.Sin(stateTimer * 3.4f) * confusedSway;

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
