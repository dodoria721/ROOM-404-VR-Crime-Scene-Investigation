using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class EvidenceNotificationSetupEditor
{
    private const string MenuPath = "ROOM 404/Test Tools/Create Evidence Notification UI";
    private const string RepairMenuPath = "ROOM 404/Test Tools/Repair Evidence Notification UI";
    private const string VrTestScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string FontAssetPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";
    private const string CanvasObjectName = "EvidenceNotificationCanvas";

    [MenuItem(MenuPath)]
    private static void CreateEvidenceNotificationUI()
    {
        if (!CanRunInCurrentScene(showMessage: true))
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject existingCanvas = FindGameObjectInScene(activeScene, CanvasObjectName);

        if (existingCanvas != null)
        {
            Selection.activeGameObject = existingCanvas;
            Debug.Log(
                $"[ROOM 404] '{CanvasObjectName}'가 이미 존재하여 변경하지 않았습니다.",
                existingCanvas);
            return;
        }

        Transform mainCamera = FindMainCameraTransform(activeScene);
        if (mainCamera == null)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 Test Tools",
                "XR Origin (VR)/Camera Offset/Main Camera 계층을 찾을 수 없어 알림 UI를 생성하지 않았습니다.",
                "확인");
            return;
        }

        TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (koreanFont == null)
        {
            Debug.LogWarning(
                $"[ROOM 404] 한글 TMP Font Asset을 찾을 수 없습니다: {FontAssetPath}. " +
                "UI 생성 후 모든 TMP Text에 Noto Sans KR Font Asset을 수동으로 연결하세요.");
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Evidence Notification UI");

        GameObject canvasObject = CreateCanvas(mainCamera);
        GameObject panelObject = CreatePanel(canvasObject.transform);

        TextMeshProUGUI titleText = CreateText(
            panelObject.transform,
            "NotificationTitleText",
            "새로운 증거 확보",
            koreanFont,
            34f,
            new Vector2(0f, 72f),
            new Vector2(620f, 48f),
            FontStyles.Bold);

        TextMeshProUGUI idText = CreateText(
            panelObject.transform,
            "EvidenceIDText",
            "E01",
            koreanFont,
            28f,
            new Vector2(-230f, 18f),
            new Vector2(140f, 42f),
            FontStyles.Bold);

        TextMeshProUGUI nameText = CreateText(
            panelObject.transform,
            "EvidenceNameText",
            "세정제 용기",
            koreanFont,
            28f,
            new Vector2(70f, 18f),
            new Vector2(430f, 42f),
            FontStyles.Normal);

        TextMeshProUGUI messageText = CreateText(
            panelObject.transform,
            "NotificationMessageText",
            "증거가 수사 기록에 등록되었습니다.",
            koreanFont,
            22f,
            new Vector2(0f, -58f),
            new Vector2(620f, 40f),
            FontStyles.Normal);

        EvidenceNotificationUIController controller =
            Undo.AddComponent<EvidenceNotificationUIController>(canvasObject);
        ConnectControllerReferences(
            controller,
            panelObject,
            titleText,
            idText,
            nameText,
            messageText);

        // Controller는 활성 Canvas에 남겨 두고 실제 표시 패널만 숨긴다.
        panelObject.SetActive(false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(activeScene);
        Selection.activeGameObject = canvasObject;

        Debug.Log(
            "[ROOM 404] Evidence Notification UI를 생성했습니다. Scene은 자동 저장되지 않았습니다.",
            canvasObject);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateCreateEvidenceNotificationUI()
    {
        return CanRunInCurrentScene(showMessage: false);
    }

    [MenuItem(RepairMenuPath)]
    private static void RepairEvidenceNotificationUI()
    {
        if (!CanRunInCurrentScene(showMessage: true))
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject canvasObject = FindGameObjectInScene(activeScene, CanvasObjectName);
        Transform mainCamera = FindMainCameraTransform(activeScene);

        if (canvasObject == null || mainCamera == null)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 Test Tools",
                "EvidenceNotificationCanvas 또는 Main Camera를 찾을 수 없습니다.",
                "확인");
            return;
        }

        GameObject panelObject = FindChildByName(canvasObject.transform, "NotificationPanel");
        if (panelObject == null)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 Test Tools",
                "NotificationPanel을 찾을 수 없습니다.",
                "확인");
            return;
        }

        TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Repair Evidence Notification UI");

        Undo.SetTransformParent(canvasObject.transform, mainCamera, "Parent notification canvas to camera");
        Undo.RecordObject(canvasObject.transform, "Repair notification canvas transform");

        var canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.localPosition = new Vector3(0f, 0.25f, 1.1f);
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = new Vector3(0.0012f, 0.0012f, 0.0012f);
        canvasRect.sizeDelta = new Vector2(700f, 220f);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas != null)
        {
            Undo.RecordObject(canvas, "Repair notification canvas settings");
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCamera.GetComponent<Camera>();
            canvas.sortingOrder = 10;
        }

        Undo.RecordObject(panelObject.transform, "Repair notification panel transform");
        var panelRect = (RectTransform)panelObject.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.anchoredPosition3D = Vector3.zero;
        panelRect.localRotation = Quaternion.identity;
        panelRect.localScale = Vector3.one;

        Image panelImage = panelObject.GetComponent<Image>();
        if (panelImage != null)
        {
            Undo.RecordObject(panelImage, "Repair notification panel style");
            panelImage.color = new Color(0.025f, 0.075f, 0.12f, 0.94f);
            panelImage.raycastTarget = false;
        }

        foreach (TMP_Text text in panelObject.GetComponentsInChildren<TMP_Text>(includeInactive: true))
        {
            Undo.RecordObject(text, "Repair notification text");
            text.color = Color.white;
            text.raycastTarget = false;

            if (koreanFont != null)
                text.font = koreanFont;

            Vector3 anchoredPosition = text.rectTransform.anchoredPosition3D;
            anchoredPosition.z = 0f;
            text.rectTransform.anchoredPosition3D = anchoredPosition;
            text.rectTransform.localRotation = Quaternion.identity;
            text.rectTransform.localScale = Vector3.one;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(activeScene);
        Selection.activeGameObject = canvasObject;

        Debug.Log(
            "[ROOM 404] Evidence Notification UI 배치를 복구했습니다. Scene은 자동 저장되지 않았습니다.",
            canvasObject);
    }

    [MenuItem(RepairMenuPath, true)]
    private static bool ValidateRepairEvidenceNotificationUI()
    {
        return CanRunInCurrentScene(showMessage: false);
    }

    private static bool CanRunInCurrentScene(bool showMessage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (showMessage)
            {
                EditorUtility.DisplayDialog(
                    "ROOM 404 Test Tools",
                    "Play Mode에서는 Evidence Notification UI를 생성할 수 없습니다.",
                    "확인");
            }

            return false;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        bool isVrTestScene = activeScene.IsValid()
            && activeScene.isLoaded
            && string.Equals(activeScene.path, VrTestScenePath, StringComparison.Ordinal);

        if (!isVrTestScene && showMessage)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 Test Tools",
                $"이 메뉴는 '{VrTestScenePath}' Scene에서만 실행할 수 있습니다.",
                "확인");
        }

        return isVrTestScene;
    }

    private static GameObject CreateCanvas(Transform mainCamera)
    {
        var canvasObject = new GameObject(CanvasObjectName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create evidence notification canvas");
        Undo.SetTransformParent(canvasObject.transform, mainCamera, "Parent evidence notification canvas");

        var rectTransform = (RectTransform)canvasObject.transform;
        rectTransform.localPosition = new Vector3(0f, 0.25f, 1.1f);
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = new Vector3(0.0012f, 0.0012f, 0.0012f);
        rectTransform.sizeDelta = new Vector2(700f, 220f);

        Canvas canvas = Undo.AddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = mainCamera.GetComponent<Camera>();
        canvas.sortingOrder = 10;

        CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 1f;

        SetLayerRecursively(canvasObject, LayerMask.NameToLayer("UI"));
        return canvasObject;
    }

    private static GameObject CreatePanel(Transform parent)
    {
        var panelObject = new GameObject(
            "NotificationPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer));
        Undo.RegisterCreatedObjectUndo(panelObject, "Create notification panel");
        Undo.SetTransformParent(panelObject.transform, parent, "Parent notification panel");

        var rectTransform = (RectTransform)panelObject.transform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition3D = Vector3.zero;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;

        Image background = Undo.AddComponent<Image>(panelObject);
        background.color = new Color(0.025f, 0.075f, 0.12f, 0.92f);
        background.raycastTarget = false;

        SetLayerRecursively(panelObject, LayerMask.NameToLayer("UI"));
        return panelObject;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        string text,
        TMP_FontAsset font,
        float fontSize,
        Vector2 anchoredPosition,
        Vector2 size,
        FontStyles fontStyle)
    {
        var textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer));
        Undo.RegisterCreatedObjectUndo(textObject, $"Create {objectName}");
        Undo.SetTransformParent(textObject.transform, parent, $"Parent {objectName}");

        var rectTransform = (RectTransform)textObject.transform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition3D = new Vector3(
            anchoredPosition.x,
            anchoredPosition.y,
            0f);
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;

        TextMeshProUGUI tmpText = Undo.AddComponent<TextMeshProUGUI>(textObject);
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.fontStyle = fontStyle;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
        tmpText.textWrappingMode = TextWrappingModes.NoWrap;
        tmpText.raycastTarget = false;

        if (font != null)
            tmpText.font = font;

        SetLayerRecursively(textObject, LayerMask.NameToLayer("UI"));
        return tmpText;
    }

    private static void ConnectControllerReferences(
        EvidenceNotificationUIController controller,
        GameObject panelObject,
        TMP_Text titleText,
        TMP_Text idText,
        TMP_Text nameText,
        TMP_Text messageText)
    {
        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("notificationPanel").objectReferenceValue = panelObject;
        serializedController.FindProperty("notificationTitleText").objectReferenceValue = titleText;
        serializedController.FindProperty("evidenceIdText").objectReferenceValue = idText;
        serializedController.FindProperty("evidenceNameText").objectReferenceValue = nameText;
        serializedController.FindProperty("notificationMessageText").objectReferenceValue = messageText;
        serializedController.FindProperty("displayDuration").floatValue = 2.5f;
        serializedController.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
    }

    private static Transform FindMainCameraTransform(Scene scene)
    {
        GameObject xrOrigin = FindGameObjectInScene(scene, "XR Origin (VR)");
        if (xrOrigin == null)
            return null;

        return xrOrigin.transform.Find("Camera Offset/Main Camera");
    }

    private static GameObject FindGameObjectInScene(Scene scene, string objectName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            foreach (Transform child in rootObject.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (string.Equals(child.name, objectName, StringComparison.Ordinal))
                    return child.gameObject;
            }
        }

        return null;
    }

    private static GameObject FindChildByName(Transform parent, string objectName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (string.Equals(child.name, objectName, StringComparison.Ordinal))
                return child.gameObject;
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
}
