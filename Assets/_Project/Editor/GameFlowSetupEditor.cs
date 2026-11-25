using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

/// <summary>
/// MainMenu/Briefing Scene을 Unity API로 한 번 생성하는 설정 도구다.
/// Scene YAML을 직접 수정하지 않으며 VR_Test는 읽기 전용 원본으로만 연다.
/// </summary>
public static class GameFlowSetupEditor
{
    private const string MenuPath = "ROOM 404/Game Flow/Create Main Menu and Briefing Scenes";
    private const string RefineMenuPath = "ROOM 404/Game Flow/Apply Figma Layout to Existing Scenes";
    private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string BriefingScenePath = "Assets/_Project/Scenes/Briefing.unity";
    private const string InvestigationScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string SampleScenePath = "Assets/_Project/Scenes/SampleScene.unity";
    private const string FontPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";
    private const string MainBackgroundPath =
        "Assets/_Project/Art/UI/GameFlow/MainMenu_Background.png";
    private const string MenuPatchPath =
        "Assets/_Project/Art/UI/GameFlow/MainMenu_LabBackground.png";
    private const string PlayIconPath = "Assets/_Project/Art/UI/GameFlow/PlayIcon.svg";
    private const string FingerprintIconPath =
        "Assets/_Project/Art/UI/GameFlow/FingerprintIcon.svg";
    private const string ControlsControllerIconPath =
        "Assets/_Project/Art/UI/GameFlow/Controls_ControllerIcon.svg";
    private const string ControlsConnectorLinesPath =
        "Assets/_Project/Art/UI/GameFlow/Controls_ConnectorLines.svg";
    private const string ControlsInnerLinesPath =
        "Assets/_Project/Art/UI/GameFlow/Controls_InnerLines.svg";
    private const string ControlsLeftControllerPath =
        "Assets/_Project/Art/UI/GameFlow/Controls_LeftController.svg";
    private const string ControlsRightControllerPath =
        "Assets/_Project/Art/UI/GameFlow/Controls_RightController.svg";

    private static readonly Color NearBlack = Html("#101617");
    private static readonly Color BriefingBackground = Html("#111517");
    private static readonly Color Panel = Html("#24282B");
    private static readonly Color White = Html("#FAFAFA");
    private static readonly Color Secondary = Html("#BFC2C2");
    private static readonly Color Gold = Html("#C9AA55");
    private static readonly Color GoldButton = Html("#D9B44D");
    private static readonly Color Orange = Html("#F38142");

    [MenuItem(MenuPath)]
    public static void CreateGameFlowScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            ShowDialog("Play Mode에서는 게임 흐름 Scene을 생성할 수 없습니다.");
            return;
        }

        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (!ValidateRequiredAssets())
            return;

        bool scenesExist = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) != null ||
                           AssetDatabase.LoadAssetAtPath<SceneAsset>(BriefingScenePath) != null;
        if (scenesExist && !Application.isBatchMode)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "ROOM 404 Game Flow",
                "MainMenu 또는 Briefing Scene이 이미 존재합니다.\n" +
                "생성 도구가 만든 두 Scene을 새 구성으로 덮어쓸까요?\n\n" +
                "VR_Test와 기존 Prefab은 변경하지 않습니다.",
                "덮어쓰기",
                "취소");
            if (!overwrite)
                return;
        }

        try
        {
            ConfigureSpriteImporter(MainBackgroundPath);
            ConfigureSpriteImporter(MenuPatchPath);
            ConfigureSvgAsSprite(PlayIconPath);
            ConfigureSvgAsSprite(FingerprintIconPath);
            ConfigureControlsSvgAssets();

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Sprite mainBackground = AssetDatabase.LoadAssetAtPath<Sprite>(MainBackgroundPath);
            Texture2D menuPatch = AssetDatabase.LoadAssetAtPath<Texture2D>(MenuPatchPath);
            Sprite playIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PlayIconPath);
            Sprite fingerprintIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FingerprintIconPath);

            Scene sourceScene = EditorSceneManager.OpenScene(
                InvestigationScenePath,
                OpenSceneMode.Single);

            CreateMainMenuScene(
                sourceScene,
                font,
                mainBackground,
                menuPatch,
                playIcon);
            CreateBriefingScene(sourceScene, font, fingerprintIcon);
            ApplyBuildSettings();

            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            AssetDatabase.Refresh();

            Debug.Log(
                "[ROOM 404] MainMenu/Briefing Scene 생성과 Build Settings 구성이 완료되었습니다.");
            ShowDialog(
                "MainMenu와 Briefing Scene을 생성했습니다.\n" +
                "Build Settings 순서: MainMenu, Briefing, VR_Test\n\n" +
                "현재 MainMenu Scene을 열었습니다. Play Mode와 Quest 2 검증은 직접 진행해 주세요.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowDialog(
                "Scene 생성 중 오류가 발생했습니다. Console의 예외를 확인하세요.\n\n" +
                exception.Message);
            if (Application.isBatchMode)
                throw;
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(RefineMenuPath)]
    public static void ApplyFigmaLayoutToExistingScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            ShowDialog("Play Mode에서는 UI 레이아웃을 수정할 수 없습니다.");
            return;
        }

        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(BriefingScenePath) == null)
        {
            ShowDialog("MainMenu 또는 Briefing Scene을 찾을 수 없습니다.");
            return;
        }

        ConfigureSpriteImporter(MainBackgroundPath);
        ConfigureSpriteImporter(MenuPatchPath);
        ConfigureSvgAsSprite(PlayIconPath);
        ConfigureSvgAsSprite(FingerprintIconPath);
        ConfigureControlsSvgAssets();

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(MainBackgroundPath);
        Texture2D patchTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MenuPatchPath);
        Sprite playIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PlayIconPath);
        Sprite fingerprintIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FingerprintIconPath);

        Scene mainMenu = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        ApplyMainMenuFigmaLayout(mainMenu, font, background, patchTexture, playIcon);
        EditorSceneManager.SaveScene(mainMenu);

        Scene briefing = EditorSceneManager.OpenScene(BriefingScenePath, OpenSceneMode.Single);
        ApplyBriefingFigmaLayout(briefing, font, fingerprintIcon);
        EditorSceneManager.SaveScene(briefing);

        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        AssetDatabase.Refresh();

        Debug.Log("[ROOM 404] MainMenu/Briefing Figma 레이아웃 적용 완료");
        ShowDialog(
            "Figma 기준 1920×1080 레이아웃을 MainMenu와 Briefing에 적용했습니다.\n" +
            "버튼 이벤트와 XR 구성은 변경하지 않았습니다.");
    }

    [MenuItem(RefineMenuPath, true)]
    private static bool ValidateRefineMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool ValidateRequiredAssets()
    {
        string[] requiredPaths =
        {
            InvestigationScenePath,
            FontPath,
            MainBackgroundPath,
            MenuPatchPath
        };

        foreach (string path in requiredPaths)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                continue;

            ShowDialog("필수 Asset을 찾을 수 없습니다.\n" + path);
            return false;
        }

        return true;
    }

    private static void ApplyMainMenuFigmaLayout(
        Scene scene,
        TMP_FontAsset font,
        Sprite backgroundSprite,
        Texture2D patchTexture,
        Sprite playIcon)
    {
        GameObject canvasObject = FindObjectInScene(scene, "MainMenuCanvas");
        if (canvasObject == null)
            throw new InvalidOperationException("MainMenuCanvas를 찾을 수 없습니다.");

        ConfigureExistingWorldCanvas(canvasObject, scene);
        Transform root = canvasObject.transform;

        GameObject background = FindChild(root, "Background");
        Stretch(background.GetComponent<RectTransform>());
        Image backgroundImage = GetOrAddComponent<Image>(background);
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = Color.white;
        backgroundImage.preserveAspect = false;
        backgroundImage.raycastTarget = false;
        background.transform.SetSiblingIndex(0);

        GameObject patch = FindChild(root, "MenuBackdropPatch", false);
        if (patch == null)
            patch = CreateUIObject("MenuBackdropPatch", root);
        SetRectFromTopLeft(
            patch.GetComponent<RectTransform>(),
            new Vector2(1061.05f, 429.47f),
            new Vector2(691.292f, 293.971f));
        Image oldPatchImage = patch.GetComponent<Image>();
        if (oldPatchImage != null)
            Object.DestroyImmediate(oldPatchImage);
        RawImage rawPatch = GetOrAddComponent<RawImage>(patch);
        rawPatch.texture = patchTexture;
        rawPatch.uvRect = new Rect(
            1061.05f / 1920f,
            1f - (429.47f + 293.971f) / 1080f,
            691.292f / 1920f,
            293.971f / 1080f);
        rawPatch.color = Color.white;
        rawPatch.raycastTarget = false;
        patch.transform.SetSiblingIndex(1);

        GameObject menuPanel = FindChild(root, "MainMenuPanel");
        SetRectFromTopLeft(
            menuPanel.GetComponent<RectTransform>(),
            new Vector2(1169f, 505.26f),
            new Vector2(678.66f, 436.364f));
        menuPanel.transform.SetSiblingIndex(2);

        Button startButton = FindChild(menuPanel.transform, "StartButton").GetComponent<Button>();
        ConfigureExistingButton(
            startButton,
            font,
            "게임 시작",
            57.416f,
            GoldButton,
            NearBlack,
            new Vector2(0f, 153.876f),
            new Vector2(678.66f, 128.612f),
            Html("#E1BB50"));

        GameObject playIconObject = FindChild(startButton.transform, "PlayIcon", false);
        if (playIcon != null)
        {
            if (playIconObject == null)
                playIconObject = CreateUIObject("PlayIcon", startButton.transform);
            RectTransform iconRect = playIconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(202.605f, 0f);
            iconRect.sizeDelta = new Vector2(56.268f, 56.268f);
            Image iconImage = GetOrAddComponent<Image>(playIconObject);
            iconImage.sprite = playIcon;
            iconImage.color = NearBlack;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }

        Button controlsButton = FindChild(menuPanel.transform, "ControlsButton").GetComponent<Button>();
        ConfigureExistingButton(
            controlsButton,
            font,
            "조작 방법",
            51.675f,
            Html("#101719", 0.97f),
            White,
            new Vector2(0f, -4.271f),
            new Vector2(678.66f, 119.426f),
            Html("#DCE1DF"));
        Button quitButton = FindChild(menuPanel.transform, "QuitButton").GetComponent<Button>();
        ConfigureExistingButton(
            quitButton,
            font,
            "게임 종료",
            51.675f,
            Html("#101719", 0.97f),
            White,
            new Vector2(0f, -158.271f),
            new Vector2(678.66f, 119.426f),
            Html("#DCE1DF"));

        GameObject controlsPanel = FindChild(root, "ControlsPanel");
        Button closeButton = FindChild(controlsPanel.transform, "CloseButton").GetComponent<Button>();
        RebuildControlsPanel(controlsPanel, font, closeButton);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ApplyBriefingFigmaLayout(
        Scene scene,
        TMP_FontAsset font,
        Sprite fingerprintIcon)
    {
        GameObject canvasObject = FindObjectInScene(scene, "BriefingCanvas");
        if (canvasObject == null)
            throw new InvalidOperationException("BriefingCanvas를 찾을 수 없습니다.");

        ConfigureExistingWorldCanvas(canvasObject, scene);
        Transform root = canvasObject.transform;

        GameObject background = FindChild(root, "Background");
        Stretch(background.GetComponent<RectTransform>());
        background.transform.SetSiblingIndex(0);

        ConfigureTopLeftText(root, "CaseKicker", font, 27.56f, Gold,
            TextAlignmentOptions.Left, new Vector2(45.933f, 45.933f), new Vector2(700f, 42f), false);
        ConfigureTopLeftText(root, "CaseTitle", font, 48.23f, White,
            TextAlignmentOptions.Left, new Vector2(45.933f, 96.46f), new Vector2(800f, 68f), false);

        GameObject badge = FindChild(root, "ConfidentialBadge");
        SetRectFromTopLeft(
            badge.GetComponent<RectTransform>(),
            new Vector2(1600.76f, 59.71f),
            new Vector2(273.301f, 52.823f));
        TMP_Text badgeText = badge.GetComponentInChildren<TMP_Text>(true);
        badgeText.font = font;
        badgeText.fontSize = 27.56f;
        Stretch(badgeText.rectTransform);

        GameObject divider = FindChild(root, "HeaderDivider");
        SetRectFromTopLeft(
            divider.GetComponent<RectTransform>(),
            new Vector2(45.933f, 196.36f),
            new Vector2(1828.134f, 1.148f));

        ConfigureInfoPair(root, "Occurrence", font, 45.933f, 220f);
        ConfigureInfoPair(root, "Victim", font, 45.933f, 340f);
        ConfigureInfoPair(root, "Location", font, 978.37f, 220f);
        ConfigureInfoPair(root, "Classification", font, 978.37f, 340f);

        GameObject reportPanel = FindChild(root, "InitialReportPanel");
        SetRectFromTopLeft(
            reportPanel.GetComponent<RectTransform>(),
            new Vector2(45.933f, 496f),
            new Vector2(1828.134f, 331.866f));

        ConfigureTopLeftText(reportPanel.transform, "ReportLabel", font, 27.56f, Gold,
            TextAlignmentOptions.Left, new Vector2(29.856f, 20f), new Vector2(500f, 42f), false);
        TMP_Text report = FindChild(reportPanel.transform, "ReportText").GetComponent<TMP_Text>();
        report.font = font;
        report.fontSize = 36.746f;
        report.color = White;
        report.alignment = TextAlignmentOptions.TopLeft;
        report.lineSpacing = 6f;
        report.margin = Vector4.zero;
        report.textWrappingMode = TextWrappingModes.NoWrap;
        report.overflowMode = TextOverflowModes.Truncate;
        SetRectFromTopLeft(report.rectTransform, new Vector2(29.856f, 78f), new Vector2(1768f, 205f));

        ConfigureTopLeftText(root, "InvestigationObjective", font, 25f, Secondary,
            TextAlignmentOptions.Left, new Vector2(75.789f, 782f), new Vector2(1700f, 40f), false);
        ConfigureTopLeftText(root, "CaseStatus", font, 29.856f, Secondary,
            TextAlignmentOptions.Left, new Vector2(45.933f, 850.8f), new Vector2(500f, 50.526f), false);

        GameObject fingerprint = FindChild(root, "FingerprintIcon", false);
        if (fingerprintIcon != null)
        {
            if (fingerprint == null)
                fingerprint = CreateUIObject("FingerprintIcon", root);
            SetRectFromTopLeft(
                fingerprint.GetComponent<RectTransform>(),
                new Vector2(1823.54f, 850.8f),
                new Vector2(50.526f, 50.526f));
            Image fingerprintImage = GetOrAddComponent<Image>(fingerprint);
            fingerprintImage.sprite = fingerprintIcon;
            fingerprintImage.color = Gold;
            fingerprintImage.preserveAspect = true;
            fingerprintImage.raycastTarget = false;
        }

        GameObject backButtonObject = FindChild(root, "ReturnToMainMenuButton", false);
        Button backButton = backButtonObject != null
            ? backButtonObject.GetComponent<Button>()
            : CreateButton(
                "ReturnToMainMenuButton",
                root,
                font,
                "메인 메뉴",
                Vector2.zero,
                new Vector2(330f, 58f),
                Panel,
                White,
                27f);
        ConfigureExistingButton(
            backButton, font, "메인 메뉴", 27f, Panel, White,
            Vector2.zero, new Vector2(330f, 58f), Html("#DCE1DF"));
        SetRectFromTopLeft(
            backButton.GetComponent<RectTransform>(),
            new Vector2(1450f, 846f),
            new Vector2(330f, 58f));

        BriefingUIController briefingController = Object.FindFirstObjectByType<BriefingUIController>();
        if (briefingController != null)
            SetObjectReference(briefingController, "returnToMainMenuButton", backButton);

        Button startButton = FindChild(root, "StartInvestigationButton").GetComponent<Button>();
        ConfigureExistingButton(
            startButton, font, "현장 조사 시작", 39.043f, GoldButton, NearBlack,
            Vector2.zero, new Vector2(1828.134f, 110.239f), Html("#E1BB50"));
        SetRectFromTopLeft(
            startButton.GetComponent<RectTransform>(),
            new Vector2(45.933f, 923.83f),
            new Vector2(1828.134f, 110.239f));

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureExistingWorldCanvas(GameObject canvasObject, Scene scene)
    {
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.position = new Vector3(0f, 1.6f, 1.35f);
        rect.rotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.0012f;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = FindCamera(scene);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 30f;
        }
    }

    private static void RebuildControlsPanel(
        GameObject controlsPanel,
        TMP_FontAsset font,
        Button closeButton)
    {
        bool wasActive = controlsPanel.activeSelf;
        Transform panelTransform = controlsPanel.transform;
        closeButton.transform.SetParent(panelTransform, false);

        for (int i = panelTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = panelTransform.GetChild(i);
            if (child != closeButton.transform)
                Object.DestroyImmediate(child.gameObject);
        }

        Stretch(controlsPanel.GetComponent<RectTransform>());
        Image overlay = GetOrAddComponent<Image>(controlsPanel);
        overlay.sprite = null;
        overlay.type = Image.Type.Simple;
        overlay.color = BriefingBackground;
        overlay.raycastTarget = true;
        controlsPanel.transform.SetAsLastSibling();

        GameObject header = CreateUIObject("ManualHeader", panelTransform);
        SetRectFromTopLeft(
            header.GetComponent<RectTransform>(),
            new Vector2(45.933f, 45.933f),
            new Vector2(1828.134f, 151.579f));

        TMP_Text kicker = CreateText(
            "ManualKicker", header.transform, font, "ROOM 404 / MANUAL",
            27.56f, FontStyles.Normal, Gold, TextAlignmentOptions.Left);
        SetRectFromTopLeft(kicker.rectTransform, Vector2.zero, new Vector2(500f, 40f));
        kicker.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text title = CreateText(
            "ManualTitle", header.transform, font, "조작 방법",
            48.23f, FontStyles.Bold, White, TextAlignmentOptions.Left);
        SetRectFromTopLeft(
            title.rectTransform,
            new Vector2(0f, 50.526f),
            new Vector2(500f, 68f));
        title.textWrappingMode = TextWrappingModes.NoWrap;

        GameObject divider = CreateUIObject("ManualHeaderDivider", header.transform);
        SetRectFromTopLeft(
            divider.GetComponent<RectTransform>(),
            new Vector2(0f, 150.431f),
            new Vector2(1828.134f, 1.148f));
        AddImage(divider, Html("#45494B"), false);

        CreateSvgImage(
            "ControllerHeaderIcon",
            header.transform,
            AssetDatabase.LoadAssetAtPath<Sprite>(ControlsControllerIconPath),
            new Vector2(1779.9f, 36.75f),
            new Vector2(48.23f, 48.23f));

        GameObject diagram = CreateUIObject("ControllerDiagram", panelTransform);
        SetRectFromTopLeft(
            diagram.GetComponent<RectTransform>(),
            new Vector2(45.933f, 223.923f),
            new Vector2(1828.134f, 535.12f));
        AddImage(diagram, Html("#191D21"), false);
        Outline diagramOutline = diagram.AddComponent<Outline>();
        diagramOutline.effectColor = Html("#292F33");
        diagramOutline.effectDistance = new Vector2(1.148f, -1.148f);

        TMP_Text diagramTitle = CreateText(
            "DiagramTitle", diagram.transform, font,
            "META QUEST 2  /  TOUCH CONTROLLERS",
            21.818f, FontStyles.Normal, Html("#C4A655"), TextAlignmentOptions.Center);
        SetRectFromTopLeft(
            diagramTitle.rectTransform,
            new Vector2(615.5f, 12.63f),
            new Vector2(600f, 34f));
        diagramTitle.textWrappingMode = TextWrappingModes.NoWrap;

        CreateSvgImage(
            "ConnectionLines", diagram.transform,
            AssetDatabase.LoadAssetAtPath<Sprite>(ControlsConnectorLinesPath),
            new Vector2(373.15f, 119.44f),
            new Vector2(1078.4f, 315.77f));
        CreateSvgImage(
            "InnerConnectionLines", diagram.transform,
            AssetDatabase.LoadAssetAtPath<Sprite>(ControlsInnerLinesPath),
            new Vector2(676.4f, 192.91f),
            new Vector2(513.3f, 171.1f));
        CreateSvgImage(
            "LeftControllerVisual", diagram.transform,
            AssetDatabase.LoadAssetAtPath<Sprite>(ControlsLeftControllerPath),
            new Vector2(527.08f, 51.67f),
            new Vector2(367.464f, 476.555f));
        CreateSvgImage(
            "RightControllerVisual", diagram.transform,
            AssetDatabase.LoadAssetAtPath<Sprite>(ControlsRightControllerPath),
            new Vector2(969.19f, 51.67f),
            new Vector2(367.464f, 476.555f));

        CreateControlCard(diagram.transform, font, "LStickCard", "L STICK", "캐릭터 이동", 37.89f, 63.16f);
        CreateControlCard(diagram.transform, font, "LGripCard", "L GRIP / G", "증거물 잡기", 37.89f, 253.78f);
        CreateControlCard(diagram.transform, font, "LTriggerCard", "L TRIGGER / T", "증거 조사 · 수집", 37.89f, 378.95f);
        CreateControlCard(diagram.transform, font, "RStickCard", "R STICK", "시점 회전", 1450.34f, 63.16f);
        CreateControlCard(diagram.transform, font, "RGripCard", "R GRIP / G", "UV 손전등 잡기", 1450.34f, 253.78f);
        CreateControlCard(diagram.transform, font, "RTriggerCard", "R TRIGGER / T", "UV 켜기 · UI 선택", 1450.34f, 378.95f);

        GameObject tipPanel = CreateUIObject("UVTipPanel", panelTransform);
        SetRectFromTopLeft(
            tipPanel.GetComponent<RectTransform>(),
            new Vector2(45.933f, 785.454f),
            new Vector2(1828.134f, 128.612f));
        AddImage(tipPanel, Html("#28251C"), false);

        TMP_Text tipTitle = CreateText(
            "TipTitle", tipPanel.transform, font, "TIP · UV 탐색",
            27.56f, FontStyles.Normal, Html("#DDC77D"), TextAlignmentOptions.Left);
        SetRectFromTopLeft(
            tipTitle.rectTransform,
            new Vector2(29.856f, 20.67f),
            new Vector2(1768.421f, 40f));
        tipTitle.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text tipBody = CreateText(
            "TipBody", tipPanel.transform, font,
            "손전등을 잡고 Trigger로 UV를 켠 뒤, 책상 모서리의 숨겨진 흔적을 조사하세요.",
            33.301f, FontStyles.Normal, Html("#DBD6C5"), TextAlignmentOptions.Left);
        SetRectFromTopLeft(
            tipBody.rectTransform,
            new Vector2(29.856f, 64.115f),
            new Vector2(1768.421f, 48f));
        tipBody.textWrappingMode = TextWrappingModes.NoWrap;

        ConfigureExistingButton(
            closeButton,
            font,
            "메인 화면으로 돌아가기",
            39.043f,
            GoldButton,
            NearBlack,
            Vector2.zero,
            new Vector2(1828.134f, 110.239f),
            Html("#C4A955"));
        SetRectFromTopLeft(
            closeButton.GetComponent<RectTransform>(),
            new Vector2(45.933f, 940.478f),
            new Vector2(1828.134f, 110.239f));
        closeButton.transform.SetAsLastSibling();

        controlsPanel.SetActive(wasActive);
    }

    private static void CreateSvgImage(
        string name,
        Transform parent,
        Sprite sprite,
        Vector2 topLeft,
        Vector2 size)
    {
        if (sprite == null)
            throw new InvalidOperationException($"SVG Sprite를 불러오지 못했습니다: {name}");

        GameObject imageObject = CreateUIObject(name, parent);
        SetRectFromTopLeft(imageObject.GetComponent<RectTransform>(), topLeft, size);
        Image image = AddImage(imageObject, Color.white, false, sprite);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }

    private static void CreateControlCard(
        Transform parent,
        TMP_FontAsset font,
        string name,
        string inputName,
        string description,
        float x,
        float y)
    {
        GameObject card = CreateUIObject(name, parent);
        SetRectFromTopLeft(
            card.GetComponent<RectTransform>(),
            new Vector2(x, y),
            new Vector2(330.718f, 110.239f));
        AddImage(card, Panel, false);
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = Html("#655B3C");
        outline.effectDistance = new Vector2(1.148f, -1.148f);

        TMP_Text inputText = CreateText(
            "InputName", card.transform, font, inputName,
            27.56f, FontStyles.Normal, Gold, TextAlignmentOptions.Left);
        SetRectFromTopLeft(
            inputText.rectTransform,
            new Vector2(20.67f, 14.928f),
            new Vector2(289.378f, 35f));
        inputText.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text descriptionText = CreateText(
            "Description", card.transform, font, description,
            32.153f, FontStyles.Normal, Html("#F4F4F4"), TextAlignmentOptions.Left);
        SetRectFromTopLeft(
            descriptionText.rectTransform,
            new Vector2(20.67f, 54f),
            new Vector2(289.378f, 43f));
        descriptionText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void ConfigureExistingButton(
        Button button,
        TMP_FontAsset font,
        string label,
        float fontSize,
        Color backgroundColor,
        Color textColor,
        Vector2 position,
        Vector2 size,
        Color outlineColor)
    {
        SetRect(button.GetComponent<RectTransform>(), position, size);
        Image image = button.GetComponent<Image>();
        image.color = backgroundColor;
        image.raycastTarget = true;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        button.targetGraphic = image;

        Outline outline = GetOrAddComponent<Outline>(button.gameObject);
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2.3f, -2.3f);
        outline.useGraphicAlpha = true;

        GameObject oldLabel = FindChild(button.transform, "Label", false);
        if (oldLabel != null)
            Object.DestroyImmediate(oldLabel);

        TMP_Text text = CreateText(
            "Label",
            button.transform,
            font,
            label,
            fontSize,
            FontStyles.Bold,
            textColor,
            TextAlignmentOptions.Center);
        text.enabled = true;
        text.alpha = 1f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.margin = Vector4.zero;
        Stretch(text.rectTransform, new Vector2(28f, 10f), new Vector2(-28f, -10f));
        text.transform.SetAsLastSibling();
    }

    private static void ConfigureExistingText(
        TMP_Text text,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 position,
        Vector2 size,
        bool wrap)
    {
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        SetRect(text.rectTransform, position, size);
    }

    private static void ConfigureTopLeftText(
        Transform parent,
        string name,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 topLeft,
        Vector2 size,
        bool wrap)
    {
        TMP_Text text = FindChild(parent, name).GetComponent<TMP_Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.margin = Vector4.zero;
        SetRectFromTopLeft(text.rectTransform, topLeft, size);
    }

    private static void ConfigureInfoPair(
        Transform root,
        string prefix,
        TMP_FontAsset font,
        float x,
        float y)
    {
        ConfigureTopLeftText(root, prefix + "Label", font, 29.856f, Secondary,
            TextAlignmentOptions.Left, new Vector2(x, y), new Vector2(850f, 42f), false);
        ConfigureTopLeftText(root, prefix + "Value", font, 39.043f, White,
            TextAlignmentOptions.Left, new Vector2(x, y + 42f), new Vector2(850f, 58f), false);
    }

    private static GameObject FindObjectInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;

            GameObject child = FindChild(root.transform, name, false);
            if (child != null)
                return child;
        }

        return null;
    }

    private static GameObject FindChild(Transform parent, string name, bool required = true)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child.gameObject;

            GameObject nested = FindChild(child, name, false);
            if (nested != null)
                return nested;
        }

        if (required)
            throw new InvalidOperationException($"'{parent.name}' 아래에서 '{name}'을 찾을 수 없습니다.");

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void CreateMainMenuScene(
        Scene sourceScene,
        TMP_FontAsset font,
        Sprite backgroundSprite,
        Texture2D menuPatchTexture,
        Sprite playIcon)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = "MainMenu";
        CloneVrSupport(sourceScene, scene);
        SceneManager.SetActiveScene(scene);
        ConfigureSceneCamera(scene);

        GameObject flow = new("GameFlow");
        SceneManager.MoveGameObjectToScene(flow, scene);
        SceneFlowController sceneFlow = flow.AddComponent<SceneFlowController>();
        MainMenuUIController menuController = flow.AddComponent<MainMenuUIController>();

        Canvas canvas = CreateWorldCanvas(scene, "MainMenuCanvas");
        RectTransform canvasTransform = (RectTransform)canvas.transform;

        GameObject background = CreateUIObject("Background", canvasTransform);
        Stretch(background.GetComponent<RectTransform>());
        Image backgroundImage = AddImage(background, Color.white, false, backgroundSprite);
        backgroundImage.preserveAspect = false;

        if (backgroundSprite == null)
        {
            backgroundImage.color = NearBlack;
            TMP_Text fallbackTitle = CreateText(
                "TitleFallback",
                canvasTransform,
                font,
                "ROOM\n404",
                150f,
                FontStyles.Bold,
                White,
                TextAlignmentOptions.Center);
            SetRect(fallbackTitle.rectTransform, new Vector2(-420f, 30f), new Vector2(600f, 430f));
        }

        if (menuPatchTexture != null)
        {
            GameObject patch = CreateUIObject("MenuBackdropPatch", canvasTransform);
            SetRectFromTopLeft(
                patch.GetComponent<RectTransform>(),
                new Vector2(1061.05f, 429.47f),
                new Vector2(691.292f, 293.971f));
            AddRawImage(
                patch,
                menuPatchTexture,
                new Rect(
                    1061.05f / 1920f,
                    1f - (429.47f + 293.971f) / 1080f,
                    691.292f / 1920f,
                    293.971f / 1080f));
        }

        GameObject menuPanel = CreateUIObject("MainMenuPanel", canvasTransform);
        SetRectFromTopLeft(
            menuPanel.GetComponent<RectTransform>(),
            new Vector2(1169f, 505.26f),
            new Vector2(678.66f, 436.364f));

        Button startButton = CreateButton(
            "StartButton",
            menuPanel.transform,
            font,
            playIcon == null ? "▶   게임 시작" : "게임 시작",
            new Vector2(0f, 153.876f),
            new Vector2(678.66f, 128.612f),
            GoldButton,
            NearBlack,
            57.416f);
        if (playIcon != null)
        {
            AddButtonIcon(startButton.transform, playIcon);
            FindChild(startButton.transform, "Label").transform.SetAsLastSibling();
        }

        Button controlsButton = CreateButton(
            "ControlsButton",
            menuPanel.transform,
            font,
            "조작 방법",
            new Vector2(0f, -4.271f),
            new Vector2(678.66f, 119.426f),
            Html("#101719", 0.97f),
            White,
            51.675f);
        Button quitButton = CreateButton(
            "QuitButton",
            menuPanel.transform,
            font,
            "게임 종료",
            new Vector2(0f, -158.271f),
            new Vector2(678.66f, 119.426f),
            Html("#101719", 0.97f),
            White,
            51.675f);

        GameObject controlsPanel = CreateControlsPanel(canvasTransform, font, out Button closeButton);
        controlsPanel.SetActive(false);

        SetObjectReference(menuController, "sceneFlowController", sceneFlow);
        SetObjectReference(menuController, "startButton", startButton);
        SetObjectReference(menuController, "controlsButton", controlsButton);
        SetObjectReference(menuController, "quitButton", quitButton);
        SetObjectReference(menuController, "controlsPanel", controlsPanel);
        SetObjectReference(menuController, "closeControlsButton", closeButton);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, MainMenuScenePath))
            throw new InvalidOperationException("MainMenu Scene 저장에 실패했습니다.");

        EditorSceneManager.CloseScene(scene, true);
    }

    private static GameObject CreateControlsPanel(
        Transform canvasTransform,
        TMP_FontAsset font,
        out Button closeButton)
    {
        GameObject overlay = CreateUIObject("ControlsPanel", canvasTransform);
        Stretch(overlay.GetComponent<RectTransform>());
        AddImage(overlay, BriefingBackground, true);

        closeButton = CreateButton(
            "CloseButton",
            overlay.transform,
            font,
            "메인 화면으로 돌아가기",
            Vector2.zero,
            new Vector2(1828.134f, 110.239f),
            GoldButton,
            NearBlack,
            39.043f);

        RebuildControlsPanel(overlay, font, closeButton);

        return overlay;
    }

    private static void CreateBriefingScene(
        Scene sourceScene,
        TMP_FontAsset font,
        Sprite fingerprintIcon)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = "Briefing";
        CloneVrSupport(sourceScene, scene);
        SceneManager.SetActiveScene(scene);
        ConfigureSceneCamera(scene);

        GameObject flow = new("GameFlow");
        SceneManager.MoveGameObjectToScene(flow, scene);
        SceneFlowController sceneFlow = flow.AddComponent<SceneFlowController>();
        BriefingUIController briefingController = flow.AddComponent<BriefingUIController>();

        Canvas canvas = CreateWorldCanvas(scene, "BriefingCanvas");
        RectTransform root = (RectTransform)canvas.transform;

        GameObject background = CreateUIObject("Background", root);
        Stretch(background.GetComponent<RectTransform>());
        AddImage(background, BriefingBackground, false);

        TMP_Text kicker = CreateText(
            "CaseKicker",
            root,
            font,
            "ROOM 404 / CASE BRIEFING",
            27.56f,
            FontStyles.Normal,
            Gold,
            TextAlignmentOptions.Left);
        SetRectFromTopLeft(kicker.rectTransform, new Vector2(45.933f, 45.933f), new Vector2(700f, 42f));

        TMP_Text title = CreateText(
            "CaseTitle",
            root,
            font,
            "사건 기록 #404",
            48f,
            FontStyles.Bold,
            White,
            TextAlignmentOptions.Left);
        SetRectFromTopLeft(title.rectTransform, new Vector2(45.933f, 96.46f), new Vector2(800f, 68f));

        GameObject confidentialBadge = CreateUIObject("ConfidentialBadge", root);
        SetRectFromTopLeft(
            confidentialBadge.GetComponent<RectTransform>(),
            new Vector2(1600.76f, 59.71f),
            new Vector2(273.301f, 52.823f));
        AddImage(confidentialBadge, Html("#38261D"), false);
        TMP_Text confidential = CreateText(
            "ConfidentialBadge",
            confidentialBadge.transform,
            font,
            "CONFIDENTIAL",
            27.56f,
            FontStyles.Normal,
            Orange,
            TextAlignmentOptions.Center);
        Stretch(confidential.rectTransform);

        GameObject divider = CreateUIObject("HeaderDivider", root);
        SetRectFromTopLeft(
            divider.GetComponent<RectTransform>(),
            new Vector2(45.933f, 196.36f),
            new Vector2(1828.134f, 1.148f));
        AddImage(divider, Html("#45494B"), false);

        CreateInfoPair(root, font, "Occurrence", "사건 발생 시각", "2026.10.29 / 21:41", 45.933f, 220f);
        CreateInfoPair(root, font, "Victim", "피해자", "정하린 · 연구원", 45.933f, 340f);
        CreateInfoPair(root, font, "Location", "발생 장소", "대학교 연구실 404호", 978.37f, 220f);
        CreateInfoPair(root, font, "Classification", "사건 분류", "사망 원인 조사 중", 978.37f, 340f);

        GameObject reportPanel = CreateUIObject("InitialReportPanel", root);
        SetRectFromTopLeft(
            reportPanel.GetComponent<RectTransform>(),
            new Vector2(45.933f, 496f),
            new Vector2(1828.134f, 331.866f));
        AddImage(reportPanel, Panel, false);

        TMP_Text reportLabel = CreateText(
            "ReportLabel",
            reportPanel.transform,
            font,
            "INITIAL REPORT",
            27.56f,
            FontStyles.Normal,
            Gold,
            TextAlignmentOptions.Left);
        SetRectFromTopLeft(reportLabel.rectTransform, new Vector2(29.856f, 20f), new Vector2(500f, 42f));

        TMP_Text report = CreateText(
            "ReportText",
            reportPanel.transform,
            font,
            "연구실 404호에서 연구원 정하린이 사망한 채 발견되었다.\n" +
            "현장에서는 자살 가능성이 제기되었으나, 일부 정황이 이를 뒷받침하지 않는다.\n" +
            "사건과 관련된 세 명의 인물이 조사 대상에 올랐다.",
            36.746f,
            FontStyles.Normal,
            White,
            TextAlignmentOptions.TopLeft);
        SetRectFromTopLeft(report.rectTransform, new Vector2(29.856f, 78f), new Vector2(1768f, 205f));
        report.lineSpacing = 6f;
        report.textWrappingMode = TextWrappingModes.NoWrap;
        report.overflowMode = TextOverflowModes.Truncate;

        TMP_Text objective = CreateText(
            "InvestigationObjective",
            root,
            font,
            "수사 목표  ·  현장을 조사해 필수 증거 4개를 확보하고 용의자 정보를 비교하십시오.",
            25f,
            FontStyles.Normal,
            Secondary,
            TextAlignmentOptions.Left);
        SetRectFromTopLeft(objective.rectTransform, new Vector2(75.789f, 782f), new Vector2(1700f, 40f));

        TMP_Text status = CreateText(
            "CaseStatus",
            root,
            font,
            "CASE 404 — 미해결",
            29.856f,
            FontStyles.Normal,
            Secondary,
            TextAlignmentOptions.Left);
        SetRectFromTopLeft(status.rectTransform, new Vector2(45.933f, 850.8f), new Vector2(500f, 50.526f));

        if (fingerprintIcon != null)
        {
            GameObject fingerprint = CreateUIObject("FingerprintIcon", root);
            SetRectFromTopLeft(
                fingerprint.GetComponent<RectTransform>(),
                new Vector2(1823.54f, 850.8f),
                new Vector2(50.526f, 50.526f));
            AddImage(fingerprint, Gold, false, fingerprintIcon).preserveAspect = true;
        }

        Button backButton = CreateButton(
            "ReturnToMainMenuButton",
            root,
            font,
            "메인 메뉴",
            Vector2.zero,
            new Vector2(330f, 58f),
            Html("#24282B"),
            White,
            27f);
        SetRectFromTopLeft(
            backButton.GetComponent<RectTransform>(),
            new Vector2(1450f, 846f),
            new Vector2(330f, 58f));
        Button startButton = CreateButton(
            "StartInvestigationButton",
            root,
            font,
            "현장 조사 시작",
            Vector2.zero,
            new Vector2(1828.134f, 110.239f),
            GoldButton,
            NearBlack,
            39.043f);
        SetRectFromTopLeft(
            startButton.GetComponent<RectTransform>(),
            new Vector2(45.933f, 923.83f),
            new Vector2(1828.134f, 110.239f));

        SetObjectReference(briefingController, "sceneFlowController", sceneFlow);
        SetObjectReference(briefingController, "startInvestigationButton", startButton);
        SetObjectReference(briefingController, "returnToMainMenuButton", backButton);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, BriefingScenePath))
            throw new InvalidOperationException("Briefing Scene 저장에 실패했습니다.");

        EditorSceneManager.CloseScene(scene, true);
    }

    private static void CreateInfoPair(
        Transform parent,
        TMP_FontAsset font,
        string objectName,
        string label,
        string value,
        float x,
        float y)
    {
        TMP_Text labelText = CreateText(
            objectName + "Label",
            parent,
            font,
            label,
            29.856f,
            FontStyles.Normal,
            Secondary,
            TextAlignmentOptions.Left);
        SetRectFromTopLeft(labelText.rectTransform, new Vector2(x, y), new Vector2(850f, 42f));

        TMP_Text valueText = CreateText(
            objectName + "Value",
            parent,
            font,
            value,
            39.043f,
            FontStyles.Normal,
            White,
            TextAlignmentOptions.Left);
        SetRectFromTopLeft(valueText.rectTransform, new Vector2(x, y + 42f), new Vector2(850f, 58f));
    }

    private static Canvas CreateWorldCanvas(Scene scene, string name)
    {
        GameObject canvasObject = new(name, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        canvasObject.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.position = new Vector3(0f, 1.6f, 1.35f);
        rect.rotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.0012f;

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = FindCamera(scene);
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 30f;

        canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        return canvas;
    }

    private static void CloneVrSupport(Scene sourceScene, Scene destinationScene)
    {
        string[] rootNames = { "XR Interaction Manager", "XR Origin (VR)", "EventSystem" };
        Dictionary<Object, Object> objectMap = new();
        List<GameObject> clones = new();

        foreach (string rootName in rootNames)
        {
            GameObject sourceRoot = FindRoot(sourceScene, rootName);
            if (sourceRoot == null)
                throw new InvalidOperationException(
                    $"VR_Test에서 필수 XR Object를 찾을 수 없습니다: {rootName}");

            GameObject clone = Object.Instantiate(sourceRoot);
            clone.name = sourceRoot.name;
            SceneManager.MoveGameObjectToScene(clone, destinationScene);
            MapHierarchy(sourceRoot, clone, objectMap);
            clones.Add(clone);
        }

        foreach (GameObject clone in clones)
            RemapObjectReferences(clone, objectMap);

        // VR_Test의 Camera 아래에는 조사/추리 Canvas가 배치되어 있다. XR 입력 구성만
        // 재사용하고 게임플레이 UI와 그 외부 참조는 새 메뉴 Scene으로 복제하지 않는다.
        foreach (GameObject clone in clones)
            RemoveClonedGameplayCanvases(clone);
    }

    private static void RemoveClonedGameplayCanvases(GameObject cloneRoot)
    {
        Canvas[] canvases = cloneRoot.GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null)
                Object.DestroyImmediate(canvas.gameObject);
        }
    }

    private static void MapHierarchy(
        GameObject source,
        GameObject clone,
        IDictionary<Object, Object> map)
    {
        map[source] = clone;
        map[source.transform] = clone.transform;

        Component[] sourceComponents = source.GetComponents<Component>();
        Component[] cloneComponents = clone.GetComponents<Component>();
        int componentCount = Mathf.Min(sourceComponents.Length, cloneComponents.Length);
        for (int i = 0; i < componentCount; i++)
        {
            if (sourceComponents[i] != null && cloneComponents[i] != null)
                map[sourceComponents[i]] = cloneComponents[i];
        }

        int childCount = Mathf.Min(source.transform.childCount, clone.transform.childCount);
        for (int i = 0; i < childCount; i++)
        {
            MapHierarchy(
                source.transform.GetChild(i).gameObject,
                clone.transform.GetChild(i).gameObject,
                map);
        }
    }

    private static void RemapObjectReferences(
        GameObject cloneRoot,
        IReadOnlyDictionary<Object, Object> map)
    {
        Component[] components = cloneRoot.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null)
                continue;

            SerializedObject serializedObject = new(component);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                Object currentReference = property.objectReferenceValue;
                if (currentReference != null && map.TryGetValue(currentReference, out Object replacement))
                    property.objectReferenceValue = replacement;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureSceneCamera(Scene scene)
    {
        Camera camera = FindCamera(scene);
        if (camera == null)
            throw new InvalidOperationException("복제된 XR Origin에서 Main Camera를 찾을 수 없습니다.");

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
    }

    private static Camera FindCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera = root.GetComponentInChildren<Camera>(true);
            if (camera != null && camera.CompareTag("MainCamera"))
                return camera;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera = root.GetComponentInChildren<Camera>(true);
            if (camera != null)
                return camera;
        }

        return null;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        return null;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Image AddImage(
        GameObject gameObject,
        Color color,
        bool raycastTarget,
        Sprite sprite = null)
    {
        Image image = gameObject.GetComponent<Image>();
        if (image == null)
            image = gameObject.AddComponent<Image>();

        image.color = color;
        image.raycastTarget = raycastTarget;
        image.sprite = sprite;
        if (sprite == null)
        {
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
        }
        else
        {
            image.type = Image.Type.Simple;
        }

        return image;
    }

    private static RawImage AddRawImage(GameObject gameObject, Texture texture, Rect uvRect)
    {
        RawImage rawImage = gameObject.GetComponent<RawImage>();
        if (rawImage == null)
            rawImage = gameObject.AddComponent<RawImage>();

        rawImage.texture = texture;
        rawImage.uvRect = uvRect;
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;
        return rawImage;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string content,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string label,
        Vector2 position,
        Vector2 size,
        Color backgroundColor,
        Color textColor,
        float fontSize)
    {
        GameObject buttonObject = CreateUIObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetRect(rect, position, size);

        Image image = AddImage(buttonObject, backgroundColor, true);
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = backgroundColor == GoldButton ? Html("#E1BB50") : Html("#DCE1DF");
        outline.effectDistance = new Vector2(2.3f, -2.3f);
        outline.useGraphicAlpha = true;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Html("#FFF1B5");
        colors.pressedColor = Html("#B18E35");
        colors.selectedColor = Html("#F2D671");
        colors.disabledColor = Html("#5B6061", 0.65f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text text = CreateText(
            "Label",
            buttonObject.transform,
            font,
            label,
            fontSize,
            FontStyles.Bold,
            textColor,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform, new Vector2(28f, 12f), new Vector2(-28f, -12f));
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        return button;
    }

    private static void AddButtonIcon(Transform button, Sprite iconSprite)
    {
        GameObject icon = CreateUIObject("PlayIcon", button);
        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(202.605f, 0f);
        rect.sizeDelta = new Vector2(56.268f, 56.268f);
        Image image = AddImage(icon, NearBlack, false, iconSprite);
        image.preserveAspect = true;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetRectFromTopLeft(RectTransform rect, Vector2 topLeft, Vector2 size)
    {
        EditorUIUtility.SetTopLeft(rect, topLeft.x, topLeft.y, size.x, size.y);
    }

    private static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(target.GetType().Name, propertyName);

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSpriteImporter(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    private static void ConfigureSvgAsSprite(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            throw new InvalidOperationException($"SVG Importer를 찾을 수 없습니다: {assetPath}");

        SerializedObject serializedImporter = new(importer);
        SerializedProperty svgType =
            serializedImporter.FindProperty("svgType") ??
            serializedImporter.FindProperty("m_SvgType") ??
            serializedImporter.FindProperty("m_SVGType");
        if (svgType == null)
            throw new InvalidOperationException($"SVG 출력 형식 설정을 찾을 수 없습니다: {assetPath}");

        const int TexturedSpriteType = 1;
        bool needsReimport = svgType.intValue != TexturedSpriteType;
        svgType.intValue = TexturedSpriteType;

        SerializedProperty textureSize =
            serializedImporter.FindProperty("textureSize") ??
            serializedImporter.FindProperty("m_TextureSize");
        if (textureSize != null && textureSize.intValue < 1024)
        {
            textureSize.intValue = 1024;
            needsReimport = true;
        }

        if (needsReimport)
        {
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
        }

        if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) == null)
            throw new InvalidOperationException($"SVG를 Textured Sprite로 가져오지 못했습니다: {assetPath}");
    }

    private static void ConfigureControlsSvgAssets()
    {
        ConfigureSvgAsSprite(ControlsControllerIconPath);
        ConfigureSvgAsSprite(ControlsConnectorLinesPath);
        ConfigureSvgAsSprite(ControlsInnerLinesPath);
        ConfigureSvgAsSprite(ControlsLeftControllerPath);
        ConfigureSvgAsSprite(ControlsRightControllerPath);
    }

    private static void ApplyBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new()
        {
            new EditorBuildSettingsScene(MainMenuScenePath, true),
            new EditorBuildSettingsScene(BriefingScenePath, true),
            new EditorBuildSettingsScene(InvestigationScenePath, true)
        };

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath) != null)
            scenes.Add(new EditorBuildSettingsScene(SampleScenePath, false));

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static Color Html(string html, float alpha = 1f)
    {
        if (!ColorUtility.TryParseHtmlString(html, out Color color))
            color = Color.magenta;

        color.a = alpha;
        return color;
    }

    private static void ShowDialog(string message)
    {
        if (!Application.isBatchMode)
            EditorUtility.DisplayDialog("ROOM 404 Game Flow", message, "확인");
    }
}
