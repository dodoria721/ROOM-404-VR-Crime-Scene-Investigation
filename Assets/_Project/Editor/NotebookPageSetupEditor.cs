using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class NotebookPageSetupEditor
{
    const string ScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    const string FontPath = "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset";
    const string UiMaterialPath = "Assets/_Project/Materials/UI/M_UI_AlwaysOnTop.mat";
    const string TextMaterialPath = "Assets/_Project/Materials/UI/M_NotoSansKR_Overlay.mat";
    const string AssetRoot = "Assets/_Project/UI/Notebook/Pages/";
    const string DeductionEntryName = "DeductionReentryFooter";
    const string DeductionEntryMenu = "ROOM 404/UI/Configure Notebook Deduction Entry";

    static readonly string[] EvidenceIds = { "E01", "E02", "E03", "E04", "E05", "E06" };
    static readonly string[] EvidenceNames =
    {
        "세정제 용기", "책상 모서리\n충돌 흔적", "정하린 개인 하드디스크",
        "박진호 연구비 문서", "김민수 협박 메시지", "정하린 휴대폰 약속 기록"
    };

    static int layer;
    static TMP_FontAsset font;
    static Material uiMaterial, textMaterial;
    static Texture2D victim, autopsy, fingerprint, e01, e02, e03, locked;
    static Texture2D[] suspects, evidenceDetailCards;

    [MenuItem("ROOM 404/UI/Configure Notebook Pages")]
    public static void ConfigureNotebookPages()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 실행해 주세요.");

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        GameObject canvas = FindCanvas(scene);
        if (canvas == null) throw new InvalidOperationException("NotebookCanvas를 찾지 못했습니다.");

        ConfigureNotebookPagesInScene(scene, canvas);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("VR_Test 저장 실패");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = canvas;
        Debug.Log("[Notebook] Figma 기반 02~05 페이지를 적용했습니다.", canvas);
    }

    public static void ConfigureNotebookPagesInScene(Scene scene, GameObject canvas)
    {
        LoadAssets();
        Transform content = canvas.transform.Find("NotebookRoot/NotebookContent");
        Transform tabs = canvas.transform.Find("NotebookRoot/TabNavigation");
        Transform nav = canvas.transform.Find("NotebookRoot/PageNavigation");
        if (content == null || tabs == null || nav == null)
            throw new InvalidOperationException("NotebookCanvas 계층 구조가 불완전합니다.");

        GameObject casePage = Need(content, "CaseOverviewPage").gameObject;
        GameObject victimPage = Page(content, "VictimPage");
        GameObject suspectPage = Page(content, "SuspectPage");
        GameObject evidencePage = Page(content, "EvidencePage");
        GameObject memoPage = Page(content, "MemoPage");

        Rebuild(victimPage.transform, BuildVictim);
        Rebuild(suspectPage.transform, BuildSuspects);
        Rebuild(evidencePage.transform, p => BuildEvidence(p, evidencePage, nav.gameObject));
        Rebuild(memoPage.transform, BuildMemo, memoPage.transform.Find(DeductionEntryName));

        Button[] buttons = new Button[5];
        Image[] graphics = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            Transform target = Need(tabs, $"Tab_{i + 1:00}/TabGraphic");
            buttons[i] = target.GetComponent<Button>();
            graphics[i] = target.GetComponent<Image>();
            if (buttons[i] == null || graphics[i] == null) throw new InvalidOperationException("탭 참조 누락");
            buttons[i].interactable = true;
            buttons[i].transition = Selectable.Transition.None;
            graphics[i].raycastTarget = true;
        }

        Button prev = Need(nav, "PreviousPage/Target").GetComponent<Button>();
        Button next = Need(nav, "NextPage/Target").GetComponent<Button>();
        TMP_Text number = Need(nav, "PageNumber").GetComponent<TMP_Text>();
        NotebookPageController controller = canvas.GetComponent<NotebookPageController>() ??
                                            Undo.AddComponent<NotebookPageController>(canvas);
        SerializedObject so = new SerializedObject(controller);
        Obj(so, "caseOverviewPage", casePage); Obj(so, "victimPage", victimPage);
        Obj(so, "suspectPage", suspectPage); Obj(so, "evidencePage", evidencePage); Obj(so, "memoPage", memoPage);
        Obj(so, "caseTabButton", buttons[0]); Obj(so, "victimTabButton", buttons[1]);
        Obj(so, "suspectTabButton", buttons[2]); Obj(so, "evidenceTabButton", buttons[3]);
        Obj(so, "memoTabButton", buttons[4]); Obj(so, "previousPageButton", prev);
        Obj(so, "nextPageButton", next); Obj(so, "pageNumberText", number);
        Array(so, "tabSelectionGraphics", graphics);
        Color[] colors = { C("D6C9B2"), C("B4C2CA"), C("9CB2A7"), C("BFA39A"), C("AAA4BD") };
        SerializedProperty cp = so.FindProperty("tabDefaultColors"); cp.arraySize = 5;
        for (int i = 0; i < 5; i++) cp.GetArrayElementAtIndex(i).colorValue = colors[i];
        so.FindProperty("selectedTabColor").colorValue = C("CDB66F");
        so.ApplyModifiedPropertiesWithoutUndo();

        casePage.SetActive(true); victimPage.SetActive(false); suspectPage.SetActive(false);
        evidencePage.SetActive(false); memoPage.SetActive(false);
        number.text = "01 / 05"; prev.interactable = false; next.interactable = true;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static void BuildVictim(Transform p)
    {
        Header(p, "피해자 / 부검 결과", 308);
        Text("Confidential", p, 790, 42, 210, 34, "CONFIDENTIAL", 20, "9B4438", FontStyles.Bold).alignment = TextAlignmentOptions.TopRight;
        RawImage portrait = Raw("VictimPortrait", p, 102, 190, 228, 332, victim); Border(portrait.gameObject, "8C795E", 2);
        Text("Name", p, 370, 188, 420, 48, "정하린", 34, "2B383D", FontStyles.Bold);
        Text("Role", p, 370, 238, 420, 40, "약학 연구원 / 27세", 23, "687074", FontStyles.Normal);
        Field(p, 370, 316, "발견 시각", "2026.10.29 21:41경");
        Field(p, 370, 386, "발견 장소", "대학교 연구실 404호");
        Field(p, 370, 456, "추정 사인", "두부 외상에 의한 사망");
        Text("SectionTitle", p, 100, 554, 600, 42, "부검 및 현장 관찰 결과", 27, "2B383D", FontStyles.Bold);
        string[] findings =
        {
            "후두부에 강한 충격으로 인한 치명적인 손상이 확인됨",
            "손상 형태는 연구실 책상 모서리와의 충돌 가능성을 시사함",
            "단순 사고인지 타인의 개입이 있었는지 추가 검증이 필요함"
        };
        for (int i = 0; i < 3; i++)
        {
            Image($"Bullet{i}", p, 103, 622 + i * 54, 10, 10, "C9A551");
            Text($"Finding{i}", p, 128, 607 + i * 54, 720, 44, findings[i], 22, "2B383D", FontStyles.Normal);
        }
        Raw("AutopsySketch", p, 860, 588, 126, 172, autopsy).color = new Color(1, 1, 1, .78f);
    }

    static void BuildSuspects(Transform p)
    {
        Header(p, "용의자 / 알리바이", 280);
        Text("StatementLabel", p, 780, 42, 220, 34, "알리바이 진술", 22, "6C8390", FontStyles.Bold).alignment = TextAlignmentOptions.TopRight;
        string[] names = { "김민수", "이서연", "박진호" };
        string[] roles = { "25세 · 생명공학 연구원", "27세 · 약학 연구원", "45세 · 연구 총괄 지도교수" };
        string[] statements =
        {
            "연구동 실험 준비실에서\n혼자 자료를 정리하고 있었다.",
            "자취방에서 몸이 좋지 않아\n휴식을 취하고 있었다.",
            "개인 연구실에서 연구과제\n서류를 검토하고 있었다."
        };
        for (int i = 0; i < 3; i++)
        {
            float y = 184 + i * 192;
            RawImage photo = Raw($"Portrait{i}", p, 100, y + 4, 112, 148, suspects[i]); Border(photo.gameObject, "8C795E", 1.5f);
            Text($"Name{i}", p, 240, y, 400, 43, names[i], 30, "2B383D", FontStyles.Bold);
            Text($"Role{i}", p, 240, y + 46, 450, 36, roles[i], 22, "687074", FontStyles.Normal);
            Text($"Statement{i}", p, 240, y + 84, 590, 72, statements[i], 25, "2B383D", FontStyles.Normal);
            TMP_Text status = Text($"Status{i}", p, 840, y + 4, 160, 36, "미검증", 21, "9B4438", FontStyles.Bold);
            status.alignment = TextAlignmentOptions.TopRight;
            Image($"Rule{i}", p, 100, y + 170, 900, 1, "BCAA8C");
        }
    }

    static void BuildEvidence(Transform p, GameObject page, GameObject pageNavigation)
    {
        RectTransform grid = Rect("EvidenceGrid", p, 0, 0, 1060, 856);
        Header(grid, "증거 기록", 140);
        Text("CollectionTitle", grid, 620, 9, 380, 26, "수집 현황", 18, "2B383D", FontStyles.Bold);
        Raw("Fingerprint", grid, 502, 40, 58, 76, fingerprint).color = new Color(1, 1, 1, .62f);
        TMP_Text collectedCount = Status(grid, "CollectedStatus", 38, "확보한 증거", "0 / 6");
        TMP_Text requiredCount = Status(grid, "RequiredStatus", 92, "필수 증거", "0 / 4");
        GameObject[] collected = new GameObject[6], hidden = new GameObject[6];
        TMP_Text[] names = new TMP_Text[6];
        Button[] cardButtons = new Button[6];
        for (int i = 0; i < 6; i++)
        {
            float x = 100 + i % 3 * 308, y = i < 3 ? 194 : 446;
            Image card = Image("EvidenceCard_" + EvidenceIds[i], grid, x, y, 284, 230, "E1D1B6", .78f);
            Border(card.gameObject, "B6A78D", 1.5f);
            card.raycastTarget = false;
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.transition = Selectable.Transition.ColorTint;
            button.interactable = false;
            ColorBlock colors = button.colors;
            colors.highlightedColor = C("FFF4D8");
            colors.pressedColor = C("D8C49D");
            colors.disabledColor = card.color;
            button.colors = colors;
            cardButtons[i] = button;
            Image badge = Image("IdBadge", card.rectTransform, 12, 12, 72, 36, "655D52");
            Text("Id", badge.rectTransform, 9, 0, 60, 36, EvidenceIds[i], 22, "FAF4E9", FontStyles.Bold);
            RectTransform have = Rect("Collected", card.rectTransform, 10, 51, 260, 166); collected[i] = have.gameObject;
            Texture2D icon = i == 0 ? e01 : i == 1 ? e02 : null;
            if (icon != null) Raw("Illustration", have, 76, 0, 108, 86, icon);
            else Text("CollectedMark", have, 101, 4, 58, 72, "✓", 50, "4F735C", FontStyles.Bold).alignment = TextAlignmentOptions.Center;
            names[i] = Text("EvidenceName", have, 6, 86, 248, 48, EvidenceNames[i], i == 1 ? 19 : 21, "29383D", FontStyles.Bold);
            names[i].alignment = TextAlignmentOptions.Center;
            Image state = Image("CollectedBadge", have, 75, 137, 110, 27, "D6E4D4");
            Text("Label", state.rectTransform, 0, 1, 110, 25, "✓ 확보 완료", 16, "4F735C", FontStyles.Bold).alignment = TextAlignmentOptions.Center;
            RectTransform lockRoot = Rect("Locked", card.rectTransform, 0, 0, 284, 230); hidden[i] = lockRoot.gameObject;
            Raw("LockIcon", lockRoot, 120, 77, 42, 42, locked);
            Text("Unknown", lockRoot, 12, 145, 260, 48, "???", 32, "2B383D", FontStyles.Bold).alignment = TextAlignmentOptions.Center;
            have.gameObject.SetActive(false); lockRoot.gameObject.SetActive(true);
        }
        Text("Hint", grid, 100, 718, 900, 34, "기록이 해금된 증거를 선택하면 상세 내용을 확인할 수 있습니다.", 23, "687074", FontStyles.Normal);

        RectTransform detail = Rect("EvidenceDetailPanel", p, 0, 0, 1060, 856);
        TMP_Text detailHeader = Text("DetailHeader", detail, 100, 36, 600, 34,
            "CASE 404  /  EVIDENCE RECORD", 22, "6C8390", FontStyles.Normal);
        TMP_Text detailName = Text("EvidenceName", detail, 100, 80, 660, 62,
            string.Empty, 40, "2B383D", FontStyles.Bold);
        Image("TitleRule", detail, 100, 151, 900, 1, "BCAA8C");
        Image("TitleAccent", detail, 100, 141, 140, 5, "C9A551", .75f);

        Image statusBadge = Image("EvidenceStatusBadge", detail, 785, 87, 215, 42, "E6DDC4");
        Border(statusBadge.gameObject, "BA9F63", 1.3f);
        TMP_Text detailStatus = Text("EvidenceStatus", statusBadge.rectTransform, 0, 1, 215, 40,
            "✓ 확보 완료", 21, "5B6949", FontStyles.Bold);
        detailStatus.alignment = TextAlignmentOptions.Center;

        Image imageCard = Image("EvidenceImageCard", detail, 100, 188, 340, 320, "29343C");
        Border(imageCard.gameObject, "C19C58", 1.3f);
        TMP_Text detailId = Text("EvidenceId", imageCard.rectTransform, 18, 10, 300, 30,
            string.Empty, 17, "D3B77F", FontStyles.Normal);
        RawImage detailImage = Raw("EvidenceImage", imageCard.rectTransform, 0, 0, 340, 320, evidenceDetailCards[0]);
        TMP_Text fallback = Text("EvidenceFallbackIcon", imageCard.rectTransform, 80, 65, 180, 160,
            "✓", 96, "D3B77F", FontStyles.Bold);
        fallback.alignment = TextAlignmentOptions.Center;
        Text("ImageCaption", imageCard.rectTransform, 17, 264, 306, 31,
            "수집 완료 / 수사 기록 등록", 18, "E9DDC6", FontStyles.Normal).alignment = TextAlignmentOptions.Center;
        Text("ImageFooter", imageCard.rectTransform, 18, 292, 305, 23,
            "CASE 404   •   EVIDENCE FILE", 14, "B8C6C7", FontStyles.Normal).alignment = TextAlignmentOptions.Center;

        // The exported asset is the complete Figma card, so keep it above the
        // legacy generated caption children and avoid duplicated text.
        detailImage.transform.SetAsLastSibling();

        Image metadata = Image("DetailMetadata", detail, 464, 188, 536, 320, "EFE4CF");
        Border(metadata.gameObject, "C8B99B", 1.3f);
        Text("MetadataTitle", metadata.rectTransform, 22, 12, 300, 42,
            "증거물 개요", 28, "26343C", FontStyles.Bold);
        Image("MetadataAccent", metadata.rectTransform, 22, 55, 488, 2, "BA9C63");
        TMP_Text[] detailMetadataValues =
        {
            AddDetailRow(metadata.rectTransform, 0, "발견 장소"),
            AddDetailRow(metadata.rectTransform, 1, "자료 유형"),
            AddDetailRow(metadata.rectTransform, 2, "관련 인물"),
            AddDetailRow(metadata.rectTransform, 3, "관련 사건"),
            AddDetailRow(metadata.rectTransform, 4, "기록 시점")
        };
        TMP_Text detailMetadataFooter = Text("MetadataFooter", metadata.rectTransform, 23, 280, 494, 27,
            string.Empty, 15, "6A5941", FontStyles.Normal);

        Image description = Image("EvidenceDescriptionBlock", detail, 100, 523, 900, 178, "F0E4CD");
        Border(description.gameObject, "D0BE9F", 1.3f);
        Text("DescriptionTitle", description.rectTransform, 20, 10, 780, 36,
            "확인된 핵심 내용", 24, "26363D", FontStyles.Bold);
        Image("DescriptionAccent", description.rectTransform, 19, 50, 5, 108, "C0A05F");
        TMP_Text detailDescription = Text("EvidenceDescription", description.rectTransform, 36, 52, 838, 106,
            string.Empty, 18, "32424A", FontStyles.Normal);
        detailDescription.overflowMode = TextOverflowModes.Ellipsis;
        detailDescription.lineSpacing = 8;

        Image recordState = Image("DetailStatus", detail, 100, 716, 900, 55, "E8D7AF");
        Border(recordState.gameObject, "CEB276", 1.3f);
        TMP_Text detailMemo = Text("Text", recordState.rectTransform, 14, 5, 865, 46,
            string.Empty, 18, "645132", FontStyles.Normal);

        Image backTarget = Image("BackButton", detail, 100, 791, 328, 51, "30424B");
        backTarget.raycastTarget = true;
        Border(backTarget.gameObject, "BBA06D", 1.3f);
        Button backButton = backTarget.gameObject.AddComponent<Button>();
        backButton.targetGraphic = backTarget;
        Text("Label", backTarget.rectTransform, 10, 0, 308, 51,
            "←   증거 목록으로 돌아가기", 20, "F1E9D6", FontStyles.Bold).alignment = TextAlignmentOptions.Center;
        TMP_Text detailPageNumber = Text("RecordIndex", detail, 873, 797, 126, 39,
            "E01 / 06", 19, "817359", FontStyles.Normal);
        detailPageNumber.alignment = TextAlignmentOptions.TopRight;
        detail.gameObject.SetActive(false);

        NotebookEvidencePageController controller = page.GetComponent<NotebookEvidencePageController>() ?? Undo.AddComponent<NotebookEvidencePageController>(page);
        SerializedObject so = new SerializedObject(controller);
        Obj(so, "collectedCountText", collectedCount); Obj(so, "requiredCountText", requiredCount);
        Array(so, "collectedVisuals", collected); Array(so, "lockedVisuals", hidden); Array(so, "evidenceNameTexts", names);
        Obj(so, "evidenceGrid", grid.gameObject); Obj(so, "evidenceDetailPanel", detail.gameObject);
        Obj(so, "pageNavigationRoot", pageNavigation); Array(so, "evidenceCardButtons", cardButtons);
        Obj(so, "backButton", backButton); Obj(so, "detailHeaderText", detailHeader);
        Obj(so, "detailEvidenceIdText", detailId); Obj(so, "detailMetadataIdText", null);
        Obj(so, "detailEvidenceNameText", detailName);
        Obj(so, "detailEvidenceTypeText", null); Obj(so, "detailDescriptionText", detailDescription);
        Obj(so, "detailStatusText", detailStatus); Obj(so, "detailMetadataStatusText", null);
        Obj(so, "detailEvidenceImage", detailImage);
        Obj(so, "detailFallbackIcon", fallback.gameObject);
        Array(so, "evidenceImages", evidenceDetailCards);
        Array(so, "detailMetadataValueTexts", detailMetadataValues);
        Obj(so, "detailMetadataFooterText", detailMetadataFooter);
        Obj(so, "detailMemoText", detailMemo);
        Obj(so, "detailPageNumberText", detailPageNumber);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static TMP_Text AddDetailRow(RectTransform parent, int index, string label)
    {
        float y = 71 + index * 39;
        if (index % 2 == 0) Image("RowBackground_" + index, parent, 22, y, 488, 39, "E5D8BF", .55f);
        Text("RowLabel_" + index, parent, 34, y + 2, 118, 35, label, 18, "87765F", FontStyles.Normal);
        TMP_Text value = Text("RowValue_" + index, parent, 164, y + 1, 334, 37,
            string.Empty, 19, "28373E", FontStyles.Normal);
        return value;
    }

    static void BuildMemo(Transform p)
    {
        Header(p, "추리 메모", 140);
        Text("DocumentLabel", p, 620, 42, 380, 34, "INVESTIGATION NOTES", 22, "6C8390", FontStyles.Bold).alignment = TextAlignmentOptions.TopRight;
        string[] questions =
        {
            "실제 사망 경위는 무엇인가?",
            "사건 당시 피해자와 마지막으로 접촉한 사람은 누구인가?",
            "세 사람의 알리바이 중, 증거와 모순되는 부분은 무엇인가?",
            "과거 임상시험 사고와 이번 사건은 어떤 연관이 있는가?"
        };
        float[] widths = { 180, 360, 384, 360 };
        for (int i = 0; i < 4; i++)
        {
            float y = 200 + i * 138;
            Image checkbox = Image($"Checkbox{i}", p, 104, y + 10, 27, 27, "F5ECD8", .45f); Border(checkbox.gameObject, "8C795E", 1.5f);
            Text($"Question{i}", p, 148, y, 852, 52, questions[i], 28, "2B383D", FontStyles.Normal);
            Image($"Underline{i}", p, 148, y + 57, widths[i], 3, "C9A551", .75f);
        }
    }

    static void Header(Transform p, string title, float accentWidth)
    {
        Text("CaseCode", p, 100, 36, 500, 34, "CASE 404", 23, "6C8390", FontStyles.Normal);
        Text("Title", p, 100, 80, 900, 62, title, 42, "2B383D", FontStyles.Bold);
        Image("TitleRule", p, 100, 151, 900, 1, "BCAA8C");
        Image("TitleAccent", p, 100, 141, accentWidth, 5, "C9A551");
    }

    static void Field(Transform p, float x, float y, string label, string value)
    {
        Text(label + "Label", p, x, y, 145, 36, label, 21, "687074", FontStyles.Bold);
        Text(label + "Value", p, x + 150, y - 2, 430, 40, value, 24, "2B383D", FontStyles.Normal);
        Image(label + "Rule", p, x, y + 48, 580, 1, "C5B598");
    }

    static TMP_Text Status(Transform p, string name, float y, string label, string value)
    {
        Image bar = Image(name, p, 620, y, 380, 46, "293035");
        Text("Label", bar.rectTransform, 16, 8, 245, 30, label, 21, "F4EEE1", FontStyles.Normal);
        TMP_Text count = Text("Count", bar.rectTransform, 280, 4, 90, 38, value, 26, "D8BA60", FontStyles.Bold);
        count.alignment = TextAlignmentOptions.TopRight; return count;
    }

    static GameObject Page(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null) return found.gameObject;
        RectTransform page = Rect(name, parent, 0, 0, 1060, 856); page.gameObject.SetActive(false); return page.gameObject;
    }

    static void Rebuild(Transform page, Action<Transform> builder, Transform preserve = null)
    {
        for (int i = page.childCount - 1; i >= 0; i--)
        {
            Transform child = page.GetChild(i);
            if (child != preserve)
                Undo.DestroyObjectImmediate(child.gameObject);
        }
        builder(page);
    }

    [MenuItem(DeductionEntryMenu, true)]
    static bool CanConfigureDeductionEntry() =>
        !EditorApplication.isPlayingOrWillChangePlaymode &&
        PrefabStageUtility.GetCurrentPrefabStage() == null &&
        SceneManager.GetActiveScene().path == ScenePath;

    [MenuItem(DeductionEntryMenu)]
    static void ConfigureDeductionEntry()
    {
        if (!CanConfigureDeductionEntry())
            return;

        Scene scene = SceneManager.GetActiveScene();
        GameObject canvas = FindCanvas(scene);
        var memo = canvas != null
            ? canvas.transform.Find("NotebookRoot/NotebookContent/MemoPage") as RectTransform : null;
        var navigation = canvas != null ? canvas.transform.Find("NotebookRoot/PageNavigation") : null;
        if (memo == null || navigation == null)
            throw new InvalidOperationException("기존 MemoPage 또는 PageNavigation을 찾지 못했습니다.");

        Transform existing = memo.Find(DeductionEntryName);
        if (existing != null)
        {
            if (existing.GetComponent<NotebookDeductionEntry>() == null)
                throw new InvalidOperationException("같은 이름의 사용자 UI가 이미 있습니다.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }
        if (memo.Find("StartDeductionButton") != null)
            throw new InvalidOperationException("기존 StartDeductionButton을 먼저 확인해 주세요.");

        DeductionStartUIController entry = null;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var candidate in root.GetComponentsInChildren<DeductionStartUIController>(true))
        {
            if (entry != null)
                throw new InvalidOperationException("DeductionStartUIController가 여러 개입니다.");
            entry = candidate;
        }
        if (entry == null)
            throw new InvalidOperationException("DeductionStartUIController를 찾지 못했습니다.");
        var entryState = new SerializedObject(entry);
        var result = entryState.FindProperty("resultController").objectReferenceValue as ResultController;
        var types = entryState.FindProperty("caseTypeSelectionController").objectReferenceValue as CaseTypeSelectionController;
        var notebook = canvas.GetComponent<NotebookToggleController>();
        if (result == null || types == null || notebook == null ||
            new SerializedObject(types).FindProperty("notebookToggleController").objectReferenceValue != notebook)
            throw new InvalidOperationException("추리 컨트롤러의 결과 및 수첩 참조를 확인해 주세요.");

        var title = memo.Find("Title")?.GetComponent<TMP_Text>();
        var checkbox = memo.Find("Checkbox0")?.GetComponent<Image>();
        if (title == null || title.font == null || checkbox == null)
            throw new InvalidOperationException("기존 메모의 폰트와 체크박스 스타일을 찾지 못했습니다.");

        // The shipped memo ends at y=674 and navigation starts at y=790.
        // Check current scene rectangles as well, since the layout can be edited.
        var footerArea = new Rect(memo.rect.xMin + 100, memo.rect.yMax - 772, 900, 54);
        EnsureFooterFits(memo, navigation, footerArea);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Notebook Deduction Entry");
        layer = memo.gameObject.layer;
        font = title.font;
        textMaterial = title.fontSharedMaterial;
        uiMaterial = checkbox.material;

        var footer = Rect(DeductionEntryName, memo, 100, 718, 900, 54);
        var progress = Text("RequiredEvidenceProgress", footer, 0, 0, 340, 54,
            "0 / 4", 26, "2B383D", FontStyles.Bold);
        progress.alignment = TextAlignmentOptions.Left;

        var target = Image("StartDeductionButton", footer, 620, 0, 280, 54, "30424B");
        target.raycastTarget = true;
        Border(target.gameObject, "BBA06D", 1.3f);
        var button = Undo.AddComponent<Button>(target.gameObject);
        button.targetGraphic = target;
        button.interactable = false;
        var colors = button.colors;
        colors.disabledColor = new Color(0.62f, 0.64f, 0.65f, 0.72f);
        colors.highlightedColor = C("E6CD94");
        colors.selectedColor = C("E6CD94");
        button.colors = colors;
        Text("Label", target.rectTransform, 0, 0, 280, 54,
            "최종 추리 시작", 25, "F1E9D6", FontStyles.Bold).alignment = TextAlignmentOptions.Center;

        var binder = new SerializedObject(Undo.AddComponent<NotebookDeductionEntry>(footer.gameObject));
        Obj(binder, "deductionStart", entry);
        Obj(binder, "result", result);
        Obj(binder, "startButton", button);
        Obj(binder, "progressText", progress);
        binder.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = footer.gameObject;
    }

    static void EnsureFooterFits(RectTransform memo, Transform navigation, Rect footer)
    {
        if (!memo.rect.Contains(footer.min) || !memo.rect.Contains(footer.max))
            throw new InvalidOperationException("메모 페이지의 하단 영역이 부족합니다.");

        var corners = new Vector3[4];
        foreach (Transform container in new Transform[] { memo, navigation })
        foreach (Transform child in container)
        {
            if (child is not RectTransform rect)
                continue;
            rect.GetWorldCorners(corners);
            Vector2 min = memo.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = memo.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            if (footer.Overlaps(UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y)))
                throw new InvalidOperationException($"'{child.name}'과(와) 겹쳐 추리 버튼 생성을 중단했습니다.");
        }
    }

    static void LoadAssets()
    {
        layer = LayerMask.NameToLayer("XR_UI_Overlay");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        uiMaterial = AssetDatabase.LoadAssetAtPath<Material>(UiMaterialPath);
        textMaterial = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);
        victim = Tex("VictimPortrait.png"); autopsy = Tex("AutopsySketch.png"); fingerprint = Tex("FingerprintReference.png");
        suspects = new[] { Tex("Suspect_KimMinSu.png"), Tex("Suspect_LeeSeoYeon.png"), Tex("Suspect_ParkJinHo.png") };
        e01 = Tex("Evidence_E01.png"); e02 = Tex("Evidence_E02.png"); e03 = Tex("Evidence_E03.png"); locked = Tex("Evidence_Locked.png");
        evidenceDetailCards = new[]
        {
            Tex("E01 · 증거 이미지 카드.png"), Tex("E02 · 증거 이미지 카드.png"),
            Tex("E03 · 증거 이미지 카드.png"), Tex("E04 · 증거 이미지 카드.png"),
            Tex("E05 · 증거 이미지 카드.png"), Tex("E06 · 증거 이미지 카드.png")
        };
        if (layer < 0 || font == null || uiMaterial == null || textMaterial == null || victim == null ||
            autopsy == null || fingerprint == null || e01 == null || e02 == null || e03 == null || locked == null ||
            suspects[0] == null || suspects[1] == null || suspects[2] == null ||
            System.Array.Exists(evidenceDetailCards, card => card == null))
            throw new InvalidOperationException("수첩 UI Layer, 폰트, Material 또는 Figma PNG 자산이 누락되었습니다.");
    }

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot + name);
    static GameObject FindCanvas(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "NotebookCanvas") return t.gameObject;
        return null;
    }
    static Transform Need(Transform p, string path) => p.Find(path) ?? throw new InvalidOperationException("누락된 Notebook 오브젝트: " + path);
    static void Obj(SerializedObject so, string name, UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue = value;
    static void Array<T>(SerializedObject so, string name, T[] values) where T : UnityEngine.Object
    {
        SerializedProperty a = so.FindProperty(name); a.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go, "Configure Notebook Pages");
        go.layer = layer; go.transform.SetParent(parent, false); RectTransform r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
    }
    static Image Image(string name, Transform parent, float x, float y, float w, float h, string hex, float alpha = 1)
    {
        Image image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); Color color = C(hex); color.a *= alpha;
        image.color = color; image.material = uiMaterial; image.raycastTarget = false; return image;
    }
    static RawImage Raw(string name, Transform parent, float x, float y, float w, float h, Texture texture)
    {
        RawImage image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<RawImage>(); image.texture = texture;
        image.material = uiMaterial; image.raycastTarget = false; return image;
    }
    static TextMeshProUGUI Text(string name, Transform parent, float x, float y, float w, float h, string value, float size, string hex, FontStyles style)
    {
        TextMeshProUGUI text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSharedMaterial = textMaterial; text.text = value; text.fontSize = size; text.color = C(hex);
        text.fontStyle = style; text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate; text.raycastTarget = false; return text;
    }
    static void Border(GameObject go, string hex, float distance)
    {
        Outline outline = go.AddComponent<Outline>(); outline.effectColor = C(hex); outline.effectDistance = new Vector2(distance, -distance); outline.useGraphicAlpha = true;
    }
    static Color C(string hex)
    {
        if (!ColorUtility.TryParseHtmlString("#" + hex, out Color color)) throw new ArgumentException("Invalid color: " + hex); return color;
    }
}
