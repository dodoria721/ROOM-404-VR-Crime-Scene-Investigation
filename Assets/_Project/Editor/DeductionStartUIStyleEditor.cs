using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DeductionStartUIStyleEditor
{
    private const string MenuPath = "ROOM 404/UI/Apply Deduction Start UI";
    private const string VrTestScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string FontAssetPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";

    private static readonly Color PanelColor = Html("#172736", 0.96f);
    private static readonly Color HeaderColor = Html("#F3F7F8");
    private static readonly Color SecondaryTextColor = Html("#B8C8D0");
    private static readonly Color AccentColor = Html("#DCC48A");
    private static readonly Color ActiveButtonColor = Html("#327C78");
    private static readonly Color HoverButtonColor = Html("#42A69B");
    private static readonly Color PressedButtonColor = Html("#286763");
    private static readonly Color DisabledButtonColor = Html("#475663", 0.88f);
    private static readonly Color MissingSegmentColor = Html("#475663");

    [MenuItem(MenuPath)]
    private static void ApplyStyle()
    {
        if (!CanRunInCurrentScene(showMessage: true))
            return;

        Scene scene = SceneManager.GetActiveScene();
        GameObject canvasObject = FindGameObjectInScene(scene, "DeductionCanvas");
        if (canvasObject == null)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 UI",
                "현재 Scene에서 DeductionCanvas를 찾을 수 없습니다.",
                "확인");
            return;
        }

        DeductionUIController deductionUIController =
            canvasObject.GetComponent<DeductionUIController>();
        if (deductionUIController == null)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 UI",
                "DeductionCanvas에서 기존 DeductionUIController를 찾을 수 없습니다.",
                "확인");
            return;
        }

        TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (koreanFont == null)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 UI",
                $"Noto Sans KR TMP Font Asset을 찾을 수 없습니다.\n{FontAssetPath}",
                "확인");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Deduction Start UI Style");

        GameObject startArea = GetOrCreateUIObject(canvasObject.transform, "DeductionStartArea");
        ConfigureStartArea(startArea);

        TMP_Text headerText = GetOrCreateText(startArea.transform, "RequiredEvidenceLabel", koreanFont);
        ConfigureText(
            headerText,
            "필수 증거",
            25f,
            FontStyles.Bold,
            HeaderColor,
            TextAlignmentOptions.MidlineLeft,
            new Vector2(30f, -28f),
            new Vector2(220f, 40f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));

        TMP_Text progressText = GetOrCreateText(
            startArea.transform,
            "RequiredEvidenceProgressText",
            koreanFont);
        ConfigureText(
            progressText,
            "0 / 4",
            27f,
            FontStyles.Bold,
            AccentColor,
            TextAlignmentOptions.MidlineRight,
            new Vector2(-30f, -28f),
            new Vector2(120f, 40f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f));

        GameObject progressTrack = GetOrCreateUIObject(startArea.transform, "EvidenceProgressSegments");
        ConfigureRect(
            progressTrack.GetComponent<RectTransform>(),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(30f, -78f),
            new Vector2(360f, 12f));
        SetGraphicRaycast(progressTrack, false);

        Image[] indicators = new Image[4];
        const float segmentWidth = 81f;
        const float segmentGap = 12f;
        for (int i = 0; i < indicators.Length; i++)
        {
            GameObject segment = GetOrCreateUIObject(
                progressTrack.transform,
                $"EvidenceSegment_{i + 1:00}");
            ConfigureRect(
                segment.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(i * (segmentWidth + segmentGap), 0f),
                new Vector2(segmentWidth, 10f));
            indicators[i] = ConfigureImage(segment, MissingSegmentColor, raycastTarget: false);
        }

        GameObject buttonObject = GetOrCreateUIObject(startArea.transform, "DeductionStartButton");
        ConfigureRect(
            buttonObject.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -116f),
            new Vector2(360f, 82f));
        // Color Tint가 기준색에 곱해지므로 Target Graphic은 흰색으로 유지한다.
        Image buttonImage = ConfigureImage(buttonObject, Color.white, raycastTarget: true);
        Button startButton = GetOrAddComponent<Button>(buttonObject);
        ConfigureButton(startButton, buttonImage);

        TMP_Text buttonText = GetOrCreateText(buttonObject.transform, "Label", koreanFont);
        ConfigureStretchText(
            buttonText,
            "사건 추리 시작",
            29f,
            FontStyles.Bold,
            HeaderColor,
            TextAlignmentOptions.Center);

        TMP_Text guidanceText = GetOrCreateText(startArea.transform, "GuidanceText", koreanFont);
        ConfigureText(
            guidanceText,
            "필수 증거를 모두 확보해야 합니다.",
            20f,
            FontStyles.Normal,
            SecondaryTextColor,
            TextAlignmentOptions.Center,
            new Vector2(0f, 24f),
            new Vector2(360f, 36f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f));

        DeductionStartUIController startController =
            canvasObject.GetComponent<DeductionStartUIController>();
        if (startController == null)
            startController = Undo.AddComponent<DeductionStartUIController>(canvasObject);

        ResultController resultController = ResolveResultController(scene, deductionUIController);
        ConnectController(
            startController,
            startArea,
            startButton,
            guidanceText,
            progressText,
            indicators,
            deductionUIController,
            resultController);

        SetLayerRecursively(startArea, LayerMask.NameToLayer("UI"));
        Undo.RecordObject(startArea.transform, "Order deduction start area");
        startArea.transform.SetAsLastSibling();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = startArea;

        string resultReferenceMessage = resultController != null
            ? "기존 ResultController 참조도 연결했습니다."
            : "ResultController를 자동으로 찾지 못했습니다. Inspector에서 수동 연결하세요.";

        Debug.Log(
            $"[ROOM 404] DeductionStartArea UI를 적용했습니다. {resultReferenceMessage} " +
            "Scene은 자동 저장되지 않았습니다.",
            startArea);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateApplyStyle()
    {
        return CanRunInCurrentScene(showMessage: false);
    }

    private static void ConfigureStartArea(GameObject startArea)
    {
        ConfigureRect(
            startArea.GetComponent<RectTransform>(),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-34f, 34f),
            new Vector2(420f, 300f));

        Image background = ConfigureImage(startArea, PanelColor, raycastTarget: false);
        background.type = Image.Type.Sliced;

        Shadow shadow = GetOrAddComponent<Shadow>(startArea);
        Undo.RecordObject(shadow, "Style deduction start shadow");
        shadow.effectColor = new Color(0f, 0f, 0f, 0.48f);
        shadow.effectDistance = new Vector2(7f, -7f);
        shadow.useGraphicAlpha = true;

        Outline outline = GetOrAddComponent<Outline>(startArea);
        Undo.RecordObject(outline, "Style deduction start outline");
        outline.effectColor = new Color(0.26f, 0.65f, 0.61f, 0.32f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
    }

    private static void ConfigureButton(Button button, Image targetImage)
    {
        Undo.RecordObject(button, "Style deduction start button");
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = targetImage;

        ColorBlock colors = button.colors;
        colors.normalColor = ActiveButtonColor;
        colors.highlightedColor = HoverButtonColor;
        colors.pressedColor = PressedButtonColor;
        colors.selectedColor = HoverButtonColor;
        colors.disabledColor = DisabledButtonColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private static Image ConfigureImage(GameObject gameObject, Color color, bool raycastTarget)
    {
        Image image = GetOrAddComponent<Image>(gameObject);
        Undo.RecordObject(image, $"Style {gameObject.name}");
        image.color = color;
        image.raycastTarget = raycastTarget;

        Sprite roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    private static void ConnectController(
        DeductionStartUIController controller,
        GameObject startArea,
        Button startButton,
        TMP_Text guidanceText,
        TMP_Text progressText,
        Image[] indicators,
        DeductionUIController deductionUIController,
        ResultController resultController)
    {
        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("startAreaRoot").objectReferenceValue = startArea;
        serializedController.FindProperty("deductionStartButton").objectReferenceValue = startButton;
        serializedController.FindProperty("guidanceText").objectReferenceValue = guidanceText;
        serializedController.FindProperty("requiredEvidenceProgressText").objectReferenceValue = progressText;
        serializedController.FindProperty("deductionUIController").objectReferenceValue = deductionUIController;
        serializedController.FindProperty("resultController").objectReferenceValue = resultController;
        serializedController.FindProperty("requiredEvidenceTotal").intValue = 4;

        SerializedProperty indicatorProperty =
            serializedController.FindProperty("requiredEvidenceIndicators");
        indicatorProperty.arraySize = indicators.Length;
        for (int i = 0; i < indicators.Length; i++)
            indicatorProperty.GetArrayElementAtIndex(i).objectReferenceValue = indicators[i];

        serializedController.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
    }

    private static ResultController ResolveResultController(
        Scene scene,
        DeductionUIController deductionUIController)
    {
        var serializedDeductionUI = new SerializedObject(deductionUIController);
        ResultController referencedResult = serializedDeductionUI
            .FindProperty("resultController")
            .objectReferenceValue as ResultController;
        if (referencedResult != null)
            return referencedResult;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ResultController result = root.GetComponentInChildren<ResultController>(true);
            if (result != null)
                return result;
        }

        return null;
    }

    private static TMP_Text GetOrCreateText(
        Transform parent,
        string objectName,
        TMP_FontAsset font)
    {
        GameObject textObject = GetOrCreateUIObject(parent, objectName);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (text == null)
            text = Undo.AddComponent<TextMeshProUGUI>(textObject);

        Undo.RecordObject(text, $"Configure {objectName}");
        text.font = font;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void ConfigureText(
        TMP_Text text,
        string value,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 anchoredPosition,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot)
    {
        Undo.RecordObject(text, $"Style {text.name}");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;

        ConfigureRect(
            text.rectTransform,
            anchorMin,
            anchorMax,
            pivot,
            anchoredPosition,
            size);
    }

    private static void ConfigureStretchText(
        TMP_Text text,
        string value,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment)
    {
        Undo.RecordObject(text, $"Style {text.name}");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        Undo.RecordObject(rect, $"Layout {text.name}");
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(18f, 8f);
        rect.offsetMax = new Vector2(-18f, -8f);
        rect.anchoredPosition3D = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        Undo.RecordObject(rect, $"Layout {rect.name}");
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static GameObject GetOrCreateUIObject(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.gameObject;

        var gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {objectName}");
        Undo.SetTransformParent(gameObject.transform, parent, $"Parent {objectName}");
        return gameObject;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(gameObject);
    }

    private static void SetGraphicRaycast(GameObject gameObject, bool raycastTarget)
    {
        Graphic graphic = gameObject.GetComponent<Graphic>();
        if (graphic == null)
            return;

        Undo.RecordObject(graphic, $"Set raycast for {gameObject.name}");
        graphic.raycastTarget = raycastTarget;
    }

    private static bool CanRunInCurrentScene(bool showMessage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (showMessage)
            {
                EditorUtility.DisplayDialog(
                    "ROOM 404 UI",
                    "Play Mode에서는 추리 시작 UI를 적용할 수 없습니다.",
                    "확인");
            }

            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        bool valid = scene.IsValid()
            && scene.isLoaded
            && string.Equals(scene.path, VrTestScenePath, StringComparison.Ordinal);

        if (!valid && showMessage)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 UI",
                $"이 메뉴는 '{VrTestScenePath}' Scene에서만 실행할 수 있습니다.",
                "확인");
        }

        return valid;
    }

    private static GameObject FindGameObjectInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(child.name, objectName, StringComparison.Ordinal))
                    return child.gameObject;
            }
        }

        return null;
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        if (layer < 0)
            return;

        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static Color Html(string html, float alpha = 1f)
    {
        if (!ColorUtility.TryParseHtmlString(html, out Color color))
            color = Color.white;

        color.a = alpha;
        return color;
    }
}
