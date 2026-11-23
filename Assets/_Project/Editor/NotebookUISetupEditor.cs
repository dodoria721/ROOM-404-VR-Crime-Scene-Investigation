using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Creates the Figma-based first notebook page in VR_Test through supported Unity APIs.
/// The command aborts when NotebookCanvas already exists.
/// </summary>
public static class NotebookUISetupEditor
{
    private const string ScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string ActionPath = "Assets/_Project/Scripts/UI/NotebookInput.asset";
    private const string FontPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";
    private const string UiMaterialPath = "Assets/_Project/Materials/UI/M_UI_AlwaysOnTop.mat";
    private const string TextMaterialPath = "Assets/_Project/Materials/UI/M_NotoSansKR_Overlay.mat";
    private const string WornEdgesPath = "Assets/_Project/UI/Notebook/NotebookWornEdges.png";

    private static int overlayLayer;
    private static TMP_FontAsset font;
    private static Material uiMaterial;
    private static Material textMaterial;
    private static Texture2D wornEdgesTexture;

    [MenuItem("ROOM 404/UI/Create Notebook")]
    public static void CreateNotebook()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before creating the notebook UI.");

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        Camera mainCamera = FindMainCamera(scene);
        if (mainCamera == null)
            throw new InvalidOperationException("VR_Test Main Camera was not found.");

        if (mainCamera.transform.Find("NotebookCanvas") != null)
            throw new InvalidOperationException(
                "NotebookCanvas already exists. Existing UI was not overwritten.");

        Transform overlayTransform = mainCamera.transform.Find("XRUIOverlayCamera");
        Camera overlayCamera = overlayTransform != null ? overlayTransform.GetComponent<Camera>() : null;
        LoadRequiredAssets(overlayCamera);
        InputActionReference toggleAction = GetOrCreateToggleAction();

        GameObject canvasObject = new GameObject(
            "NotebookCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster),
            typeof(NotebookToggleController));

        Undo.RegisterCreatedObjectUndo(canvasObject, "Create ROOM 404 Notebook");
        canvasObject.layer = overlayLayer;
        canvasObject.transform.SetParent(mainCamera.transform, false);

        RectTransform canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        canvasRect.localPosition = new Vector3(0f, 0f, 1.2f);
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one * 0.001f;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = overlayCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 250;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 100f;

        TrackedDeviceGraphicRaycaster raycaster =
            canvasObject.GetComponent<TrackedDeviceGraphicRaycaster>();
        raycaster.ignoreReversedGraphics = false;
        raycaster.checkFor2DOcclusion = false;
        raycaster.checkFor3DOcclusion = false;

        RectTransform notebookRoot = CreateRect("NotebookRoot", canvasRect, 0f, 0f, 1920f, 1080f);
        BuildNotebook(notebookRoot);
        NotebookPageSetupEditor.ConfigureNotebookPagesInScene(scene, canvasObject);
        ConfigureController(
            canvasObject.GetComponent<NotebookToggleController>(),
            notebookRoot,
            toggleAction,
            scene);

        // Canvas/controller stay active. Only the visual root starts closed.
        notebookRoot.gameObject.SetActive(false);

        EditorUtility.SetDirty(canvasObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("VR_Test could not be saved.");

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = canvasObject;
        Debug.Log("[Notebook] Figma notebook UI was created and saved in VR_Test.", canvasObject);
    }

    private static void BuildNotebook(RectTransform root)
    {
        Image modalBlocker =
            CreateImage("ModalBlocker", root, 0f, 0f, 1920f, 1080f, "191E20", 0.98f);
        modalBlocker.raycastTarget = true;

        CreateRawImage(
            "WornEdges", root, 0f, 0f, 1920f, 1080f, wornEdgesTexture, Color.white);
        CreateText(
            "NotebookHeader", root, 72f, 28f, 1776f, 56f,
            "ROOM 404   /   DETECTIVE'S NOTEBOOK", 25f, "C9AA55", FontStyles.Normal);

        RectTransform visual = CreateRect("NotebookVisual", root, 0f, 0f, 1920f, 1080f);
        CreateImage("CoverShadow", visual, 306f, 100f, 1148f, 920f, "090D0F", 0.65f);
        CreateImage("Cover", visual, 320f, 110f, 1120f, 900f, "355565");
        CreateImage("CoverInner", visual, 334f, 124f, 1092f, 872f, "496B79");
        CreateImage("PageEdgeShadow", visual, 348f, 134f, 1068f, 854f, "8D795D");
        CreateImage("Paper", visual, 346f, 128f, 1060f, 856f, "EFE0C4");
        CreateImage("PaperTint", visual, 356f, 138f, 1040f, 836f, "F8EED9", 0.36f);
        BuildBinding(visual);

        RectTransform content = CreateRect("NotebookContent", root, 346f, 128f, 1060f, 856f);
        BuildCaseOverviewPage(content);

        RectTransform tabs = CreateRect("TabNavigation", root, 1394f, 244f, 220f, 536f);
        BuildTabs(tabs);

        RectTransform pageNavigation =
            CreateRect("PageNavigation", root, 346f, 128f, 1060f, 856f);
        BuildPageNavigation(pageNavigation);
    }

    private static void BuildBinding(RectTransform visual)
    {
        RectTransform binding = CreateRect("SpringBinding", visual, 282f, 145f, 120f, 820f);
        for (int i = 0; i < 16; i++)
        {
            float y = 14f + i * 50f;
            CreateImage($"RingShadow_{i + 1:00}", binding, 10f, y + 3f, 104f, 13f, "0C1519", 0.75f);
            CreateImage($"Ring_{i + 1:00}", binding, 7f, y, 104f, 10f, "B8C4C8");
            CreateImage($"RingHighlight_{i + 1:00}", binding, 10f, y + 1f, 94f, 3f, "F2F5F5", 0.9f);
            CreateImage($"PageHole_{i + 1:00}", binding, 90f, y - 1f, 16f, 16f, "6B5C49", 0.9f);
        }
    }

    private static void BuildTabs(RectTransform tabsRoot)
    {
        string[] labels = { "사건\n개요", "피해자", "용의자", "증거", "메모" };
        string[] colors = { "CDB66F", "B4C2CA", "9CB2A7", "BFA39A", "AAA4BD" };

        for (int i = 0; i < labels.Length; i++)
        {
            float width = i == 0 ? 220f : 192f;
            float x = i == 0 ? 0f : 28f;
            RectTransform tabRoot =
                CreateRect($"Tab_{i + 1:00}", tabsRoot, x, i * 110f, width, 96f);
            CreateImage("TabShadow", tabRoot, 6f, 7f, width, 92f, "080B0D", 0.45f);
            Image tabGraphic = CreateImage("TabGraphic", tabRoot, 0f, 0f, width, 92f, colors[i]);
            tabGraphic.raycastTarget = true;

            Button button = tabGraphic.gameObject.AddComponent<Button>();
            button.targetGraphic = tabGraphic;
            button.transition = Selectable.Transition.None;
            button.interactable = false;

            TextMeshProUGUI label =
                CreateText("Label", tabRoot, 28f, 10f, width - 50f, 72f,
                    labels[i], i == 0 ? 31f : 28f, "2E393B", FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    private static void BuildCaseOverviewPage(RectTransform content)
    {
        RectTransform page = CreateRect("CaseOverviewPage", content, 0f, 0f, 1060f, 856f);

        CreateText("CaseCode", page, 100f, 36f, 450f, 38f,
            "CASE 404", 23f, "6C8390", FontStyles.Bold);

        Image stamp = CreateImage(
            "CaseFileStamp", page, 818f, 30f, 184f, 48f, "9B4438", 0.08f);
        TextMeshProUGUI stampText =
            CreateText("Text", stamp.rectTransform, 0f, 0f, 184f, 48f,
                "CASE FILE", 21f, "9B4438", FontStyles.Bold);
        stampText.alignment = TextAlignmentOptions.Center;

        CreateText("Title", page, 100f, 80f, 900f, 62f,
            "사건 개요", 42f, "2B383D", FontStyles.Bold);
        CreateImage("TitleRule", page, 100f, 151f, 900f, 2f, "BCAA8C");
        CreateImage("TitleAccent", page, 100f, 142f, 140f, 5f, "C9A551");

        string[] fieldNames = { "사건명", "발생 일시", "발생 장소", "피해자", "사건 상태" };
        string[] fieldValues =
        {
            "연구실 404호 사망 사건",
            "2026.10.29 21:41경",
            "대학교 연구실 404호",
            "정하린 (임상시험 총괄 연구원)",
            "사망 경위 조사 중"
        };

        for (int i = 0; i < fieldNames.Length; i++)
        {
            float y = 184f + i * 62f;
            CreateText($"FieldLabel_{i + 1:00}", page, 100f, y, 180f, 40f,
                fieldNames[i], 23f, "687074", FontStyles.Bold);
            CreateText($"FieldValue_{i + 1:00}", page, 290f, y - 2f, 710f, 42f,
                fieldValues[i], 28f, "2B383D", FontStyles.Normal);
            CreateImage($"FieldRule_{i + 1:00}", page, 100f, y + 50f, 900f, 1f, "C5B598");
        }

        CreateImage("ReportLabelBackground", page, 92f, 510f, 260f, 52f, "D7C39C", 0.52f);
        CreateText("ReportTitle", page, 100f, 518f, 900f, 44f,
            "사건 개요 보고", 29f, "2B383D", FontStyles.Bold);
        CreateText("ReportBody", page, 100f, 576f, 900f, 148f,
            "연구실 404호에서 정하린 연구원이 사망한 채 발견되었다.\n" +
            "현장에는 자살을 의심하게 하는 정황이 있으나,\n" +
            "피해자의 두부 손상과 일부 혈흔 증거가 이에 부합하지 않는다.",
            27f, "2B383D", FontStyles.Normal, 1.22f);
    }

    private static void BuildPageNavigation(RectTransform pageNavigation)
    {
        CreateText("PageNumber", pageNavigation, 830f, 792f, 170f, 54f,
            "01 / 05", 25f, "6C8390", FontStyles.Normal);
        CreateDisabledNavigationButton("PreviousPage", pageNavigation, 100f, 790f, "‹");
        CreateDisabledNavigationButton("NextPage", pageNavigation, 952f, 790f, "›");
    }

    private static void CreateDisabledNavigationButton(
        string name, Transform parent, float x, float y, string symbol)
    {
        RectTransform root = CreateRect(name, parent, x, y, 52f, 52f);
        Image target = CreateImage("Target", root, 0f, 0f, 52f, 52f, "FFFFFF", 0f);
        target.raycastTarget = true;
        Button button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        button.transition = Selectable.Transition.None;
        button.interactable = false;

        TextMeshProUGUI label =
            CreateText("Icon", root, 0f, -2f, 52f, 52f,
                symbol, 35f, "9CAAA7", FontStyles.Normal);
        label.alignment = TextAlignmentOptions.Center;
    }

    private static void ConfigureController(
        NotebookToggleController controller,
        RectTransform notebookRoot,
        InputActionReference actionReference,
        Scene scene)
    {
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("notebookRoot").objectReferenceValue = notebookRoot.gameObject;
        serialized.FindProperty("notebookToggleAction").objectReferenceValue = actionReference;

        SerializedProperty blockers = serialized.FindProperty("blockingModals");
        string[] blockerNames =
        {
            "DeductionUnlockPrompt",
            "DeductionPanel",
            "ConfirmationPanel",
            "EndingRoot",
            "ResultPanel"
        };

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in allTransforms)
        {
            if (candidate.gameObject.scene != scene ||
                Array.IndexOf(blockerNames, candidate.name) < 0)
            {
                continue;
            }

            int index = blockers.arraySize;
            blockers.InsertArrayElementAtIndex(index);
            blockers.GetArrayElementAtIndex(index).objectReferenceValue = candidate.gameObject;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Camera FindMainCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera candidate in root.GetComponentsInChildren<Camera>(true))
            {
                if (candidate.CompareTag("MainCamera"))
                    return candidate;
            }
        }

        return null;
    }

    private static void LoadRequiredAssets(Camera overlayCamera)
    {
        overlayLayer = LayerMask.NameToLayer("XR_UI_Overlay");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        uiMaterial = AssetDatabase.LoadAssetAtPath<Material>(UiMaterialPath);
        textMaterial = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);

        AssetDatabase.ImportAsset(WornEdgesPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(WornEdgesPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        wornEdgesTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WornEdgesPath);
        if (overlayCamera == null || overlayLayer < 0 || font == null || uiMaterial == null ||
            textMaterial == null || wornEdgesTexture == null)
        {
            throw new InvalidOperationException(
                "Notebook prerequisites are missing: XRUIOverlayCamera, XR_UI_Overlay layer, " +
                "font, overlay materials, or Figma worn-edge texture.");
        }
    }

    private static InputActionReference GetOrCreateToggleAction()
    {
        InputActionAsset existingAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionPath);
        if (existingAsset != null)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ActionPath))
            {
                if (asset is InputActionReference reference &&
                    reference.action != null &&
                    reference.action.name == "NotebookToggle")
                {
                    return reference;
                }
            }

            throw new InvalidOperationException(
                "NotebookInput.asset exists but its NotebookToggle reference is missing.");
        }

        InputActionAsset actionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
        actionAsset.name = "NotebookInput";
        InputActionMap map = new InputActionMap("Notebook");
        InputAction action = map.AddAction("NotebookToggle", InputActionType.Button);
        action.AddBinding("<XRController>{LeftHand}/primaryButton");
        action.AddBinding("<Keyboard>/n");
        actionAsset.AddActionMap(map);

        AssetDatabase.CreateAsset(actionAsset, ActionPath);
        InputActionReference actionReference = InputActionReference.Create(action);
        actionReference.name = "NotebookToggle";
        AssetDatabase.AddObjectToAsset(actionReference, actionAsset);
        EditorUtility.SetDirty(actionAsset);
        AssetDatabase.SaveAssets();
        return actionReference;
    }

    private static RectTransform CreateRect(
        string name, Transform parent, float x, float y, float width, float height)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = overlayLayer;
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static Image CreateImage(
        string name, Transform parent, float x, float y, float width, float height,
        string hex, float alpha = 1f)
    {
        RectTransform rect = CreateRect(name, parent, x, y, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        Color color = ParseColor(hex);
        color.a *= alpha;
        image.color = color;
        image.material = uiMaterial;
        image.raycastTarget = false;
        return image;
    }

    private static RawImage CreateRawImage(
        string name, Transform parent, float x, float y, float width, float height,
        Texture texture, Color color)
    {
        RectTransform rect = CreateRect(name, parent, x, y, width, height);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = color;
        image.material = uiMaterial;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string name, Transform parent, float x, float y, float width, float height,
        string value, float fontSize, string colorHex, FontStyles style,
        float lineSpacing = 1f)
    {
        RectTransform rect = CreateRect(name, parent, x, y, width, height);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSharedMaterial = textMaterial;
        text.text = value;
        text.fontSize = fontSize;
        text.color = ParseColor(colorHex);
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.lineSpacing = (lineSpacing - 1f) * 100f;
        return text;
    }

    private static Color ParseColor(string hex)
    {
        if (!ColorUtility.TryParseHtmlString("#" + hex, out Color color))
            throw new ArgumentException($"Invalid color: {hex}");
        return color;
    }
}
