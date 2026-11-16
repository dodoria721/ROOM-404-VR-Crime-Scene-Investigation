using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// VR_Test의 기존 EndingCanvas와 런타임 컴포넌트를 유지한 채
/// Figma 123:2 / 123:166 디자인의 시각 자식만 다시 구성한다.
/// </summary>
public static class EndingUIFigmaApplier
{
    private const string ScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string FontPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";
    private const string PortraitPath = "Assets/_Project/Art/UI/Ending/LeeSeoYeon.png";

    private static readonly Color Background = Html("#101721");
    private static readonly Color Grid = Html("#2C3746", 0.22f);
    private static readonly Color Panel = Html("#182330");
    private static readonly Color InnerPanel = Html("#131D29");
    private static readonly Color Footer = Html("#202D3B");
    private static readonly Color Border = Html("#435264");
    private static readonly Color White = Html("#F4F0E9");
    private static readonly Color Secondary = Html("#A3B0BC");
    private static readonly Color Gold = Html("#CBAE74");
    private static readonly Color Failure = Html("#DA746E");
    private static readonly Color Warning = Html("#4A272D");

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode에서는 Ending UI를 재구성할 수 없습니다.");

        ConfigurePortrait();

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
        if (font == null || portrait == null)
            throw new InvalidOperationException("Ending UI에 필요한 Font 또는 Portrait를 불러오지 못했습니다.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject canvasObject = FindSceneObject(scene, "EndingCanvas");
        if (canvasObject == null)
            throw new InvalidOperationException("VR_Test에서 EndingCanvas를 찾을 수 없습니다.");

        EndingUIController controller = canvasObject.GetComponent<EndingUIController>();
        SceneFlowController flow = canvasObject.GetComponent<SceneFlowController>();
        if (controller == null || flow == null)
            throw new InvalidOperationException("EndingCanvas의 EndingUIController/SceneFlowController를 확인하세요.");

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 30f;
        }

        GameObject endingRoot = FindChild(canvasObject.transform, "EndingRoot");
        endingRoot.SetActive(true);
        DestroyChildren(endingRoot.transform);
        Stretch(endingRoot.GetComponent<RectTransform>());

        CreateBackdrop(endingRoot.transform);
        GameObject successPanel = CreateRoot("SuccessPanel", endingRoot.transform);
        GameObject failurePanel = CreateRoot("FailurePanel", endingRoot.transform);

        SuccessReferences success = BuildSuccess(successPanel.transform, font, portrait);
        FailureReferences failure = BuildFailure(failurePanel.transform, font);

        SerializedObject serializedController = new(controller);
        SetReference(serializedController, "sceneFlowController", flow);
        SetReference(serializedController, "endingRoot", endingRoot);
        SetReference(serializedController, "successPanel", successPanel);
        SetReference(serializedController, "failurePanel", failurePanel);
        SetReference(serializedController, "caseRecordPanel", success.CaseRecordPanel);
        SetReference(serializedController, "caseRecordButton", success.CaseRecordButton);
        SetReference(serializedController, "retryButton", failure.RetryButton);
        SetReference(serializedController, "successMainMenuButton", success.MainMenuButton);
        SetReference(serializedController, "failureMainMenuButton", failure.MainMenuButton);
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        success.CaseRecordPanel.SetActive(false);
        successPanel.SetActive(false);
        failurePanel.SetActive(false);
        endingRoot.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("VR_Test Scene 저장에 실패했습니다.");

        AssetDatabase.Refresh();
        Debug.Log("[ROOM 404] EndingCanvas Figma 성공/실패 디자인 적용 완료");
    }

    private static SuccessReferences BuildSuccess(Transform root, TMP_FontAsset font, Sprite portrait)
    {
        CreateHeader(root, font, true);
        CreateText(root, font, "SectionHeader", "INVESTIGATION RESULT   /   사건 기록 404",
            24f, FontStyles.Normal, Secondary, new Vector2(51f, 216f), new Vector2(930f, 34f));

        GameObject report = CreatePanel("FinalReportPanel", root, 47f, 268f, 1140f, 520f, Panel, Border);
        CreateBlock("GoldAccent", report.transform, 0f, 0f, 6f, 520f, Gold);
        CreateText(report.transform, font, "ClosedLabel", "✓   수사 완료  ·  INVESTIGATION CLOSED",
            25f, FontStyles.Bold, Gold, new Vector2(35f, 24f), new Vector2(850f, 40f));
        CreateText(report.transform, font, "TruthTitle", "정하린 사망 사건의 진실",
            47f, FontStyles.Bold, White, new Vector2(35f, 77f), new Vector2(1050f, 66f));
        CreateText(report.transform, font, "TruthDescription",
            "과거 사고의 진실을 둘러싼 대립 끝에 발생한 우발적 사망.\n이서연은 사건 직후 신고 대신 현장 은폐를 선택했습니다.",
            27f, FontStyles.Normal, Secondary, new Vector2(35f, 137f), new Vector2(1040f, 72f), true);
        CreateBlock("ChainDivider", report.transform, 35f, 217f, 1064f, 1f, Html("#344353"));
        CreateText(report.transform, font, "ChainTitle", "사건 재구성   /   CHAIN OF EVENTS",
            26f, FontStyles.Bold, Gold, new Vector2(35f, 228f), new Vector2(860f, 38f));

        CreateEventCard(report.transform, font, 35f, 274f, "01", "최후의 대면", "이서연이 정하린과 연구실에서 대면");
        CreateEventCard(report.transform, font, 573f, 274f, "02", "격렬한 몸싸움", "두 사람 사이에 물리적 충돌 발생");
        CreateEventCard(report.transform, font, 35f, 391f, "03", "치명적인 충격", "책상 모서리 충돌로 두부 손상");
        CreateEventCard(report.transform, font, 573f, 391f, "04", "현장 은폐", "자살로 오인하도록 현장 정황을 조작");

        GameObject culprit = CreatePanel("CulpritCard", root, 1212f, 268f, 661f, 520f, Panel, Border);
        CreateText(culprit.transform, font, "ClosedCaseLabel", "CLOSED CASE   /   용의자 B",
            24f, FontStyles.Bold, Gold, new Vector2(25f, 20f), new Vector2(590f, 36f));
        GameObject portraitObject = CreateBlock("LeeSeoYeonPortrait", culprit.transform, 25f, 73f, 250f, 252f, Color.white);
        Image portraitImage = portraitObject.GetComponent<Image>();
        portraitImage.sprite = portrait;
        portraitImage.preserveAspect = false;
        AddOutline(portraitObject, Gold, 1f);
        CreateText(culprit.transform, font, "ConfirmationLabel", "범인 확인",
            25f, FontStyles.Normal, Secondary, new Vector2(300f, 82f), new Vector2(310f, 36f));
        CreateText(culprit.transform, font, "CulpritName", "이서연",
            53f, FontStyles.Bold, White, new Vector2(299f, 121f), new Vector2(325f, 74f));
        CreateText(culprit.transform, font, "CulpritRole", "용의자 B  /  약학 연구원",
            22f, FontStyles.Normal, Gold, new Vector2(300f, 210f), new Vector2(310f, 36f));
        CreateBlock("VerdictDivider", culprit.transform, 25f, 356f, 608f, 1f, Html("#344353"));
        CreateInfoRow(culprit.transform, font, 376f, "사건 유형", "타인의 개입", White);
        CreateBlock("TypeDivider", culprit.transform, 23f, 427f, 613f, 1f, Html("#344353"));
        CreateInfoRow(culprit.transform, font, 434f, "필수 증거", "4 / 4   확보", Gold);
        CreateBlock("EvidenceDivider", culprit.transform, 23f, 485f, 613f, 1f, Html("#344353"));

        CreateFooterNotice(root, font, Gold,
            "사건은 해결되었지만, 은폐된 연구 사고의 책임은 여전히 남아 있습니다.");
        Button recordButton = CreateButton("CaseRecordButton", root, font, "사건 기록 보기",
            428f, 946f, 490f, 84f, Gold, Html("#101721"), Gold);
        Button mainButton = CreateButton("SuccessMainMenuButton", root, font, "메인 메뉴",
            948f, 946f, 490f, 84f, Footer, White, Html("#677888"));

        GameObject caseRecord = BuildCaseRecordOverlay(root, font);
        return new SuccessReferences(recordButton, mainButton, caseRecord);
    }

    private static FailureReferences BuildFailure(Transform root, TMP_FontAsset font)
    {
        CreateHeader(root, font, false);
        CreateText(root, font, "SectionHeader", "INVESTIGATION RESULT   /   사건 기록 404",
            24f, FontStyles.Normal, Secondary, new Vector2(51f, 216f), new Vector2(930f, 34f));

        GameObject review = CreatePanel("ReviewPanel", root, 47f, 268f, 1140f, 520f, Panel, Border);
        CreateBlock("FailureAccent", review.transform, 0f, 0f, 6f, 520f, Failure);
        CreateText(review.transform, font, "MismatchLabel", "!   추리 불일치   /   DEDUCTION MISMATCH",
            27f, FontStyles.Bold, Failure, new Vector2(36f, 26f), new Vector2(1020f, 40f));
        CreateText(review.transform, font, "FailureHeadline", "아직, 진실에 도달하지 못했습니다.",
            45f, FontStyles.Bold, White, new Vector2(36f, 90f), new Vector2(1020f, 62f));
        CreateText(review.transform, font, "FailureDescription",
            "사건의 유형 또는 지목한 용의자가 현장 기록과 맞지 않습니다.\n확보한 증거를 다시 검토하고, 진술의 모순을 찾아보세요.",
            26f, FontStyles.Normal, Secondary, new Vector2(36f, 162f), new Vector2(1020f, 70f), true);
        CreateBlock("ReviewDivider", review.transform, 36f, 276f, 1065f, 1f, Html("#344353"));
        CreateText(review.transform, font, "ReviewTitle", "다시 살펴볼 세 가지 쟁점",
            28f, FontStyles.Bold, White, new Vector2(36f, 293f), new Vector2(980f, 42f));
        CreateReviewRow(review.transform, font, 355f, "01", "사망 원인", "자살을 시사하는 흔적만으로 충분한가?");
        CreateReviewRow(review.transform, font, 406f, "02", "현장 정황", "책상 모서리의 충격 흔적은 무엇을 의미하는가?");
        CreateReviewRow(review.transform, font, 457f, "03", "용의자 진술", "사건 당일 동선과 알리바이가 일치하는가?");

        GameObject guide = CreatePanel("ReinvestigationGuide", root, 1212f, 268f, 661f, 520f, Panel, Border);
        CreateText(guide.transform, font, "StatusLabel", "CASE STATUS   /   보류",
            24f, FontStyles.Bold, Failure, new Vector2(28f, 24f), new Vector2(585f, 36f));
        CreatePanel("RejectedBox", guide.transform, 26f, 89f, 604f, 94f, Warning, Failure);
        CreateText(guide.transform, font, "RejectedText", "결론 제출이 반려되었습니다",
            31f, FontStyles.Bold, White, new Vector2(46f, 109f), new Vector2(559f, 45f));
        CreateText(guide.transform, font, "GuideTitle", "재수사 가이드",
            30f, FontStyles.Bold, Gold, new Vector2(28f, 213f), new Vector2(550f, 44f));
        CreateBlock("GuideDivider", guide.transform, 28f, 273f, 597f, 1f, Html("#344353"));
        CreateText(guide.transform, font, "GuideText",
            "1.  책상 모서리의 충돌 흔적 확인\n\n2.  용의자들의 알리바이 재검토\n\n3.  사건 전후의 기록 비교",
            26f, FontStyles.Normal, White, new Vector2(32f, 300f), new Vector2(570f, 165f), true);

        CreateFooterNotice(root, font, Failure,
            "재수사를 시작하면 증거 수집 상태가 초기화됩니다. 범인의 정체는 아직 공개되지 않았습니다.");
        Button retryButton = CreateButton("RetryButton", root, font, "재수사",
            428f, 946f, 490f, 84f, Warning, White, Failure);
        Button mainButton = CreateButton("FailureMainMenuButton", root, font, "메인 메뉴",
            948f, 946f, 490f, 84f, Footer, White, Html("#677888"));
        return new FailureReferences(retryButton, mainButton);
    }

    private static void CreateBackdrop(Transform root)
    {
        CreateBlock("EndingBackdrop", root, 0f, 0f, 1920f, 1080f, Background);
        for (int x = 46; x <= 1810; x += 84)
            CreateBlock($"GridV_{x}", root, x, 0f, 1f, 1080f, Grid);
        for (int y = 36; y <= 1044; y += 84)
            CreateBlock($"GridH_{y}", root, 0f, y, 1920f, 1f, Grid);
    }

    private static void CreateHeader(Transform root, TMP_FontAsset font, bool success)
    {
        string english = success ? "ROOM 404   /   CASE RESOLVED" : "ROOM 404   /   INVESTIGATION INCOMPLETE";
        string title = success ? "사건 해결" : "수사 실패";
        string description = success
            ? "수집한 단서와 진술을 종합하여 사건의 진실을 밝혀냈습니다."
            : "제출한 결론이 수집된 증거와 일치하지 않습니다.";

        CreateBlock("HeaderSlashGold", root, 44f, 27f, 31f, 11f, Html("#C5A95F"));
        CreateBlock("HeaderSlashGray", root, 81f, 27f, 14f, 11f, Html("#66727F"));
        CreateText(root, font, "EnglishHeader", english, 25f, FontStyles.Normal,
            Html("#D4C6A4"), new Vector2(110f, 14f), new Vector2(880f, 55f));
        CreateBlock("HeaderAccent", root, 45f, 90f, 7f, 54f, Html("#C8AA64"));
        CreateText(root, font, "EndingTitle", title, 64f, FontStyles.Bold,
            White, new Vector2(80f, 73f), new Vector2(550f, 98f));
        CreateBlock("TitleDivider", root, 490f, 84f, 2f, 56f, Html("#5D6974"));
        CreateText(root, font, "EndingDescription", description, 28f, FontStyles.Normal,
            Html("#D6D9DB"), new Vector2(520f, 82f), new Vector2(1000f, 70f));
        CreateText(root, font, "RoomLogo", "ROOM 404", 40f, FontStyles.Bold,
            Html("#636C78"), new Vector2(1590f, 20f), new Vector2(270f, 55f), false, TextAlignmentOptions.Right);
        CreateText(root, font, "RoomLogoSub", "VR CRIME INVESTIGATION", 13f, FontStyles.Normal,
            Html("#717E8D"), new Vector2(1586f, 78f), new Vector2(271f, 25f), false, TextAlignmentOptions.Right);
        CreateBlock("HeaderBottomDivider", root, 42f, 168f, 1836f, 2f, Html("#455365"));
    }

    private static void CreateEventCard(Transform parent, TMP_FontAsset font, float x, float y,
        string number, string title, string description)
    {
        GameObject card = CreatePanel($"Event_{number}", parent, x, y, 530f, 112f, InnerPanel, Html("#344353"));
        CreateBlock("Accent", card.transform, 0f, 0f, 4f, 112f, Gold);
        CreateText(card.transform, font, "Number", number, 26f, FontStyles.Bold, Gold,
            new Vector2(21f, 12f), new Vector2(66f, 36f));
        CreateText(card.transform, font, "Title", title, 28f, FontStyles.Bold, White,
            new Vector2(91f, 10f), new Vector2(415f, 40f));
        CreateText(card.transform, font, "Description", description, 21f, FontStyles.Normal, Secondary,
            new Vector2(23f, 57f), new Vector2(476f, 34f));
    }

    private static void CreateInfoRow(Transform parent, TMP_FontAsset font, float y,
        string label, string value, Color valueColor)
    {
        CreateText(parent, font, label + "Label", label, 24f, FontStyles.Normal, Secondary,
            new Vector2(23f, y), new Vector2(204f, 36f));
        CreateText(parent, font, label + "Value", value, 27f, FontStyles.Normal, valueColor,
            new Vector2(243f, y - 1f), new Vector2(391f, 40f));
    }

    private static void CreateReviewRow(Transform parent, TMP_FontAsset font, float y,
        string number, string title, string description)
    {
        CreateText(parent, font, "Number" + number, number, 23f, FontStyles.Bold, Failure,
            new Vector2(41f, y), new Vector2(75f, 30f));
        CreateText(parent, font, "Issue" + number, title, 25f, FontStyles.Bold, White,
            new Vector2(119f, y), new Vector2(220f, 34f));
        CreateText(parent, font, "IssueDescription" + number, description, 23f, FontStyles.Normal, Secondary,
            new Vector2(349f, y), new Vector2(720f, 32f));
        if (number != "03")
            CreateBlock("Divider" + number, parent, 41f, y + 43f, 1025f, 1f, Html("#344353"));
    }

    private static void CreateFooterNotice(Transform root, TMP_FontAsset font, Color accent, string message)
    {
        CreatePanel("FooterNotice", root, 47f, 812f, 1826f, 91f, Footer, Border);
        CreateBlock("FooterAccent", root, 47f, 812f, 5f, 91f, accent);
        CreateText(root, font, "FooterMessage", message, 28f, FontStyles.Normal, White,
            new Vector2(77f, 834f), new Vector2(1740f, 40f));
        CreateBlock("FooterDivider", root, 47f, 918f, 1826f, 1f, Html("#344353"));
    }

    private static GameObject BuildCaseRecordOverlay(Transform root, TMP_FontAsset font)
    {
        GameObject overlay = CreatePanel("CaseRecordPanel", root, 47f, 268f, 1826f, 520f,
            Html("#121C28", 0.99f), Gold);
        CreateBlock("RecordAccent", overlay.transform, 0f, 0f, 6f, 520f, Gold);
        CreateText(overlay.transform, font, "RecordTitle", "사건 기록 404   /   CASE ARCHIVE",
            34f, FontStyles.Bold, Gold, new Vector2(40f, 32f), new Vector2(1600f, 52f));
        CreateText(overlay.transform, font, "RecordBody",
            "피해자 정하린은 연구실에서 이서연과 대면한 뒤 몸싸움 과정에서 책상 모서리에 충돌했습니다.\n\n" +
            "이서연은 즉시 신고하지 않고 현장을 자살로 오인하도록 조작했습니다. UV 조사로 확인한 충돌 흔적과 휴대폰 약속 기록이 사건 재구성의 핵심 근거가 되었습니다.\n\n" +
            "사건 기록 보기 버튼을 다시 누르면 최종 보고서로 돌아갑니다.",
            30f, FontStyles.Normal, White, new Vector2(42f, 112f), new Vector2(1710f, 330f), true);
        overlay.SetActive(false);
        return overlay;
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string label,
        float x, float y, float width, float height, Color background, Color textColor, Color outline)
    {
        GameObject buttonObject = CreatePanel(name, parent, x, y, width, height, background, outline, true);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.72f, 1f);
        colors.pressedColor = new Color(0.72f, 0.65f, 0.48f, 1f);
        button.colors = colors;
        CreateText(buttonObject.transform, font, "Label", label, 32f, FontStyles.Bold, textColor,
            new Vector2(24f, 19f), new Vector2(width - 48f, 46f), false, TextAlignmentOptions.Center);
        return button;
    }

    private static GameObject CreatePanel(string name, Transform parent, float x, float y,
        float width, float height, Color color, Color outline, bool raycast = false)
    {
        GameObject panel = CreateBlock(name, parent, x, y, width, height, color, raycast);
        AddOutline(panel, outline, 1f);
        return panel;
    }

    private static GameObject CreateBlock(string name, Transform parent, float x, float y,
        float width, float height, Color color, bool raycast = false)
    {
        GameObject go = CreateUIObject(name, parent);
        SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        return go;
    }

    private static TMP_Text CreateText(Transform parent, TMP_FontAsset font, string name, string value,
        float size, FontStyles style, Color color, Vector2 topLeft, Vector2 dimensions,
        bool wrap = false, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        GameObject go = CreateUIObject(name, parent);
        SetTopLeft(go.GetComponent<RectTransform>(), topLeft.x, topLeft.y, dimensions.x, dimensions.y);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
        return text;
    }

    private static GameObject CreateRoot(string name, Transform parent)
    {
        GameObject root = CreateUIObject(name, parent);
        Stretch(root.GetComponent<RectTransform>());
        return root;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void AddOutline(GameObject target, Color color, float distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    private static void ConfigurePortrait()
    {
        AssetDatabase.ImportAsset(PortraitPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(PortraitPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("이서연 Portrait TextureImporter를 찾을 수 없습니다.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }

    private static void SetReference(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"EndingUIController 필드를 찾을 수 없습니다: {propertyName}");
        property.objectReferenceValue = value;
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
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in transforms)
            {
                if (item.name == name)
                    return item.gameObject;
            }
        }
        return null;
    }

    private static GameObject FindChild(Transform parent, string name)
    {
        Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform item in transforms)
        {
            if (item.name == name)
                return item.gameObject;
        }
        throw new InvalidOperationException($"{parent.name} 아래에서 {name}을 찾을 수 없습니다.");
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

    private static Color Html(string value, float alpha = 1f)
    {
        if (!ColorUtility.TryParseHtmlString(value, out Color color))
            return Color.magenta;
        color.a = alpha;
        return color;
    }

    private readonly struct SuccessReferences
    {
        public SuccessReferences(Button caseRecordButton, Button mainMenuButton, GameObject caseRecordPanel)
        {
            CaseRecordButton = caseRecordButton;
            MainMenuButton = mainMenuButton;
            CaseRecordPanel = caseRecordPanel;
        }
        public Button CaseRecordButton { get; }
        public Button MainMenuButton { get; }
        public GameObject CaseRecordPanel { get; }
    }

    private readonly struct FailureReferences
    {
        public FailureReferences(Button retryButton, Button mainMenuButton)
        {
            RetryButton = retryButton;
            MainMenuButton = mainMenuButton;
        }
        public Button RetryButton { get; }
        public Button MainMenuButton { get; }
    }
}
