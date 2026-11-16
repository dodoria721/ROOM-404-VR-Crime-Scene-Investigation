using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// VR_Test의 기존 Evidence Controller와 Canvas 컴포넌트를 보존하면서
/// Figma 41:2 / 130:2의 HUD 시각 계층만 일회성으로 재구성한다.
/// </summary>
public static class EvidenceHudFigmaApplier
{
    private const string ScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string FontPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";

    private static readonly Color HudBackground = Html("#111517", 0.94f);
    private static readonly Color ToastBackground = Html("#162230", 0.99f);
    private static readonly Color ToastBorder = Html("#526277");
    private static readonly Color Gold = Html("#C9AA55");
    private static readonly Color GoldLight = Html("#D4B878");
    private static readonly Color White = Html("#FAFAFA");
    private static readonly Color PaperWhite = Html("#F6F4EE");
    private static readonly Color Secondary = Html("#C4C9CA");
    private static readonly Color BlueGray = Html("#AFC0CB");

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode에서는 Evidence HUD를 재구성할 수 없습니다.");

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new InvalidOperationException("Noto Sans KR TMP Font Asset을 찾을 수 없습니다.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject inspectionCanvas = FindSceneObject(scene, "EvidenceInspectionCanvas");
        GameObject notificationCanvas = FindSceneObject(scene, "EvidenceNotificationCanvas");
        if (inspectionCanvas == null || notificationCanvas == null)
            throw new InvalidOperationException("VR_Test에서 Evidence Canvas를 찾을 수 없습니다.");

        EvidenceUIController inspectionController = inspectionCanvas.GetComponent<EvidenceUIController>();
        EvidenceNotificationUIController notificationController =
            notificationCanvas.GetComponent<EvidenceNotificationUIController>();
        if (inspectionController == null || notificationController == null)
            throw new InvalidOperationException("Evidence UI Controller 구성을 확인하세요.");

        ConfigureCanvas(inspectionCanvas, 1.2f, 20);
        ConfigureCanvas(notificationCanvas, 1.1f, 25);

        DestroyChildren(inspectionCanvas.transform);
        DestroyChildren(notificationCanvas.transform);

        InspectionReferences inspection = BuildInspectionHud(inspectionCanvas.transform, font);
        NotificationReferences notification = BuildNotification(notificationCanvas.transform, font);

        SerializedObject inspectionSerialized = new(inspectionController);
        SetReference(inspectionSerialized, "inspectionPanel", inspection.Panel);
        SetReference(inspectionSerialized, "evidenceIdText", inspection.IdText);
        SetReference(inspectionSerialized, "evidenceNameText", inspection.NameText);
        SetReference(inspectionSerialized, "descriptionText", inspection.DescriptionText);
        SetReference(inspectionSerialized, "requiredProgressText", inspection.RequiredProgressText);
        inspectionSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject notificationSerialized = new(notificationController);
        SetReference(notificationSerialized, "notificationPanel", notification.Panel);
        SetReference(notificationSerialized, "notificationTitleText", notification.TitleText);
        SetReference(notificationSerialized, "evidenceIdText", notification.IdText);
        SetReference(notificationSerialized, "evidenceNameText", notification.NameText);
        SetReference(notificationSerialized, "notificationMessageText", notification.MessageText);
        SetReference(notificationSerialized, "requiredProgressText", notification.RequiredProgressText);
        SetReference(notificationSerialized, "requiredProgressFill", notification.ProgressFill);
        SetBoolean(notificationSerialized, "applySafeWorldSpaceDefaults", false);
        notificationSerialized.ApplyModifiedPropertiesWithoutUndo();

        inspection.Panel.SetActive(false);
        notification.Panel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("VR_Test Scene 저장에 실패했습니다.");

        Debug.Log("[ROOM 404] Evidence Inspection HUD / Collection Notification Figma 디자인 적용 완료");
    }

    private static InspectionReferences BuildInspectionHud(Transform root, TMP_FontAsset font)
    {
        GameObject caseStatus = CreatePanel("CaseStatus", root, 64f, 64f, 228f, 100f,
            HudBackground, Color.clear);
        CreateText(caseStatus.transform, font, "CaseNumber", "CASE 404", 20f, FontStyles.Normal,
            Gold, 24f, 13f, 180f, 30f);
        CreateText(caseStatus.transform, font, "InvestigationStatus", "현장 조사 중", 28f,
            FontStyles.Normal, White, 24f, 42f, 180f, 43f);

        GameObject objective = CreateRoot("InvestigationObjective", root);
        SetTopLeft(objective.GetComponent<RectTransform>(), 64f, 340f, 360f, 119f);
        TMP_Text objectiveTitle = CreateText(objective.transform, font, "ObjectiveTitle", "현장 조사",
            26f, FontStyles.Bold, Html("#E0BF63"), 22f, 13f, 316f, 40f);
        TMP_Text objectiveLabel = CreateText(objective.transform, font, "ObjectiveLabel", "필수 증거 확보",
            26f, FontStyles.Normal, White, 22f, 59f, 220f, 43f);
        TMP_Text progress = CreateText(objective.transform, font, "RequiredProgress", "0/4",
            28f, FontStyles.Normal, White, 266f, 58f, 72f, 43f, TextAlignmentOptions.Right);
        AddShadow(objectiveTitle);
        AddShadow(objectiveLabel);
        AddShadow(progress);

        GameObject panel = CreatePanel("InspectionPanel", root, 1256f, 804f, 600f, 212f,
            HudBackground, Html("#655B3C"));
        CreateBlock("GoldAccent", panel.transform, -1f, -1f, 4f, 212f, Gold);
        CreateText(panel.transform, font, "InspectionLabel", "OBJECT INSPECTION", 20f,
            FontStyles.Normal, Gold, 28f, 18f, 260f, 34f);
        TMP_Text idText = CreateText(panel.transform, font, "EvidenceId", "E03", 20f,
            FontStyles.Normal, Html("#D8DDDD"), 510f, 18f, 62f, 34f, TextAlignmentOptions.Right);
        TMP_Text nameText = CreateText(panel.transform, font, "EvidenceName", "정하린의 개인 하드디스크",
            30f, FontStyles.Normal, White, 28f, 58f, 544f, 47f);
        TMP_Text descriptionText = CreateText(panel.transform, font, "EvidenceDescription",
            "사건의 단서가 저장된 장치", 23f, FontStyles.Normal, Secondary,
            28f, 105f, 544f, 38f);
        CreateText(panel.transform, font, "CollectHint", "Trigger · 증거 수집", 23f,
            FontStyles.Normal, Html("#D9BE6F"), 28f, 151f, 300f, 40f);

        return new InspectionReferences(panel, idText, nameText, descriptionText, progress);
    }

    private static NotificationReferences BuildNotification(Transform root, TMP_FontAsset font)
    {
        GameObject panel = CreatePanel("NotificationPanel", root, 1110f, 194f, 744f, 365f,
            ToastBackground, ToastBorder);
        CreateBlock("GoldStatusAccent", panel.transform, -1f, -1f, 7f, 365f, Html("#C9AB6E"));
        CreateBlock("HeaderDivider", panel.transform, 31f, 68f, 681f, 2f, Html("#806E4D"));
        CreateText(panel.transform, font, "EnglishStatus", "EVIDENCE COLLECTED  /  CASE 404",
            23f, FontStyles.Bold, GoldLight, 35f, 17f, 585f, 42f);

        GameObject badge = CreatePanel("CompleteBadge", panel.transform, 631f, 17f, 82f, 42f,
            Html("#25392E"), Html("#6C956D"));
        CreateText(badge.transform, font, "Check", "✓", 28f, FontStyles.Bold, Html("#BFE3B9"),
            0f, 0f, 82f, 42f, TextAlignmentOptions.Center);

        TMP_Text title = CreateText(panel.transform, font, "NotificationTitle", "증거 확보 완료",
            45f, FontStyles.Bold, PaperWhite, 35f, 79f, 665f, 66f);
        TMP_Text id = CreateText(panel.transform, font, "EvidenceId", "E03  ·", 29f,
            FontStyles.Normal, Html("#F1EDE5"), 35f, 148f, 110f, 48f);
        TMP_Text name = CreateText(panel.transform, font, "EvidenceName", "정하린의 개인 하드디스크",
            29f, FontStyles.Normal, Html("#F1EDE5"), 148f, 148f, 550f, 48f);
        TMP_Text message = CreateText(panel.transform, font, "NotificationMessage",
            "수집한 증거가 수사 기록에 기록되었습니다.", 23f, FontStyles.Normal,
            BlueGray, 35f, 195f, 665f, 42f);
        CreateBlock("ContentDivider", panel.transform, 35f, 249f, 674f, 1f, Html("#45576A"));
        CreateText(panel.transform, font, "RequiredLabel", "필수 증거 수집 현황", 24f,
            FontStyles.Normal, Html("#C3CFD7"), 35f, 258f, 475f, 47f);
        TMP_Text required = CreateText(panel.transform, font, "RequiredProgress", "0 / 4", 28f,
            FontStyles.Bold, GoldLight, 599f, 258f, 103f, 47f, TextAlignmentOptions.Right);

        CreateBlock("ProgressBackground", panel.transform, 35f, 319f, 674f, 9f, Html("#334455"));
        GameObject fillObject = CreateBlock("ProgressFill", panel.transform, 35f, 319f, 674f, 9f,
            Html("#D0AF71"));
        Image fill = fillObject.GetComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;

        return new NotificationReferences(panel, title, id, name, message, required, fill);
    }

    private static void ConfigureCanvas(GameObject canvasObject, float distance, int sortingOrder)
    {
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.localPosition = new Vector3(0f, 0f, distance);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.001f;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;
        }

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 30f;
        }
    }

    private static GameObject CreatePanel(string name, Transform parent, float x, float y,
        float width, float height, Color color, Color border)
    {
        GameObject panel = CreateBlock(name, parent, x, y, width, height, color);
        if (border.a > 0f)
        {
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }
        return panel;
    }

    private static GameObject CreateBlock(string name, Transform parent, float x, float y,
        float width, float height, Color color)
    {
        GameObject go = CreateRoot(name, parent);
        SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        return go;
    }

    private static TMP_Text CreateText(Transform parent, TMP_FontAsset font, string name, string value,
        float fontSize, FontStyles style, Color color, float x, float y, float width, float height,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        GameObject go = CreateRoot(name, parent);
        SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
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

    private static void AddShadow(TMP_Text text)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;
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

    private static void SetReference(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"직렬화 필드를 찾을 수 없습니다: {propertyName}");
        property.objectReferenceValue = value;
    }

    private static void SetBoolean(SerializedObject target, string propertyName, bool value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"직렬화 필드를 찾을 수 없습니다: {propertyName}");
        property.boolValue = value;
    }

    private static void DestroyChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
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

    private readonly struct InspectionReferences
    {
        public InspectionReferences(GameObject panel, TMP_Text idText, TMP_Text nameText,
            TMP_Text descriptionText, TMP_Text requiredProgressText)
        {
            Panel = panel;
            IdText = idText;
            NameText = nameText;
            DescriptionText = descriptionText;
            RequiredProgressText = requiredProgressText;
        }

        public GameObject Panel { get; }
        public TMP_Text IdText { get; }
        public TMP_Text NameText { get; }
        public TMP_Text DescriptionText { get; }
        public TMP_Text RequiredProgressText { get; }
    }

    private readonly struct NotificationReferences
    {
        public NotificationReferences(GameObject panel, TMP_Text titleText, TMP_Text idText,
            TMP_Text nameText, TMP_Text messageText, TMP_Text requiredProgressText, Image progressFill)
        {
            Panel = panel;
            TitleText = titleText;
            IdText = idText;
            NameText = nameText;
            MessageText = messageText;
            RequiredProgressText = requiredProgressText;
            ProgressFill = progressFill;
        }

        public GameObject Panel { get; }
        public TMP_Text TitleText { get; }
        public TMP_Text IdText { get; }
        public TMP_Text NameText { get; }
        public TMP_Text MessageText { get; }
        public TMP_Text RequiredProgressText { get; }
        public Image ProgressFill { get; }
    }
}
