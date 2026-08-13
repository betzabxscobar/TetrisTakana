using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// El arsenal de bombas. Se llena de tres en tres cada vez que el marcador
    /// cruza un escalon de puntos, y se gasta de una en una reventando el
    /// cuadro de fichas que rodea a una celda.
    ///
    /// Aqui solo vive el recuento y la explosion. Quien decide cuando entra una
    /// bomba al tablero es la mascota (<see cref="MascotReactor"/>), que se
    /// mete a soltarla cuando el jugador lleva un rato sin tocar nada: es una
    /// ayuda para el que se ha quedado atascado, no un boton mas.
    ///
    /// Es la valvula de escape del modo: la pila sube sola y hay momentos en
    /// los que no queda ningun intercambio que forme linea. Sin bombas ahi solo
    /// cabe esperar a que la fila nueva arregle el tablero, y muchas veces la
    /// fila nueva es justo la que te mata.
    ///
    /// Reventar no da puntos a proposito. Si diera, cada bomba acercaria el
    /// escalon siguiente y con el bombas nuevas, y la partida se resolveria
    /// sola a base de detonar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BombSystem : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] private Board board;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private MatchSystem matchSystem;
        [SerializeField] private Gravity gravity;
        [SerializeField] private Match3Game game;
        [Tooltip("Solo hace falta para la detonacion manual, que apunta al cursor.")]
        [SerializeField] private GridCursor cursor;

        [Header("Reparto")]
        [Tooltip("Puntos que hay que sumar para cobrar el lote siguiente.")]
        [SerializeField, Min(1)] private int pointsPerReward = 60;
        [Tooltip("Bombas que entran en cada lote.")]
        [SerializeField, Min(1)] private int bombsPerReward = 3;
        [Tooltip("Con las que se empieza la partida.")]
        [SerializeField, Min(0)] private int startingBombs;
        [SerializeField, Min(1)] private int maximumBombs = 99;

        [Header("Explosion")]
        [Tooltip("Celdas a cada lado del cursor. 1 revienta un cuadro de 3x3.")]
        [SerializeField, Min(0)] private int radius = 1;
        [Tooltip("Lo que dura el fogonazo antes de que caiga lo de arriba.")]
        [SerializeField, Min(0f)] private float blastDuration = 0.22f;
        [SerializeField] private AudioClip explosionClip;

        [Header("Control")]
        [Tooltip("Deja que el jugador detone con el teclado. Apagado, las bombas " +
                 "solo entran al tablero de la mano de la mascota.")]
        [SerializeField] private bool allowManualDetonation;
        [SerializeField] private Key detonateKey = Key.B;
        [Tooltip("Segunda tecla para lo mismo; util con la mano en el WASD.")]
        [SerializeField] private Key alternateKey = Key.Q;

        private static Sprite blastSprite;

        private readonly List<Vector2Int> blast = new List<Vector2Int>();
        private Coroutine detonation;
        private int nextRewardScore;

        /// <summary>Bombas que le quedan al jugador.</summary>
        public int Bombs { get; private set; }

        /// <summary>Celdas a cada lado del punto de impacto.</summary>
        public int Radius => radius;

        /// <summary>Se puede detonar ahora mismo.</summary>
        public bool CanDetonate =>
            Bombs > 0 &&
            detonation == null &&
            board != null &&
            (game == null || game.AcceptsInput) &&
            (matchSystem == null || !matchSystem.IsResolving);

        /// <summary>Cambio el numero de bombas; en positivo cuando se cobran.</summary>
        public event Action<int, int> BombsChanged;

        /// <summary>Busca en el propio objeto los sistemas que no vengan asignados.</summary>
        private void Awake()
        {
            board ??= GetComponent<Board>();
            scoreManager ??= GetComponent<ScoreManager>();
            matchSystem ??= GetComponent<MatchSystem>();
            gravity ??= GetComponent<Gravity>();
            game ??= GetComponent<Match3Game>();
            cursor ??= FindAnyObjectByType<GridCursor>();

            Bombs = startingBombs;
            nextRewardScore = pointsPerReward;
        }

        /// <summary>Se pone a escuchar el marcador, que es quien reparte.</summary>
        private void OnEnable()
        {
            if (scoreManager != null)
                scoreManager.ScoreChanged += HandleScoreChanged;
        }

        /// <summary>Deja de escuchar el marcador y corta una detonacion a medias.</summary>
        private void OnDisable()
        {
            if (scoreManager != null)
                scoreManager.ScoreChanged -= HandleScoreChanged;

            if (detonation == null)
                return;

            StopCoroutine(detonation);
            detonation = null;
            game?.SetHold(false);
        }

        /// <summary>Reparte el HUD inicial cuando ya hay quien escuche.</summary>
        private void Start()
        {
            BombsChanged?.Invoke(Bombs, 0);
        }

        /// <summary>Lee la tecla de detonar, si es que se juega asi.</summary>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (!allowManualDetonation || keyboard == null)
                return;

            if (WasPressed(keyboard, detonateKey) ||
                (alternateKey != detonateKey && WasPressed(keyboard, alternateKey)))
                TryDetonate();
        }

        /// <summary>Mira una tecla del teclado, saltandose la tecla vacia.</summary>
        private static bool WasPressed(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        /// <summary>
        /// El marcador se movio: se cobran de golpe todos los escalones que se
        /// hayan cruzado, que una cascada larga puede pasar de mil puntos y
        /// saltarse varios lotes de una sentada.
        /// </summary>
        private void HandleScoreChanged(int score, int multiplier)
        {
            // El marcador a cero es una partida nueva: se vuelve a empezar con
            // las bombas de salida y el primer escalon por delante.
            if (score == 0)
            {
                ResetBombs();
                return;
            }

            int reward = 0;

            while (score >= nextRewardScore)
            {
                reward += bombsPerReward;
                nextRewardScore += pointsPerReward;
            }

            if (reward > 0)
                Add(reward);
        }

        /// <summary>Deja el arsenal como al empezar una partida.</summary>
        public void ResetBombs()
        {
            Bombs = startingBombs;
            nextRewardScore = pointsPerReward;
            BombsChanged?.Invoke(Bombs, 0);
        }

        /// <summary>Suma bombas al arsenal sin pasarse del tope.</summary>
        public void Add(int amount)
        {
            if (amount <= 0)
                return;

            int before = Bombs;
            Bombs = Mathf.Min(Bombs + amount, maximumBombs);

            if (Bombs != before)
                BombsChanged?.Invoke(Bombs, Bombs - before);
        }

        /// <summary>
        /// Revienta el cuadro que rodea al cursor. Si ahi no queda ninguna
        /// ficha no se gasta nada: la bomba es cara y tirarla contra el vacio
        /// seria un castigo por un error de puntería.
        /// </summary>
        public bool TryDetonate()
        {
            return cursor != null && TryDetonate(cursor.CurrentPosition);
        }

        /// <summary>Revienta el cuadro que rodea a una celda cualquiera.</summary>
        public bool TryDetonate(Vector2Int center)
        {
            if (!CanDetonate)
                return false;

            CollectBlast(center);

            if (blast.Count == 0)
                return false;

            Bombs--;
            BombsChanged?.Invoke(Bombs, -1);

            detonation = StartCoroutine(Detonate(center));
            return true;
        }

        /// <summary>
        /// Busca donde conviene mas soltar la bomba: el cuadro que se lleva por
        /// delante mas fichas, y a igualdad de fichas el que este mas arriba,
        /// que es de donde viene el peligro. Es lo que usa la mascota cuando
        /// entra al tablero a echar una mano.
        /// </summary>
        public bool TryFindTarget(out Vector2Int target)
        {
            target = Vector2Int.zero;

            if (board == null)
                return false;

            int best = 0;

            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                Vector2Int center = new Vector2Int(x, y);
                int count = CountBlast(center);

                // Mayor o igual recorriendo de abajo arriba: con el mismo
                // botin se queda con el cuadro mas alto.
                if (count == 0 || count < best)
                    continue;

                best = count;
                target = center;
            }

            return best > 0;
        }

        /// <summary>Cuenta las fichas que caerian con la bomba en esa celda.</summary>
        private int CountBlast(Vector2Int center)
        {
            int count = 0;

            for (int x = center.x - radius; x <= center.x + radius; x++)
            for (int y = center.y - radius; y <= center.y + radius; y++)
                if (board.IsOccupied(new Vector2Int(x, y)))
                    count++;

            return count;
        }

        /// <summary>Reune las celdas con ficha que caen dentro del radio.</summary>
        private void CollectBlast(Vector2Int center)
        {
            blast.Clear();

            for (int x = center.x - radius; x <= center.x + radius; x++)
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (board.IsOccupied(position))
                    blast.Add(position);
            }
        }

        /// <summary>
        /// Quita las fichas del cuadro, deja caer lo de arriba y resuelve las
        /// combinaciones que se hayan cerrado al caer; esas si puntuan, que las
        /// ha encadenado el jugador eligiendo donde poner la bomba.
        /// </summary>
        private IEnumerator Detonate(Vector2Int center)
        {
            // SetHold y no SetBusy, igual que la pila: Match3Game reescribe
            // busy cada frame con el estado de las cascadas y borraria la
            // retencion a media explosion.
            game?.SetHold(true);

            SpawnFlash(center);
            Play(explosionClip);

            foreach (Vector2Int position in blast)
            {
                // Sin destruir: la ficha se despega del tablero y se apaga con
                // su propia animacion, que reventar con un Destroy seco no se
                // ve ni se oye.
                BoardBlock block = board.RemoveBlock(position, false);

                if (block != null)
                    StartCoroutine(PopBlock(block));
            }

            blast.Clear();
            yield return new WaitForSeconds(blastDuration);

            // La partida puede haber terminado o haber vuelto a empezar
            // mientras duraba el fogonazo.
            if (game != null && game.State != BoardGame.GameState.Playing)
            {
                game.SetHold(false);
                detonation = null;
                yield break;
            }

            if (gravity != null)
                yield return gravity.ApplyGravity();

            if (matchSystem != null)
                yield return matchSystem.ResolveExisting();

            game?.SetHold(false);
            detonation = null;
        }

        /// <summary>Infla la ficha y la apaga antes de destruirla.</summary>
        private IEnumerator PopBlock(BoardBlock block)
        {
            SpriteRenderer renderer = block.GetComponent<SpriteRenderer>();
            Vector3 scale = block.transform.localScale;
            Color color = renderer != null ? renderer.color : Color.white;
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, blastDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                block.transform.localScale = scale * (1f + progress * 0.45f);

                if (renderer != null)
                    renderer.color = new Color(
                        color.r,
                        color.g,
                        color.b,
                        color.a * (1f - progress));

                yield return null;
            }

            Destroy(block.gameObject);
        }

        /// <summary>
        /// El fogonazo: un circulo que crece y se apaga sobre la celda. Se
        /// dibuja por codigo para que la bomba funcione sin tener que colocar
        /// ningun sprite en la escena.
        /// </summary>
        private void SpawnFlash(Vector2Int center)
        {
            if (board == null)
                return;

            GameObject instance = new GameObject("BombFlash");
            instance.transform.SetParent(board.BlocksRoot, false);
            instance.transform.position = board.GridToWorld(center);

            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = GetBlastSprite();
            renderer.color = new Color(1f, 0.85f, 0.45f, 0.9f);

            // Por delante de las fichas, que es lo que se esta reventando.
            renderer.sortingOrder = 30;

            StartCoroutine(AnimateFlash(instance, renderer));
        }

        /// <summary>Crece el fogonazo hasta cubrir el cuadro y lo desvanece.</summary>
        private IEnumerator AnimateFlash(GameObject instance, SpriteRenderer renderer)
        {
            Sprite sprite = renderer.sprite;
            float unit = sprite != null ? Mathf.Max(sprite.bounds.size.x, 0.01f) : 1f;
            float target = board.CellSize * (radius * 2f + 1.4f) / unit;
            float duration = Mathf.Max(0.05f, blastDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // Arranca de golpe y se frena: es lo que se lee como estallido
                // y no como una burbuja que se hincha.
                float eased = 1f - (1f - progress) * (1f - progress);
                float scale = target * (0.25f + eased * 0.75f);

                instance.transform.localScale = new Vector3(scale, scale, 1f);
                renderer.color = new Color(1f, 0.85f, 0.45f, 0.9f * (1f - progress));

                yield return null;
            }

            Destroy(instance);
        }

        /// <summary>Genera una vez el circulo difuminado que usa el fogonazo.</summary>
        private static Sprite GetBlastSprite()
        {
            if (blastSprite != null)
                return blastSprite;

            const int size = 64;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "BombBlast"
            };

            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) * 2f;

                // Nucleo lleno y borde que se apaga: sin la caida suave el
                // circulo se ve dentado sobre las fichas.
                float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, distance - 0.45f) / 0.55f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * alpha * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            blastSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            blastSprite.name = "BombBlast";
            return blastSprite;
        }

        /// <summary>Reproduce el estallido si hay clip y mezclador.</summary>
        private void Play(AudioClip clip)
        {
            if (clip == null)
                return;

            AudioManager audioManager = AudioManager.Instance;

            if (audioManager != null)
                audioManager.PlaySfx(clip);
        }
    }
}
