using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TetrisTakana.Editor
{
    /// <summary>
    /// Crea la interfaz editable de la escena Puntuaciones.
    /// Se conserva como herramienta para reconstruirla si fuese necesario.
    /// </summary>
    public static class LeaderboardSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Puntuaciones.unity";
        private const int RowCount = 10;

        [MenuItem("TetrisTakana/Construir escena Puntuaciones")]
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Main Camera")
                {
                    Object.DestroyImmediate(root);
                    continue;
                }

                LeaderboardSceneController oldController =
                    root.GetComponent<LeaderboardSceneController>();

                if (oldController != null)
                    Object.DestroyImmediate(oldController);
            }

            GameObject controllerObject = new GameObject("LeaderboardSceneController");
            controllerObject.AddComponent<TetrisTakana.LeaderboardSceneController>();

            CreateEventSystem();

            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage("Background", canvasObject.transform, new Color(0.015f, 0.02f, 0.07f, 1f));
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            Image panel = CreateImage("RankingPanel", canvasObject.transform, new Color(0.035f, 0.045f, 0.12f, 0.98f));
            Center(panel.rectTransform, new Vector2(1200f, 850f), new Vector2(0f, 0f));
            AddOutline(panel.gameObject, new Color(0.15f, 0.78f, 1f, 0.8f));

            CreateText("Title", panel.transform, "MEJORES PUNTUACIONES", 52, Color.white, new Vector2(0f, 355f), new Vector2(1080f, 70f));

            RectTransform header = CreateLayoutRect("HeaderRow", panel.transform, new Vector2(1080f, 40f), new Vector2(0f, 270f));
            ConfigureHorizontalLayout(header, 4f, false);
            CreateColumnText(header, "Posicion", "POSICION", 130f, 22, new Color(0.72f, 0.78f, 0.9f, 1f));
            CreateColumnText(header, "Puntuacion", "PUNTUACION", 300f, 22, new Color(0.72f, 0.78f, 0.9f, 1f));
            CreateColumnText(header, "Lineas", "LINEAS", 220f, 22, new Color(0.72f, 0.78f, 0.9f, 1f));
            CreateColumnText(header, "Nivel", "NIVEL", 150f, 22, new Color(0.72f, 0.78f, 0.9f, 1f));
            CreateColumnText(header, "Fecha", "FECHA", 220f, 22, new Color(0.72f, 0.78f, 0.9f, 1f));

            RectTransform rowsContainer = CreateLayoutRect("RowsContainer", panel.transform, new Vector2(1080f, 600f), new Vector2(0f, -20f));
            ConfigureVerticalLayout(rowsContainer, 8f);

            for (int index = 0; index < RowCount; index++)
                CreateRow(rowsContainer, index);

            Text emptyMessage = CreateText("EmptyMessage", panel.transform, "AUN NO HAY PUNTUACIONES", 30, new Color(0.72f, 0.78f, 0.9f, 1f), new Vector2(0f, -10f), new Vector2(800f, 60f));
            emptyMessage.gameObject.SetActive(false);

            Button backButton = CreateButton("BackButton", panel.transform, "VOLVER AL MENU", new Vector2(0f, -370f));

            LeaderboardSceneController controller = controllerObject.GetComponent<LeaderboardSceneController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("rows").arraySize = RowCount;

            SerializedProperty rowsProperty = serializedController.FindProperty("rows");
            LeaderboardRowUI[] rowComponents = rowsContainer.GetComponentsInChildren<LeaderboardRowUI>();

            for (int index = 0; index < rowComponents.Length; index++)
                rowsProperty.GetArrayElementAtIndex(index).objectReferenceValue = rowComponents[index];

            serializedController.FindProperty("emptyMessage").objectReferenceValue = emptyMessage.gameObject;
            serializedController.FindProperty("backButton").objectReferenceValue = backButton;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = controllerObject;
            Debug.Log("Escena Puntuaciones construida con objetos UI editables.");
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static void CreateRow(Transform parent, int index)
        {
            GameObject rowObject = new GameObject(
                $"Row{index + 1:00}",
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement),
                typeof(HorizontalLayoutGroup),
                typeof(TetrisTakana.LeaderboardRowUI));
            rowObject.transform.SetParent(parent, false);

            Image rowImage = rowObject.GetComponent<Image>();
            rowImage.sprite = GetBuiltinSprite();
            rowImage.type = Image.Type.Sliced;
            rowImage.color = index % 2 == 0
                ? new Color(0.055f, 0.07f, 0.16f, 0.96f)
                : new Color(0.07f, 0.085f, 0.19f, 0.96f);
            rowImage.raycastTarget = false;

            LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 52f;
            rowLayout.minHeight = 52f;

            ConfigureHorizontalLayout(rowObject.GetComponent<RectTransform>(), 4f, true);

            Text rank = CreateColumnText(rowObject.transform, "Rank", "", 130f, 28, new Color(0.15f, 0.78f, 1f, 1f));
            Text score = CreateColumnText(rowObject.transform, "Score", "", 300f, 28, Color.white);
            Text lines = CreateColumnText(rowObject.transform, "Lines", "", 220f, 28, new Color(0.72f, 0.78f, 0.9f, 1f));
            Text level = CreateColumnText(rowObject.transform, "Level", "", 150f, 28, new Color(0.72f, 0.78f, 0.9f, 1f));
            Text date = CreateColumnText(rowObject.transform, "Date", "", 220f, 20, new Color(0.72f, 0.78f, 0.9f, 1f));

            LeaderboardRowUI row = rowObject.GetComponent<LeaderboardRowUI>();
            SerializedObject serializedRow = new SerializedObject(row);
            serializedRow.FindProperty("rankText").objectReferenceValue = rank;
            serializedRow.FindProperty("scoreText").objectReferenceValue = score;
            serializedRow.FindProperty("linesText").objectReferenceValue = lines;
            serializedRow.FindProperty("levelText").objectReferenceValue = level;
            serializedRow.FindProperty("dateText").objectReferenceValue = date;
            serializedRow.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CreateButton(string objectName, Transform parent, string label, Vector2 position)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            Center(rect, new Vector2(360f, 68f), position);

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = GetBuiltinSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.5f, 0.18f, 0.92f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.78f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.48f, 0.92f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            CreateText("Label", buttonObject.transform, label, 25, Color.white, Vector2.zero, new Vector2(340f, 58f));
            return button;
        }

        private static Text CreateColumnText(Transform parent, string objectName, string content, float width, int fontSize, Color color)
        {
            Text text = CreateText(objectName, parent, content, fontSize, color, Vector2.zero, new Vector2(width, 48f));
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.flexibleWidth = 0f;
            return text;
        }

        private static Text CreateText(string objectName, Transform parent, string content, int fontSize, Color color, Vector2 position, Vector2 size)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Shadow));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;

            Shadow shadow = textObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;

            RectTransform rect = text.rectTransform;
            Center(rect, size, position);
            return text;
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            image.sprite = GetBuiltinSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static void ConfigureHorizontalLayout(RectTransform rect, float spacing, bool controlHeight)
        {
            HorizontalLayoutGroup layout = rect.GetComponent<HorizontalLayoutGroup>();

            if (layout == null)
                layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = controlHeight;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(10, 10, 2, 2);
        }

        private static void ConfigureVerticalLayout(RectTransform rect, float spacing)
        {
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 2, 2);
        }

        private static RectTransform CreateLayoutRect(string objectName, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject objectInstance = new GameObject(objectName, typeof(RectTransform));
            objectInstance.transform.SetParent(parent, false);
            RectTransform rect = objectInstance.GetComponent<RectTransform>();
            Center(rect, size, position);
            return rect;
        }

        private static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddOutline(GameObject target, Color color)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(4f, -4f);
        }

        private static Sprite GetBuiltinSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }
    }
}
