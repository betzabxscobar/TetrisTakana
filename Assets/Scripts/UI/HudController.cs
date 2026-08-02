using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// HUD de puntaje, líneas y nivel, más los avisos de pausa y fin de
    /// partida. Se genera por código sobre un CanvasScaler adaptativo, así que
    /// se lee igual en 1280x720 que en 4K. Mayu puede sustituirlo asignando
    /// sus propios Text en el inspector.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private TetrisGame game;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private DifficultySystem difficulty;

        [Header("Etiquetas (opcionales: si faltan se crean)")]
        [SerializeField] private Text statsLabel;
        [SerializeField] private Text messageLabel;

        [Header("Estilo del HUD por defecto")]
        [SerializeField, Min(8)] private int fontSize = 34;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Vector2 margin = new Vector2(48f, 48f);

        private void Awake()
        {
            game ??= FindAnyObjectByType<TetrisGame>();
            scoreManager ??= FindAnyObjectByType<ScoreManager>();
            difficulty ??= FindAnyObjectByType<DifficultySystem>();

            if (statsLabel == null || messageLabel == null)
                CreateDefaultHud();
        }

        private void OnEnable()
        {
            if (scoreManager != null)
            {
                scoreManager.ScoreChanged += HandleScoreChanged;
                scoreManager.LinesChanged += HandleLinesChanged;
            }

            if (difficulty != null)
                difficulty.LevelChanged += HandleLevelChanged;

            if (game != null)
                game.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (scoreManager != null)
            {
                scoreManager.ScoreChanged -= HandleScoreChanged;
                scoreManager.LinesChanged -= HandleLinesChanged;
            }

            if (difficulty != null)
                difficulty.LevelChanged -= HandleLevelChanged;

            if (game != null)
                game.StateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            Refresh();
            HandleStateChanged(game != null ? game.State : TetrisGame.GameState.Ready);
        }

        private void HandleScoreChanged(int score, int gained) => Refresh();

        private void HandleLinesChanged(int lines) => Refresh();

        private void HandleLevelChanged(int level) => Refresh();

        private void HandleStateChanged(TetrisGame.GameState state)
        {
            if (messageLabel == null)
                return;

            switch (state)
            {
                case TetrisGame.GameState.Paused:
                    messageLabel.text = "PAUSA\n\nEsc para continuar";
                    break;

                case TetrisGame.GameState.GameOver:
                    int score = scoreManager != null ? scoreManager.Score : 0;
                    messageLabel.text =
                        $"FIN DE LA PARTIDA\n\nPuntaje {score}\n\nEnter para jugar de nuevo";
                    break;

                default:
                    messageLabel.text = string.Empty;
                    break;
            }

            Refresh();
        }

        private void Refresh()
        {
            if (statsLabel == null)
                return;

            int score = scoreManager != null ? scoreManager.Score : 0;
            int lines = scoreManager != null ? scoreManager.TotalLines : 0;
            int level = difficulty != null ? difficulty.Level : 1;

            statsLabel.text = $"PUNTAJE\n{score}\n\nLÍNEAS\n{lines}\n\nNIVEL\n{level}";
        }

        private void CreateDefaultHud()
        {
            GameObject canvasObject = new GameObject("Tetris HUD Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            // Sin esto el HUD conserva su tamaño en píxeles y queda diminuto
            // en pantallas grandes.
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ScreenSetup.DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            if (statsLabel == null)
                statsLabel = CreateLabel(
                    canvasObject.transform,
                    "Estadisticas",
                    new Vector2(0f, 1f),
                    new Vector2(margin.x, -margin.y),
                    new Vector2(420f, 520f),
                    TextAnchor.UpperLeft,
                    fontSize);

            if (messageLabel == null)
            {
                messageLabel = CreateLabel(
                    canvasObject.transform,
                    "Mensaje",
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(900f, 400f),
                    TextAnchor.MiddleCenter,
                    Mathf.RoundToInt(fontSize * 1.5f));
                messageLabel.text = string.Empty;
            }
        }

        private Text CreateLabel(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            TextAnchor alignment,
            int labelFontSize)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            Text label = textObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = labelFontSize;
            label.color = textColor;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(
                Mathf.Approximately(anchor.x, 0.5f) ? 0.5f : anchor.x,
                Mathf.Approximately(anchor.y, 0.5f) ? 0.5f : anchor.y);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            return label;
        }
    }
}
