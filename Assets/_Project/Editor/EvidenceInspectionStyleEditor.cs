using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class EvidenceInspectionStyleEditor
{
    private const string MenuPath = "ROOM 404/Test Tools/Apply Inspection UI Style";
    private const string VrTestScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string FontAssetPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";

    [MenuItem(MenuPath)]
    private static void ApplyInspectionStyle()
    {
        if (!CanRunInCurrentScene(showMessage: true))
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject canvasObject = EditorUIUtility.FindSceneObject(activeScene, "EvidenceInspectionCanvas");
        GameObject panelObject = EditorUIUtility.FindSceneObject(activeScene, "InspectionPanel");
        TMP_Text idText = FindComponentByName<TMP_Text>(activeScene, "EvidenceIDText");
        TMP_Text nameText = FindComponentByName<TMP_Text>(activeScene, "EvidenceNameText");
        TMP_Text descriptionText = FindComponentByName<TMP_Text>(activeScene, "DescriptionText");

        if (canvasObject == null || panelObject == null || idText == null || nameText == null || descriptionText == null)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 Test Tools",
                "기존 Evidence Inspection UI의 필수 오브젝트를 찾을 수 없습니다.",
                "확인");
            return;
        }

        TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (koreanFont == null)
        {
            Debug.LogWarning(
                $"[ROOM 404] 한글 TMP Font Asset을 찾을 수 없습니다: {FontAssetPath}");
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Evidence Inspection UI Style");

        var canvasRect = (RectTransform)canvasObject.transform;
        Undo.RecordObject(canvasRect, "Resize inspection canvas");
        canvasRect.sizeDelta = new Vector2(820f, 280f);

        var panelRect = (RectTransform)panelObject.transform;
        Undo.RecordObject(panelRect, "Layout inspection panel");
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
            Undo.RecordObject(panelImage, "Style inspection panel");
            panelImage.color = new Color(0.035f, 0.075f, 0.13f, 0.95f);
            panelImage.raycastTarget = false;
        }

        TMP_Text headerText = GetOrCreateText(
            panelObject.transform,
            "InspectionHeaderText",
            "OBJECT INSPECTION",
            koreanFont);
        TMP_Text hintText = GetOrCreateText(
            panelObject.transform,
            "InspectionHintText",
            "Trigger를 눌러 증거 수집",
            koreanFont);

        GameObject badgeObject = GetOrCreateBadge(panelObject.transform);
        if (idText.transform.parent != badgeObject.transform)
            Undo.SetTransformParent(idText.transform, badgeObject.transform, "Move evidence ID into badge");

        ConfigureText(
            headerText,
            "OBJECT INSPECTION",
            koreanFont,
            22f,
            FontStyles.Normal,
            new Color(0.42f, 0.82f, 0.96f, 1f),
            TextAlignmentOptions.MidlineLeft,
            new Vector2(24f, -18f),
            new Vector2(760f, 34f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));

        ConfigureText(
            idText,
            "E01",
            koreanFont,
            23f,
            FontStyles.Normal,
            Color.white,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            Vector2.one,
            stretch: true);

        ConfigureText(
            nameText,
            "세정제 용기",
            koreanFont,
            40f,
            FontStyles.Bold,
            Color.white,
            TextAlignmentOptions.MidlineLeft,
            new Vector2(122f, -67f),
            new Vector2(660f, 56f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));

        ConfigureText(
            descriptionText,
            "사건 현장에서 발견된 세정제 용기.",
            koreanFont,
            27f,
            FontStyles.Normal,
            new Color(0.9f, 0.93f, 0.97f, 1f),
            TextAlignmentOptions.TopLeft,
            new Vector2(24f, -130f),
            new Vector2(760f, 72f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            wrap: true);

        ConfigureText(
            hintText,
            "Trigger를 눌러 증거 수집",
            koreanFont,
            22f,
            FontStyles.Normal,
            new Color(0.58f, 0.69f, 0.82f, 1f),
            TextAlignmentOptions.MidlineLeft,
            new Vector2(24f, 22f),
            new Vector2(760f, 38f),
            Vector2.zero,
            Vector2.zero);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(activeScene);
        Selection.activeGameObject = canvasObject;

        Debug.Log(
            "[ROOM 404] Evidence Inspection UI 스타일을 적용했습니다. 기존 Controller 참조는 유지됩니다.",
            canvasObject);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateApplyInspectionStyle()
    {
        return CanRunInCurrentScene(showMessage: false);
    }

    private static bool CanRunInCurrentScene(bool showMessage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return false;

        Scene scene = SceneManager.GetActiveScene();
        bool valid = scene.IsValid()
            && scene.isLoaded
            && string.Equals(scene.path, VrTestScenePath, StringComparison.Ordinal);

        if (!valid && showMessage)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 Test Tools",
                $"이 메뉴는 '{VrTestScenePath}' Scene에서만 실행할 수 있습니다.",
                "확인");
        }

        return valid;
    }

    private static GameObject GetOrCreateBadge(Transform parent)
    {
        Transform existing = parent.Find("EvidenceIDBadge");
        GameObject badgeObject;

        if (existing != null)
        {
            badgeObject = existing.gameObject;
        }
        else
        {
            badgeObject = new GameObject(
                "EvidenceIDBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer));
            Undo.RegisterCreatedObjectUndo(badgeObject, "Create evidence ID badge");
            Undo.SetTransformParent(badgeObject.transform, parent, "Parent evidence ID badge");
            Undo.AddComponent<Image>(badgeObject);
        }

        var badgeRect = (RectTransform)badgeObject.transform;
        Undo.RecordObject(badgeRect, "Layout evidence ID badge");
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0f, 1f);
        badgeRect.anchoredPosition3D = new Vector3(24f, -50f, 0f);
        badgeRect.sizeDelta = new Vector2(82f, 42f);
        badgeRect.localRotation = Quaternion.identity;
        badgeRect.localScale = Vector3.one;

        Image badgeImage = badgeObject.GetComponent<Image>();
        if (badgeImage == null)
            badgeImage = Undo.AddComponent<Image>(badgeObject);

        Undo.RecordObject(badgeImage, "Style evidence ID badge");
        badgeImage.color = new Color(0.18f, 0.23f, 0.32f, 0.96f);
        badgeImage.raycastTarget = false;
        return badgeObject;
    }

    private static TMP_Text GetOrCreateText(
        Transform parent,
        string objectName,
        string initialText,
        TMP_FontAsset font)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            TMP_Text existingText = existing.GetComponent<TMP_Text>();
            return existingText != null
                ? existingText
                : Undo.AddComponent<TextMeshProUGUI>(existing.gameObject);
        }

        var textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer));
        Undo.RegisterCreatedObjectUndo(textObject, $"Create {objectName}");
        Undo.SetTransformParent(textObject.transform, parent, $"Parent {objectName}");

        TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(textObject);
        text.text = initialText;

        if (font != null)
            text.font = font;

        return text;
    }

    private static void ConfigureText(
        TMP_Text text,
        string fallbackText,
        TMP_FontAsset font,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 anchoredPosition,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool stretch = false,
        bool wrap = false)
    {
        Undo.RecordObject(text, $"Style {text.name}");

        if (string.IsNullOrEmpty(text.text))
            text.text = fallbackText;

        if (font != null)
            text.font = font;

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        Undo.RecordObject(rect, $"Layout {text.name}");
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        if (stretch)
        {
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition3D = Vector3.zero;
        }
        else
        {
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
            rect.sizeDelta = size;
        }
    }

    private static T FindComponentByName<T>(Scene scene, string objectName) where T : Component
    {
        GameObject gameObject = EditorUIUtility.FindSceneObject(scene, objectName);
        return gameObject != null ? gameObject.GetComponent<T>() : null;
    }
}
