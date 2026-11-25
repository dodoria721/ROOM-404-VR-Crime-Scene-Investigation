using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

public static class CaseTypeUISetupEditor
{
    private const string ScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string FontPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";
    private const string UiMaterialPath = "Assets/_Project/Materials/UI/M_UI_AlwaysOnTop.mat";
    private const string TmpMaterialPath = "Assets/_Project/Materials/UI/M_NotoSansKR_Overlay.mat";
    private const string OverlayLayerName = "XR_UI_Overlay";
    private const string Icon01Path = "Assets/_Project/UI/CaseType/사건 유형 아이콘 · 01.png";
    private const string Icon02Path = "Assets/_Project/UI/CaseType/사건 유형 아이콘 · 02.png";
    private const string Icon03Path = "Assets/_Project/UI/CaseType/사건 유형 아이콘 · 03.png";

    private static readonly Color Background = Html("#101721");
    private static readonly Color Grid = Html("#2C3746", 0.22f);
    private static readonly Color Panel = Html("#1A222E");
    private static readonly Color PanelDark = Html("#152330");
    private static readonly Color Summary = Html("#18202B");
    private static readonly Color Border = Html("#596775");
    private static readonly Color Divider = Html("#364353");
    private static readonly Color Gold = Html("#D1B06B");
    private static readonly Color GoldMuted = Html("#C6A66D");
    private static readonly Color White = Html("#F4F1EF");
    private static readonly Color Muted = Html("#9EADB9");
    private static readonly Color Secondary = Html("#B2BCC6");

    private static TMP_FontAsset font;
    private static Material uiMaterial;
    private static Material tmpMaterial;
    private static Sprite icon01;
    private static Sprite icon02;
    private static Sprite icon03;
    private static int overlayLayer;

    [MenuItem("ROOM 404/UI/Configure Case Type Selection")]
    public static void ConfigureCaseTypeSelection()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("ROOM 404",
                "Play Mode에서는 사건 유형 UI를 생성할 수 없습니다.", "확인");
            return;
        }

        try
        {
            LoadResources();
            Scene scene = OpenTargetScene();
            GameObject mainCameraObject = EditorUIUtility.FindSceneObject(scene, "Main Camera");
            GameObject overlayCameraObject = EditorUIUtility.FindSceneObject(scene, "XRUIOverlayCamera");

            if (mainCameraObject == null || overlayCameraObject == null)
                throw new InvalidOperationException("Main Camera 또는 XRUIOverlayCamera를 찾을 수 없습니다.");

            Camera overlayCamera = overlayCameraObject.GetComponent<Camera>();
            if (overlayCamera == null)
                throw new InvalidOperationException("XRUIOverlayCamera에 Camera 컴포넌트가 없습니다.");

            GameObject existing = EditorUIUtility.FindSceneObject(scene, "CaseTypeCanvas");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            GameObject canvasObject = CreateCanvas(mainCameraObject.transform, overlayCamera);
            CaseTypeSelectionController controller =
                Undo.AddComponent<CaseTypeSelectionController>(canvasObject);
            GameObject root = BuildLayout(canvasObject.transform, controller, scene);

            SetLayerRecursively(canvasObject, overlayLayer);
            ConfigureController(controller, root, scene);
            AddBlockingModalToNotebook(scene, root);
            root.SetActive(false);

            EditorUtility.SetDirty(canvasObject);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("VR_Test Scene 저장에 실패했습니다.");

            Selection.activeGameObject = canvasObject;
            AssetDatabase.SaveAssets();
            Debug.Log("[ROOM 404] CaseTypeCanvas Figma UI 생성 및 참조 연결 완료");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("ROOM 404 - Case Type UI", exception.Message, "확인");
        }
    }

    private static void LoadResources()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new InvalidOperationException("NotoSansKR-Regular SDF Font Asset을 찾을 수 없습니다.");

        uiMaterial = AssetDatabase.LoadAssetAtPath<Material>(UiMaterialPath);
        tmpMaterial = AssetDatabase.LoadAssetAtPath<Material>(TmpMaterialPath);
        icon01 = LoadIconSprite(Icon01Path);
        icon02 = LoadIconSprite(Icon02Path);
        icon03 = LoadIconSprite(Icon03Path);
        overlayLayer = LayerMask.NameToLayer(OverlayLayerName);
        if (overlayLayer < 0)
            throw new InvalidOperationException($"Layer가 존재하지 않습니다: {OverlayLayerName}");
    }

    private static Sprite LoadIconSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"사건 유형 아이콘을 찾을 수 없습니다: {path}");

        bool changed = importer.textureType != TextureImporterType.Sprite ||
                       importer.spriteImportMode != SpriteImportMode.Single ||
                       importer.mipmapEnabled || !importer.alphaIsTransparency;
        if (changed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException($"Sprite로 불러올 수 없는 사건 유형 아이콘입니다: {path}");
        return sprite;
    }

    private static Scene OpenTargetScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else
            SceneManager.SetActiveScene(scene);
        return scene;
    }

    private static GameObject CreateCanvas(Transform parent, Camera eventCamera)
    {
        GameObject go = new("CaseTypeCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, "Create Case Type Canvas");
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.localPosition = new Vector3(0f, 0f, 1.2f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.001f;

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eventCamera;
        canvas.sortingOrder = 55;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 30f;

        TrackedDeviceGraphicRaycaster raycaster = go.GetComponent<TrackedDeviceGraphicRaycaster>();
        raycaster.ignoreReversedGraphics = false;
        raycaster.checkFor2DOcclusion = false;
        raycaster.checkFor3DOcclusion = false;
        return go;
    }

    private static GameObject BuildLayout(Transform canvas, CaseTypeSelectionController controller,
        Scene scene)
    {
        GameObject root = CreateRoot("CaseTypeRoot", canvas);
        EditorUIUtility.StretchWithCenteredPivot(root.GetComponent<RectTransform>());

        CreateImage(root.transform, "Background", 0f, 0f, 1920f, 1080f, Background);
        CreateGrid(root.transform);
        BuildHeader(root.transform);

        CreateText(root.transform, "SectionLabel",
            "CASE TYPE    /    사건의 본질을 가장 잘 설명하는 유형을 선택하세요",
            22f, FontStyles.Normal, Secondary, 54f, 184f, 1120f, 50f);

        CardRefs suicide = BuildCard(root.transform, "SuicideCard", 50f, "01", "SUICIDE",
            "단독 자살", "타인의 개입 없는 자살",
            "제3자가 관여하지 않고 스스로\n생을 마감했다는 가설입니다.", icon01);
        CardRefs accident = BuildCard(root.transform, "AccidentCard", 455f, "02", "ACCIDENT",
            "단독 사고", "타인 없이 발생한 사고",
            "다른 사람과의 접촉 없이 우발적인\n사고로 사망했다는 가설입니다.", icon02);
        CardRefs thirdParty = BuildCard(root.transform, "ThirdPartyCard", 860f, "03",
            "THIRD-PARTY INVOLVEMENT", "타인의 개입", "다른 인물이 관여한 사건",
            "다른 인물의 행동이 사망 경위에\n영향을 주었다는 가설입니다.", icon03);

        SummaryRefs summary = BuildSummary(root.transform);
        FooterRefs footer = BuildFooter(root.transform);

        SerializedObject serialized = new(controller);
        Set(serialized, "caseTypeRoot", root);
        Set(serialized, "suicideButton", suicide.Button);
        Set(serialized, "accidentButton", accident.Button);
        Set(serialized, "thirdPartyButton", thirdParty.Button);
        Set(serialized, "suicideOutline", suicide.Outline);
        Set(serialized, "accidentOutline", accident.Outline);
        Set(serialized, "thirdPartyOutline", thirdParty.Outline);
        Set(serialized, "suicideRadioSelected", suicide.RadioDot);
        Set(serialized, "accidentRadioSelected", accident.RadioDot);
        Set(serialized, "thirdPartyRadioSelected", thirdParty.RadioDot);
        Set(serialized, "currentSelectionText", summary.CurrentSelection);
        Set(serialized, "selectionHintText", footer.Hint);
        Set(serialized, "requiredEvidenceText", summary.RequiredEvidence);
        Set(serialized, "openNotebookButton", footer.NotebookButton);
        Set(serialized, "nextStepButton", footer.NextButton);
        Set(serialized, "nextStepBackground", footer.NextBackground);
        Set(serialized, "nextStepLabel", footer.NextLabel);

        NotebookToggleController notebook = FindComponentInScene<NotebookToggleController>(scene);
        Set(serialized, "notebookToggleController", notebook);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static void BuildHeader(Transform parent)
    {
        CreateImage(parent, "HeaderGoldMark", 44f, 27f, 31f, 11f, Html("#C5A95F"));
        CreateImage(parent, "HeaderGrayMark", 81f, 27f, 14f, 11f, Html("#66727F"));
        CreateText(parent, "HeaderBreadcrumb",
            "ROOM 404   /   FINAL DEDUCTION   /   STEP 01", 25f, FontStyles.Normal,
            Html("#D4C6A4"), 110f, 14f, 740f, 53f);

        CreateImage(parent, "TitleAccent", 45f, 90f, 7f, 54f, Html("#C8AA64"));
        CreateText(parent, "Title", "사건 유형", 64f, FontStyles.Bold,
            White, 80f, 73f, 390f, 84f);
        CreateImage(parent, "TitleDivider", 490f, 84f, 2f, 56f, Html("#5D6974"));
        CreateText(parent, "HeaderInstruction",
            "정하린의 사망 경위를 판단한 뒤 용의자 지목 단계로 진행하세요.",
            28f, FontStyles.Normal, Html("#D6D9DB"), 520f, 82f, 1050f, 58f);

        CreateText(parent, "Room404Brand", "ROOM 404", 40f, FontStyles.Bold,
            Html("#636C78"), 1590f, 20f, 270f, 55f, TextAlignmentOptions.Right);
        CreateText(parent, "BrandSubtitle", "VR CRIME INVESTIGATION", 13f,
            FontStyles.Normal, Html("#717E8D"), 1586f, 78f, 271f, 25f,
            TextAlignmentOptions.Right);
        CreateImage(parent, "HeaderBottomLine", 42f, 168f, 1836f, 2f, Html("#455365"));
    }

    private static CardRefs BuildCard(Transform parent, string name, float x, string number,
        string english, string korean, string subtitle, string description, Sprite icon)
    {
        GameObject card = CreateImage(parent, name, x, 238f, 388f, 599f, Panel, true);
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        Button button = card.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Html("#EEF2F5");
        colors.pressedColor = Html("#C9D0D6");
        colors.selectedColor = Color.white;
        colors.disabledColor = Html("#737B84", 0.7f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        CreateImage(card.transform, "GraphicPanel", 7.5f, 7.5f, 370f, 338f, PanelDark);
        CreateImage(card.transform, "CardGoldMark", 24.5f, 31.5f, 6f, 31f, GoldMuted);
        CreateText(card.transform, "CaseTypeLabel", $"CASE TYPE  /  {number}", 21f,
            FontStyles.Bold, Html("#CBB078"), 42.5f, 20.5f, 270f, 52f);

        CreateText(card.transform, "OuterRing", "○", 183f, FontStyles.Normal,
            Html("#667788"), 101.5f, 92.5f, 183f, 183f, TextAlignmentOptions.Center);
        CreateText(card.transform, "InnerRing", "○", 137f, FontStyles.Normal,
            Html("#596B7C"), 124.5f, 115.5f, 137f, 137f, TextAlignmentOptions.Center);
        CreateSpriteImage(card.transform, "CaseTypeIcon", icon, 130.5f, 121.5f, 124f, 124f);
        CreateImage(card.transform, "DiagramUnderline", 145.5f, 287.5f, 94f, 3f,
            Html("#7C8C97", 0.68f));

        CreateText(card.transform, "EnglishName", english, 18f, FontStyles.Normal,
            Html("#A9B7BF"), 28.5f, 320.5f, 331f, 35f, TextAlignmentOptions.Center);
        CreateText(card.transform, "KoreanName", korean, 43f, FontStyles.Bold,
            White, 24.5f, 378.5f, 330f, 69f);
        CreateText(card.transform, "Subtitle", subtitle, 23f, FontStyles.Normal,
            Html("#D3B67E"), 24.5f, 450.5f, 330f, 44f);
        CreateImage(card.transform, "BodyDivider", 24.5f, 506.5f, 336f, 1f,
            Html("#566575"));
        CreateText(card.transform, "Description", description, 19f, FontStyles.Normal,
            Html("#BBC8D2"), 24.5f, 519.5f, 334f, 69f,
            TextAlignmentOptions.TopLeft, true);

        CreateText(card.transform, "RadioRing", "○", 35f, FontStyles.Normal,
            Secondary, 331.5f, 30.5f, 29f, 29f, TextAlignmentOptions.Center);
        GameObject dot = CreateText(card.transform, "RadioSelected", "●", 23f,
            FontStyles.Normal, Gold, 331.5f, 30.5f, 29f, 29f,
            TextAlignmentOptions.Center).gameObject;
        dot.SetActive(false);

        return new CardRefs(button, outline, dot);
    }

    private static SummaryRefs BuildSummary(Transform parent)
    {
        GameObject panel = CreateImage(parent, "CaseSummaryPanel", 1262f, 238f, 610f, 599f,
            Summary);
        AddOutline(panel, Border);
        CreateText(panel.transform, "TitleMark", "▱", 35f, FontStyles.Normal,
            Html("#C6A663"), 23.7f, 18.7f, 58f, 49f);
        CreateText(panel.transform, "Title", "사건 개요", 34f, FontStyles.Bold,
            White, 90.7f, 19.7f, 240f, 48f);
        GameObject requiredBadge = CreateImage(panel.transform, "RequiredEvidenceBadge",
            346.7f, 22.7f, 234f, 46f, Html("#262B30"));
        AddOutline(requiredBadge, GoldMuted);
        TMP_Text required = CreateText(requiredBadge.transform, "RequiredEvidence",
            "✓  필수 0/4", 24f, FontStyles.Bold, Html("#D8BD79"), 9.5f, 0f, 215f, 45f,
            TextAlignmentOptions.Center);
        CreateImage(panel.transform, "HeaderDivider", 23.7f, 88.7f, 562f, 2f,
            Html("#5C6B7B"));

        float y = 109.7f;
        CreateSummaryRow(panel.transform, "VictimRow", "피해자", "정하린", y);
        CreateSummaryRow(panel.transform, "TimeRow", "사망 시간", "2026.10.29  21:41", y += 70f);
        CreateSummaryRow(panel.transform, "CauseRow", "사망 원인", "두부 외상", y += 70f);
        CreateSummaryRow(panel.transform, "IssueRow", "쟁점", "사건 현장의 은폐 정황", y += 70f);
        CreateSummaryRow(panel.transform, "EvidenceRow", "전체 증거", "6개 중 4개 필수", y += 70f);
        TMP_Text current = CreateSummaryRow(panel.transform, "SelectionRow", "현재 선택", "없음",
            y += 70f, true);

        CreateText(panel.transform, "SummaryFooter",
            "필수 증거 확보 후 다음 단계에서 용의자를 지목할 수 있습니다.",
            18f, FontStyles.Normal, Html("#B2BEC8"), 23.7f, 543.7f, 568f, 35f);
        return new SummaryRefs(current, required);
    }

    private static TMP_Text CreateSummaryRow(Transform parent, string name, string label,
        string value, float y, bool highlight = false)
    {
        GameObject row = CreateRoot(name, parent);
        EditorUIUtility.SetTopLeft(row.GetComponent<RectTransform>(), 26.7f, y, 549f, 61f);
        CreateText(row.transform, "Label", label, 23f, FontStyles.Normal,
            Muted, 1f, 0f, 178f, 52f);
        TMP_Text valueText = CreateText(row.transform, "Value", value, 25f,
            highlight ? FontStyles.Bold : FontStyles.Normal,
            highlight ? Html("#D7C49D") : Html("#F0F0ED"), 181f, 0f, 350f, 52f);
        CreateImage(row.transform, "Divider", 0f, 61f, 549f, 1f, Html("#465362"));
        return valueText;
    }

    private static FooterRefs BuildFooter(Transform parent)
    {
        CreateImage(parent, "FooterDivider", 42f, 867f, 1836f, 2f, Html("#566574"));
        Button notebook = CreateButton(parent, "OpenNotebookButton", "▤   형사 수첩 열기",
            50f, 917f, 417f, 108f, Html("#222E3A"), Html("#EBEFF2"),
            Html("#B3BFC9"), 31f);

        TMP_Text hint = CreateText(parent, "SelectionHint",
            "사건 유형을 선택하면 용의자 선택 단계로 이동합니다.",
            21f, FontStyles.Normal, Html("#899AA7"), 501f, 950f, 867f, 48f,
            TextAlignmentOptions.Center);

        Button next = CreateButton(parent, "NextStepButton", "›      다음 단계",
            1404f, 911f, 468f, 118f, Html("#736C55"), Html("#343638"),
            Html("#A99872"), 39f);
        next.interactable = false;
        Image nextBackground = next.GetComponent<Image>();
        TMP_Text nextLabel = next.transform.Find("Label").GetComponent<TMP_Text>();
        return new FooterRefs(notebook, next, nextBackground, nextLabel, hint);
    }

    private static Button CreateButton(Transform parent, string name, string label,
        float x, float y, float width, float height, Color background, Color textColor,
        Color border, float fontSize)
    {
        GameObject go = CreateImage(parent, name, x, y, width, height, background, true);
        AddOutline(go, border);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Html("#F0E8D5");
        colors.pressedColor = Html("#B6A98D");
        colors.selectedColor = Color.white;
        button.colors = colors;
        CreateText(go.transform, "Label", label, fontSize, FontStyles.Bold, textColor,
            18f, 0f, width - 36f, height, TextAlignmentOptions.Center);
        return button;
    }

    private static void CreateGrid(Transform parent)
    {
        GameObject grid = CreateRoot("BackgroundGrid", parent);
        EditorUIUtility.StretchWithCenteredPivot(grid.GetComponent<RectTransform>());
        for (int x = 46; x <= 1920; x += 84)
            CreateImage(grid.transform, $"GridV_{x}", x, 0f, 1f, 1080f, Grid);
        for (int y = 36; y <= 1080; y += 84)
            CreateImage(grid.transform, $"GridH_{y}", 0f, y, 1920f, 1f, Grid);
    }

    private static GameObject CreateSpriteImage(Transform parent, string name, Sprite sprite,
        float x, float y, float width, float height)
    {
        GameObject go = CreateRoot(name, parent);
        EditorUIUtility.SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        if (uiMaterial != null)
            image.material = uiMaterial;
        return go;
    }

    private static GameObject CreateImage(Transform parent, string name, float x, float y,
        float width, float height, Color color, bool raycast = false)
    {
        GameObject go = CreateRoot(name, parent);
        EditorUIUtility.SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        if (uiMaterial != null)
            image.material = uiMaterial;
        return go;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float size,
        FontStyles style, Color color, float x, float y, float width, float height,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left, bool wrap = false)
    {
        GameObject go = CreateRoot(name, parent);
        EditorUIUtility.SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
        if (tmpMaterial != null)
            text.fontSharedMaterial = tmpMaterial;
        return text;
    }

    private static Outline AddOutline(GameObject target, Color color)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
        return outline;
    }

    private static GameObject CreateRoot(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);
        go.layer = overlayLayer;
        return go;
    }

    private static void ConfigureController(CaseTypeSelectionController controller,
        GameObject root, Scene scene)
    {
        if (controller == null || root == null)
            throw new InvalidOperationException("CaseTypeSelectionController 생성에 실패했습니다.");

        if (controller.transform != root.transform.parent)
            throw new InvalidOperationException("Controller는 활성 상태인 CaseTypeCanvas에 있어야 합니다.");

        DeductionStartUIController startController =
            FindComponentInScene<DeductionStartUIController>(scene);
        if (startController == null)
            throw new InvalidOperationException("DeductionStartUIController를 찾을 수 없습니다.");

        SerializedObject serializedStartController = new(startController);
        SerializedProperty caseTypeReference =
            serializedStartController.FindProperty("caseTypeSelectionController");
        if (caseTypeReference == null)
            throw new InvalidOperationException(
                "DeductionStartUIController의 CaseType 참조 필드를 찾을 수 없습니다.");
        caseTypeReference.objectReferenceValue = controller;
        serializedStartController.ApplyModifiedPropertiesWithoutUndo();

        DeductionUIController deductionUIController =
            FindComponentInScene<DeductionUIController>(scene);
        if (deductionUIController == null)
            throw new InvalidOperationException("DeductionUIController를 찾을 수 없습니다.");

        SerializedObject serializedDeductionUI = new(deductionUIController);
        SerializedProperty deductionCaseTypeReference =
            serializedDeductionUI.FindProperty("caseTypeSelectionController");
        if (deductionCaseTypeReference == null)
            throw new InvalidOperationException(
                "DeductionUIController의 사건 유형 참조 필드를 찾을 수 없습니다.");
        deductionCaseTypeReference.objectReferenceValue = controller;
        serializedDeductionUI.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(startController);
        EditorUtility.SetDirty(deductionUIController);
    }

    private static void AddBlockingModalToNotebook(Scene scene, GameObject root)
    {
        NotebookToggleController notebook = FindComponentInScene<NotebookToggleController>(scene);
        if (notebook == null)
        {
            Debug.LogWarning("[ROOM 404] NotebookToggleController를 찾지 못해 수첩 참조를 연결하지 않았습니다.");
            return;
        }

        SerializedObject serialized = new(notebook);
        SerializedProperty modals = serialized.FindProperty("blockingModals");
        if (modals == null)
            return;

        // Re-running the generator destroys the previous generated root. Compact its
        // now-missing reference and any older CaseTypeRoot entry before adding one.
        for (int i = modals.arraySize - 1; i >= 0; i--)
        {
            Object value = modals.GetArrayElementAtIndex(i).objectReferenceValue;
            if (value == null || value.name == "CaseTypeRoot")
                modals.DeleteArrayElementAtIndex(i);
        }
        modals.InsertArrayElementAtIndex(modals.arraySize);
        modals.GetArrayElementAtIndex(modals.arraySize - 1).objectReferenceValue = root;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(notebook);
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Controller 필드를 찾을 수 없습니다: {propertyName}");
        property.objectReferenceValue = value;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            item.gameObject.layer = layer;
    }

    private static Color Html(string html, float alpha = 1f)
    {
        if (!ColorUtility.TryParseHtmlString(html, out Color color))
            throw new ArgumentException($"잘못된 색상 값: {html}");
        color.a = alpha;
        return color;
    }

    private readonly struct CardRefs
    {
        public readonly Button Button;
        public readonly Outline Outline;
        public readonly GameObject RadioDot;

        public CardRefs(Button button, Outline outline, GameObject radioDot)
        {
            Button = button;
            Outline = outline;
            RadioDot = radioDot;
        }
    }

    private readonly struct SummaryRefs
    {
        public readonly TMP_Text CurrentSelection;
        public readonly TMP_Text RequiredEvidence;

        public SummaryRefs(TMP_Text currentSelection, TMP_Text requiredEvidence)
        {
            CurrentSelection = currentSelection;
            RequiredEvidence = requiredEvidence;
        }
    }

    private readonly struct FooterRefs
    {
        public readonly Button NotebookButton;
        public readonly Button NextButton;
        public readonly Image NextBackground;
        public readonly TMP_Text NextLabel;
        public readonly TMP_Text Hint;

        public FooterRefs(Button notebookButton, Button nextButton, Image nextBackground,
            TMP_Text nextLabel, TMP_Text hint)
        {
            NotebookButton = notebookButton;
            NextButton = nextButton;
            NextBackground = nextBackground;
            NextLabel = nextLabel;
            Hint = hint;
        }
    }
}
