using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// One-shot editor operation for Figma frames 111:2 and 112:2.
/// Run with -executeMethod DeductionUIFigmaApplier.Apply.
/// </summary>
public static class DeductionUIFigmaApplier
{
    private const string PrefabPath = "Assets/_Project/Prefabs/UI/DeductionCanvas.prefab";
    private const string ScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string FontPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";
    private const string PortraitRoot = "Assets/_Project/Art/UI/Deduction/Suspects/";

    private static readonly Color Background = Html("#101721");
    private static readonly Color Grid = Html("#2C3746", 0.22f);
    private static readonly Color Panel = Html("#18202B");
    private static readonly Color Card = Html("#19222C");
    private static readonly Color Border = Html("#596777");
    private static readonly Color Divider = Html("#455365");
    private static readonly Color White = Html("#F4F1EF");
    private static readonly Color Secondary = Html("#AAB5C0");
    private static readonly Color Muted = Html("#8998A7");
    private static readonly Color Gold = Html("#C8AA64");
    private static readonly Color GoldButton = Html("#C7AA6D");

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode에서는 DeductionCanvas를 재구성할 수 없습니다.");

        ConfigureSprite(PortraitRoot + "KimMinSu.png");
        ConfigureSprite(PortraitRoot + "LeeSeoYeon.png");
        ConfigureSprite(PortraitRoot + "ParkJinHo.png");

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite kim = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitRoot + "KimMinSu.png");
        Sprite lee = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitRoot + "LeeSeoYeon.png");
        Sprite park = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitRoot + "ParkJinHo.png");
        if (font == null || kim == null || lee == null || park == null)
            throw new InvalidOperationException("Figma 용의자 UI에 필요한 폰트 또는 초상화 에셋이 없습니다.");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            DeductionUIController controller = root.GetComponent<DeductionUIController>();
            if (controller == null)
                throw new InvalidOperationException("DeductionCanvas prefab에 DeductionUIController가 없습니다.");

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(1920f, 1080f);
            DestroyChildren(root.transform);

            GameObject panel = CreateRoot("DeductionPanel", root.transform);
            BuildBackdrop(panel.transform);
            BuildHeader(panel.transform, font);

            CardRefs kimCard = BuildCard(panel.transform, font, kim, 50f, "A", "김민수", "생명공학 연구원", "연구 성과 문제로 갈등");
            CardRefs leeCard = BuildCard(panel.transform, font, lee, 455f, "B", "이서연", "약학 연구원", "과거 실험 보고서 관련");
            CardRefs parkCard = BuildCard(panel.transform, font, park, 860f, "C", "박진호", "연구 총괄 교수", "연구비 집행 의혹");

            TMP_Text currentSelection = BuildCaseOverview(panel.transform, font);
            FooterRefs footer = BuildFooter(panel.transform, font);
            ConfirmationRefs confirmation = BuildConfirmation(panel.transform, font, lee);

            SerializedObject so = new(controller);
            SetReference(so, "deductionPanel", panel);
            SetReference(so, "statusText", footer.Status);
            SetReference(so, "currentSelectionText", currentSelection);
            SetReference(so, "kimMinSuButton", kimCard.Button);
            SetReference(so, "leeSeoYeonButton", leeCard.Button);
            SetReference(so, "parkJinHoButton", parkCard.Button);
            SetReference(so, "kimMinSuSelectionOutline", kimCard.Outline);
            SetReference(so, "leeSeoYeonSelectionOutline", leeCard.Outline);
            SetReference(so, "parkJinHoSelectionOutline", parkCard.Outline);
            SetReference(so, "reviewButton", footer.Review);
            SetReference(so, "confirmButton", footer.Confirm);
            SetReference(so, "confirmationPanel", confirmation.Root);
            SetReference(so, "confirmationCancelButton", confirmation.Cancel);
            SetReference(so, "confirmationAcceptButton", confirmation.Accept);
            SetReference(so, "confirmationPortrait", confirmation.Portrait);
            SetReference(so, "confirmationQuestionText", confirmation.Question);
            SetReference(so, "confirmationSuspectText", confirmation.Suspect);
            SetReference(so, "confirmationRoleText", confirmation.Role);
            SetReference(so, "confirmationClueText", confirmation.Clue);
            SetReference(so, "kimMinSuPortrait", kim);
            SetReference(so, "leeSeoYeonPortrait", lee);
            SetReference(so, "parkJinHoPortrait", park);
            ClearLegacyResultReferences(so);
            so.ApplyModifiedPropertiesWithoutUndo();

            confirmation.Root.SetActive(false);
            panel.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        ApplySceneInstanceSize();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ROOM 404] Figma 최종 추리 선택/확인 UI 적용 완료");
    }

    private static void BuildBackdrop(Transform root)
    {
        CreateBlock("Background", root, 0, 0, 1920, 1080, Background);
        for (int x = 46; x <= 1810; x += 84)
            CreateBlock($"GridV_{x}", root, x, 0, 1, 1080, Grid);
        for (int y = 36; y <= 1044; y += 84)
            CreateBlock($"GridH_{y}", root, 0, y, 1920, 1, Grid);
    }

    private static void BuildHeader(Transform root, TMP_FontAsset font)
    {
        CreateBlock("HeaderSlashGold", root, 44, 27, 31, 11, Html("#C5A95F"));
        CreateBlock("HeaderSlashGray", root, 81, 27, 14, 11, Html("#66727F"));
        CreateText(root, font, "EnglishHeader", "ROOM 404   /   FINAL DEDUCTION", 25, FontStyles.Normal, Html("#D4C6A4"), 110, 14, 740, 53);
        CreateBlock("HeaderAccent", root, 45, 90, 7, 54, Gold);
        CreateText(root, font, "Title", "최종 추리", 64, FontStyles.Bold, White, 80, 73, 390, 84);
        CreateBlock("TitleDivider", root, 490, 84, 2, 56, Html("#5D6974"));
        CreateText(root, font, "Instruction", "수집한 증거와 용의자 진술을 바탕으로 범인을 지목하세요.", 28, FontStyles.Normal, Html("#D6D9DB"), 520, 82, 1030, 58);
        CreateText(root, font, "RoomLogo", "ROOM 404", 40, FontStyles.Bold, Html("#636C78"), 1590, 20, 270, 55, TextAlignmentOptions.Right);
        CreateText(root, font, "RoomLogoSub", "VR CRIME INVESTIGATION", 13, FontStyles.Normal, Html("#717E8D"), 1586, 78, 271, 25, TextAlignmentOptions.Right);
        CreateBlock("HeaderDivider", root, 42, 168, 1836, 2, Divider);
        CreateText(root, font, "SuspectGuide", "SUSPECTS    /    용의자 3명 중 한 명을 선택하세요", 22, FontStyles.Normal, Html("#B2BCC6"), 54, 184, 1120, 50);
    }

    private static CardRefs BuildCard(Transform root, TMP_FontAsset font, Sprite portrait, float x,
        string code, string name, string role, string clue)
    {
        GameObject card = CreatePanel($"Suspect{code}Card", root, x, 238, 388, 599, Panel, Border, true);
        Button button = card.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Html("#DFE7EF");
        colors.pressedColor = Html("#BFC8D2");
        colors.disabledColor = Html("#78818B", 0.72f);
        button.colors = colors;

        Outline selection = card.AddComponent<Outline>();
        selection.effectColor = Gold;
        selection.effectDistance = new Vector2(4, -4);
        selection.useGraphicAlpha = false;
        selection.enabled = false;

        CreatePanel("InnerBackground", card.transform, 7, 7, 374, 585, Card, Html("#283544"));
        GameObject photo = CreateBlock("Portrait", card.transform, 11, 12, 364, 392, Color.white);
        Image image = photo.GetComponent<Image>();
        image.sprite = portrait;
        image.preserveAspect = false;
        CreateBlock("PortraitFade", card.transform, 11, 358, 364, 46, Html("#151D29", 0.82f));
        CreateText(card.transform, font, "SuspectCode", $"용의자 {code}", 20, FontStyles.Bold, Html("#D1B169"), 26, 417, 150, 39);
        CreateText(card.transform, font, "Name", name, 38, FontStyles.Bold, White, 23, 455, 335, 59);
        CreateBlock("NameDivider", card.transform, 23, 521, 334, 1, Html("#50616F"));
        CreateText(card.transform, font, "Role", role, 23, FontStyles.Normal, Html("#D9DFE2"), 24, 529, 340, 40);
        CreateText(card.transform, font, "Clue", clue, 17, FontStyles.Normal, Html("#94A6B2"), 24, 563, 340, 28);
        CreateText(card.transform, font, "Radio", "○", 31, FontStyles.Normal, White, 327, 413, 40, 40, TextAlignmentOptions.Center);
        return new CardRefs(button, selection);
    }

    private static TMP_Text BuildCaseOverview(Transform root, TMP_FontAsset font)
    {
        GameObject box = CreatePanel("CaseOverview", root, 1262, 238, 610, 599, Panel, Html("#677686"));
        CreateText(box.transform, font, "Icon", "▱", 31, FontStyles.Normal, Gold, 25, 18, 58, 49);
        CreateText(box.transform, font, "Title", "사건 개요", 32, FontStyles.Bold, White, 92, 18, 240, 48);
        CreatePanel("RequiredBadge", box.transform, 348, 24, 234, 46, Html("#292A22"), Gold);
        CreateText(box.transform, font, "RequiredText", "✓ 필수 4/4", 22, FontStyles.Normal, Gold, 358, 24, 214, 45, TextAlignmentOptions.Center);
        CreateBlock("HeaderDivider", box.transform, 25, 90, 562, 2, Divider);

        AddInfoRow(box.transform, font, 111, "피해자", "정하린");
        AddInfoRow(box.transform, font, 181, "사망 시간", "2026.10.29  21:41");
        AddInfoRow(box.transform, font, 251, "사망 원인", "두부 외상");
        AddInfoRow(box.transform, font, 321, "쟁점", "사건 현장의 은폐 정황");
        AddInfoRow(box.transform, font, 391, "전체 증거", "6 / 6 확보");
        TMP_Text current = AddInfoRow(box.transform, font, 461, "현재 선택", "없음", Gold);
        CreateBlock("BottomDivider", box.transform, 28, 522, 549, 1, Html("#344353"));
        CreateText(box.transform, font, "EvidenceHint", "필수 증거 확보 후 용의자를 지목할 수 있습니다.", 17, FontStyles.Normal, Muted, 25, 545, 568, 35);
        return current;
    }

    private static TMP_Text AddInfoRow(Transform parent, TMP_FontAsset font, float y, string label, string value, Color? valueColor = null)
    {
        CreateText(parent, font, label + "Label", label, 22, FontStyles.Normal, Secondary, 29, y, 178, 52);
        TMP_Text text = CreateText(parent, font, label + "Value", value, 23, FontStyles.Normal, valueColor ?? White, 209, y, 350, 52);
        if (y < 461)
            CreateBlock(label + "Divider", parent, 28, y + 61, 549, 1, Html("#344353"));
        return text;
    }

    private static FooterRefs BuildFooter(Transform root, TMP_FontAsset font)
    {
        CreateBlock("FooterDivider", root, 42, 867, 1836, 2, Divider);
        Button review = CreateButton("ReviewButton", root, font, "▤   형사 수첩 열기", 50, 917, 417, 108, Html("#222E3A"), White, Html("#B3BFC9"), 31);
        TMP_Text status = CreateText(root, font, "StatusText", "용의자를 선택하면 최종 결론 제출 버튼이 활성화됩니다.", 20, FontStyles.Normal, Muted, 501, 930, 867, 60, TextAlignmentOptions.Center);
        Button confirm = CreateButton("ConfirmButton", root, font, "▣   결론 제출", 1404, 911, 468, 118, GoldButton, Html("#202630"), Html("#DEC48C"), 35);
        return new FooterRefs(review, confirm, status);
    }

    private static ConfirmationRefs BuildConfirmation(Transform root, TMP_FontAsset font, Sprite defaultPortrait)
    {
        GameObject overlay = CreateRoot("ConfirmationPanel", root);
        CreateBlock("Dimmer", overlay.transform, 0, 0, 1920, 1080, Html("#050A11", 0.78f), true);
        GameObject modal = CreatePanel("ConfirmationModal", overlay.transform, 240, 110, 1440, 860, Html("#141D2B"), Html("#8796A5"));

        CreateBlock("GoldDash", modal.transform, 42, 24, 55, 7, Gold);
        CreateText(modal.transform, font, "EnglishHeader", "ROOM 404   /   FINAL DEDUCTION", 20, FontStyles.Normal, Html("#D4C6A4"), 113, 15, 620, 36);
        CreateText(modal.transform, font, "Title", "최종 추리 확인", 48, FontStyles.Bold, White, 68, 72, 600, 72);
        CreateBlock("TopDivider", modal.transform, 49, 162, 1340, 2, Html("#61717E"));
        GameObject prompt = CreatePanel("QuestionBox", modal.transform, 55, 186, 1330, 86, Html("#202B38"), Html("#657588"));
        TMP_Text question = CreateText(prompt.transform, font, "Question", "이서연을 범인으로 지목하시겠습니까?", 34, FontStyles.Bold, White, 24, 10, 1282, 66, TextAlignmentOptions.Center);

        GameObject portraitPanel = CreatePanel("SelectedPortrait", modal.transform, 69, 304, 450, 367, Html("#2B3540"), Html("#8090A0"));
        GameObject portraitObject = CreateBlock("Portrait", portraitPanel.transform, 2, 2, 446, 363, Color.white);
        Image portrait = portraitObject.GetComponent<Image>();
        portrait.sprite = defaultPortrait;
        portrait.preserveAspect = false;

        GameObject details = CreatePanel("SelectedDetails", modal.transform, 546, 304, 817, 367, Html("#192330"), Html("#687789"));
        TMP_Text suspect = AddConfirmRow(details.transform, font, 30, "선택 용의자", "이서연   /   용의자 B", Gold);
        TMP_Text role = AddConfirmRow(details.transform, font, 112, "직업", "약학 연구원", White);
        TMP_Text clue = AddConfirmRow(details.transform, font, 194, "주요 조사 내용", "과거 실험 기록 및 연구 보고서 관련 정황", White);
        AddConfirmRow(details.transform, font, 276, "지목 시 주의", "확정 후에는 다른 용의자를 선택할 수 없습니다.", Html("#E18A87"));

        CreatePanel("EvidenceBadge", modal.transform, 390, 691, 660, 52, Html("#292A22"), Gold);
        CreateText(modal.transform, font, "EvidenceText", "✓ 필수 증거 4/4 확보  ·  전체 6/6", 22, FontStyles.Normal, Gold, 400, 691, 640, 52, TextAlignmentOptions.Center);
        Button cancel = CreateButton("ConfirmationCancelButton", modal.transform, font, "↶   아니요, 다시 추리", 69, 766, 630, 78, Html("#2A3442"), White, Html("#9CA9B5"), 27);
        Button accept = CreateButton("ConfirmationAcceptButton", modal.transform, font, "✓   예, 지목합니다", 733, 765, 630, 78, GoldButton, Html("#202630"), Html("#EBD398"), 27);
        return new ConfirmationRefs(overlay, cancel, accept, portrait, question, suspect, role, clue);
    }

    private static TMP_Text AddConfirmRow(Transform parent, TMP_FontAsset font, float y, string label, string value, Color valueColor)
    {
        CreateText(parent, font, label + "Label", label, 20, FontStyles.Normal, Secondary, 24, y, 200, 44);
        TMP_Text result = CreateText(parent, font, label + "Value", value, 23, FontStyles.Normal, valueColor, 220, y, 560, 44);
        if (y < 276)
            CreateBlock(label + "Divider", parent, 24, y + 58, 769, 1, Html("#344353"));
        return result;
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string label,
        float x, float y, float width, float height, Color background, Color text, Color outline, float size)
    {
        GameObject go = CreatePanel(name, parent, x, y, width, height, background, outline, true);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Html("#FFF1C4");
        colors.pressedColor = Html("#C1B081");
        colors.disabledColor = Html("#70757A", 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        CreateText(go.transform, font, "Label", label, size, FontStyles.Bold, text, 18, 8, width - 36, height - 16, TextAlignmentOptions.Center);
        return button;
    }

    private static GameObject CreatePanel(string name, Transform parent, float x, float y, float w, float h, Color color, Color outline, bool raycast = false)
    {
        GameObject go = CreateBlock(name, parent, x, y, w, h, color, raycast);
        Outline effect = go.AddComponent<Outline>();
        effect.effectColor = outline;
        effect.effectDistance = new Vector2(1.4f, -1.4f);
        effect.useGraphicAlpha = true;
        return go;
    }

    private static GameObject CreateBlock(string name, Transform parent, float x, float y, float w, float h, Color color, bool raycast = false)
    {
        GameObject go = CreateUIObject(name, parent);
        SetTopLeft(go.GetComponent<RectTransform>(), x, y, w, h);
        Image image = go.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = raycast;
        return go;
    }

    private static TMP_Text CreateText(Transform parent, TMP_FontAsset font, string name, string value, float size,
        FontStyles style, Color color, float x, float y, float w, float h, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        GameObject go = CreateUIObject(name, parent);
        SetTopLeft(go.GetComponent<RectTransform>(), x, y, w, h);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static void ConfigureSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"이미지 Importer를 찾을 수 없습니다: {path}");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static void ApplySceneInstanceSize()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject instance = FindSceneObject(scene, "DeductionCanvas");
        if (instance == null)
            throw new InvalidOperationException("VR_Test에서 DeductionCanvas instance를 찾을 수 없습니다.");
        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920, 1080);
        rect.localPosition = new Vector3(0, 0, 1.5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.001f;
        EditorUtility.SetDirty(rect);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("VR_Test scene 저장에 실패했습니다.");
    }

    private static void ClearLegacyResultReferences(SerializedObject so)
    {
        string[] names = { "resultPanel", "resultBackground", "resultIconText", "resultTitleText", "resultDescriptionText", "resultSupportText" };
        foreach (string name in names)
            SetReference(so, name, null);
    }

    private static void SetReference(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"DeductionUIController field를 찾을 수 없습니다: {propertyName}");
        property.objectReferenceValue = value;
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name)
                    return item.gameObject;
        return null;
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

    private static void DestroyChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static Color Html(string value, float alpha = 1f)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        color.a = alpha;
        return color;
    }

    private readonly struct CardRefs
    {
        public CardRefs(Button button, Outline outline) { Button = button; Outline = outline; }
        public Button Button { get; }
        public Outline Outline { get; }
    }

    private readonly struct FooterRefs
    {
        public FooterRefs(Button review, Button confirm, TMP_Text status) { Review = review; Confirm = confirm; Status = status; }
        public Button Review { get; }
        public Button Confirm { get; }
        public TMP_Text Status { get; }
    }

    private readonly struct ConfirmationRefs
    {
        public ConfirmationRefs(GameObject root, Button cancel, Button accept, Image portrait, TMP_Text question, TMP_Text suspect, TMP_Text role, TMP_Text clue)
        { Root = root; Cancel = cancel; Accept = accept; Portrait = portrait; Question = question; Suspect = suspect; Role = role; Clue = clue; }
        public GameObject Root { get; }
        public Button Cancel { get; }
        public Button Accept { get; }
        public Image Portrait { get; }
        public TMP_Text Question { get; }
        public TMP_Text Suspect { get; }
        public TMP_Text Role { get; }
        public TMP_Text Clue { get; }
    }
}
