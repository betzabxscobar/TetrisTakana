using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana.Match3
{
    public class HudController : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private ComboSystem comboSystem;
        private Text label;

        private void Awake()
        {
            scoreManager ??= FindAnyObjectByType<ScoreManager>();
            comboSystem ??= FindAnyObjectByType<ComboSystem>();
            CreateLabelIfNeeded();
        }

        private void OnEnable()
        {
            if (scoreManager != null)
                scoreManager.ScoreChanged += HandleScoreChanged;
            if (comboSystem != null)
                comboSystem.ComboChanged += HandleComboChanged;
        }

        private void OnDisable()
        {
            if (scoreManager != null)
                scoreManager.ScoreChanged -= HandleScoreChanged;
            if (comboSystem != null)
                comboSystem.ComboChanged -= HandleComboChanged;
        }

        private void Start()
        {
            Refresh();
        }

        private void HandleScoreChanged(int score, int multiplier)
        {
            Refresh();
        }

        private void HandleComboChanged(int combo)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (label == null)
                return;

            int score = scoreManager != null ? scoreManager.Score : 0;
            int combo = comboSystem != null ? comboSystem.CurrentCombo : 0;
            label.text = $"PUNTAJE: {score}\nCOMBO: x{Mathf.Max(1, combo)}";
        }

        private void CreateLabelIfNeeded()
        {
            GameObject canvasObject = new GameObject("Match3 HUD");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject textObject = new GameObject("Score and Combo");
            textObject.transform.SetParent(canvasObject.transform, false);
            label = textObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.color = Color.white;
            label.alignment = TextAnchor.UpperLeft;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(300f, 100f);
        }
    }
}
