using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 기존 DeductionCanvas와 최종 추리 로직을 보존하고 Figma 131:2 해금 모달을
/// 별도의 카메라 부착 World Space Canvas로 구성한다.
/// </summary>
public static class DeductionUnlockFigmaApplier
{
    private const string ScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string FontPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";

    private static readonly Color ModalBackground = Html("#162230");
    private static readonly Color ModalBorder = Html("#67788A");
    private static readonly Color Gold = Html("#CBB070");
    private static readonly Color GoldText = Html("#D9BD80");
    private static readonly Color White = Html("#F5F1E9");
    private static readonly Color Secondary = Html("#D5E0E7");
    private static readonly Color Muted = Html("#9FB1BD");

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode에서는 최종 추리 해금 UI를 재구성할 수 없습니다.");

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new InvalidOperationException("Noto Sans KR TMP Font Asset을 찾을 수 없습니다.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject deductionCanvas = FindSceneObject(scene, "DeductionCanvas");
        GameObject mainCameraObject = FindSceneObject(scene, "Main Camera");
        if (deductionCanvas == null || mainCameraObject == null)
            throw new InvalidOperationException("DeductionCanvas 또는 Main Camera를 찾을 수 없습니다.");

        DeductionStartUIController controller = deductionCanvas.GetComponent<DeductionStartUIController>();
        if (controller == null)
            throw new InvalidOperationException("DeductionStartUIController를 찾을 수 없습니다.");

        GameObject oldStartArea = FindSceneObject(scene, "DeductionStartArea");
        if (oldStartArea != null)
            Object.DestroyImmediate(oldStartArea);

        GameObject previousUnlockCanvas = FindSceneObject(scene, "DeductionUnlockCanvas");
        if (previousUnlockCanvas != null)
            Object.DestroyImmediate(previousUnlockCanvas);

        GameObject unlockCanvas = CreateCanvas(mainCameraObject.transform,
            mainCameraObject.GetComponent<Camera>());
        GameObject promptRoot = CreateRoot("DeductionUnlockPrompt", unlockCanvas.transform);
        Stretch(promptRoot.GetComponent<RectTransform>());

        Image dim = promptRoot.AddComponent<Image>();
        dim.color = Html("#080F1A", 0.76f);
        dim.raycastTarget = true;

        GameObject modal = CreatePanel("UnlockModal", promptRoot.transform,
            425f, 218f, 1070f, 640f, ModalBackground, ModalBorder, true);
        CreateBlock("TopGoldAccent", modal.transform, -2f, -2f, 1070f, 8f, Gold);
        CreateText(modal.transform, font, "EnglishHeader",
            "INVESTIGATION UPDATE   /   CASE 404", 25f, FontStyles.Bold,
            GoldText, 64f, 31f, 900f, 48f);
        CreateBlock("HeaderDivider", modal.transform, 62f, 98f, 941f, 2f, Html("#4A596B"));

        GameObject iconBackground = CreatePanel("UnlockIconBackground", modal.transform,
            449f, 125f, 170f, 112f, Html("#233A43"), Html("#758A73"));
        CreateText(iconBackground.transform, font, "UnlockCheck", "✓", 75f, FontStyles.Bold,
            Html("#D4C48C"), 0f, -3f, 170f, 117f, TextAlignmentOptions.Center);

        CreateText(modal.transform, font, "UnlockTitle", "최종 추리 활성화", 58f,
            FontStyles.Bold, White, 55f, 248f, 960f, 88f, TextAlignmentOptions.Center);
        CreateText(modal.transform, font, "CompletionMessage", "필수 증거 4개를 모두 확보했습니다.",
            31f, FontStyles.Normal, Secondary, 80f, 338f, 910f, 64f,
            TextAlignmentOptions.Center);
        CreateText(modal.transform, font, "Instruction",
            "용의자들의 진술과 현장 증거를 대조하여 사건의 결론을 내리십시오.",
            25f, FontStyles.Normal, Muted, 85f, 398f, 900f, 58f,
            TextAlignmentOptions.Center);

        GameObject completionBadge = CreatePanel("RequiredEvidenceBadge", modal.transform,
            300f, 473f, 470f, 45f, Html("#2C392C"), Html("#9A8A61"));
        CreateText(completionBadge.transform, font, "BadgeLabel", "✓  필수 증거", 25f,
            FontStyles.Bold, Html("#D7BF80"), 22f, 3f, 270f, 39f,
            TextAlignmentOptions.Center);
        TMP_Text progress = CreateText(completionBadge.transform, font, "RequiredProgress",
            "4 / 4", 25f, FontStyles.Bold, Html("#D7BF80"), 292f, 3f, 150f, 39f,
            TextAlignmentOptions.Center);

        Button startButton = CreateButton("StartDeductionButton", modal.transform, font,
            "최종 추리 시작", 119f, 544f, 395f, 77f,
            Html("#CEB177"), Html("#152233"), Html("#E0CA95"));
        Button continueButton = CreateButton("ContinueInvestigationButton", modal.transform, font,
            "계속 조사", 550f, 544f, 395f, 77f,
            Html("#263646"), Html("#F3F1EE"), Html("#627386"));

        SerializedObject serializedController = new(controller);
        SetReference(serializedController, "startAreaRoot", promptRoot);
        SetReference(serializedController, "deductionStartButton", startButton);
        SetReference(serializedController, "continueInvestigationButton", continueButton);
        SetReference(serializedController, "guidanceText", null);
        SetReference(serializedController, "requiredEvidenceProgressText", progress);
        SerializedProperty indicators = serializedController.FindProperty("requiredEvidenceIndicators");
        if (indicators != null)
            indicators.arraySize = 0;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        promptRoot.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("VR_Test Scene 저장에 실패했습니다.");

        Debug.Log("[ROOM 404] 최종 추리 해금 Figma UI 적용 완료");
    }

    private static GameObject CreateCanvas(Transform parent, Camera worldCamera)
    {
        GameObject go = new("DeductionUnlockCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
        go.layer = 5;
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.localPosition = new Vector3(0f, 0f, 1.15f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.001f;

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = worldCamera;
        canvas.sortingOrder = 30;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 30f;
        return go;
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font,
        string label, float x, float y, float width, float height,
        Color background, Color textColor, Color border)
    {
        GameObject go = CreatePanel(name, parent, x, y, width, height, background, border, true);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.78f, 1f);
        colors.pressedColor = new Color(0.72f, 0.66f, 0.52f, 1f);
        button.colors = colors;
        CreateText(go.transform, font, "Label", label, 29f, FontStyles.Bold, textColor,
            20f, 0f, width - 40f, height, TextAlignmentOptions.Center);
        return button;
    }

    private static GameObject CreatePanel(string name, Transform parent, float x, float y,
        float width, float height, Color color, Color border, bool raycast = false)
    {
        GameObject go = CreateBlock(name, parent, x, y, width, height, color, raycast);
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
        return go;
    }

    private static GameObject CreateBlock(string name, Transform parent, float x, float y,
        float width, float height, Color color, bool raycast = false)
    {
        GameObject go = CreateRoot(name, parent);
        SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        return go;
    }

    private static TMP_Text CreateText(Transform parent, TMP_FontAsset font, string name,
        string value, float size, FontStyles style, Color color,
        float x, float y, float width, float height,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        GameObject go = CreateRoot(name, parent);
        SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
        return text;
    }

    private static GameObject CreateRoot(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetReference(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"직렬화 필드를 찾을 수 없습니다: {propertyName}");
        property.objectReferenceValue = value;
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == name)
                    return item.gameObject;
            }
        }
        return null;
    }

    private static Color Html(string value, float alpha = 1f)
    {
        if (!ColorUtility.TryParseHtmlString(value, out Color color))
            return Color.magenta;
        color.a = alpha;
        return color;
    }
}
