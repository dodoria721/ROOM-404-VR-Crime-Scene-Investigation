using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NotebookEvidencePageController : MonoBehaviour
{
    private static readonly string[] EvidenceIds =
    {
        "E01", "E02", "E03", "E04", "E05", "E06"
    };

    private static readonly EvidenceDetailContent[] DetailContents =
    {
        new EvidenceDetailContent(
            "세정제 용기",
            new[] { "정하린 연구 책상 주변", "세정 용액 용기 · 지문 감식", "정하린 · 이서연", "연구실 404호 사망 사건", "2026년 10월 29일 사건 현장" },
            "감식 기록:  용기 표면 지문 / 공용 세정 물품",
            "• 용기 표면에서 이서연의 지문이 확인되었다.\n• 연구실 공용 물품이므로 평소 접촉했을 가능성도 있다.\n• 사건 이후 현장 정리에 사용되었을 가능성이 제기된다.",
            "수사 메모  |  이서연의 지문은 일상적인 사용 흔적일까, 사건과 관련된 흔적일까?"),
        new EvidenceDetailContent(
            "책상 모서리 충돌 흔적",
            new[] { "정하린 연구 책상 모서리", "충돌 흔적 · UV 감식 표식", "정하린", "연구실 404호 사망 사건", "2026년 10월 29일 사건 현장" },
            "감식 기록:  UV 표식 / 책상 모서리 충돌 지점",
            "• UV 조사로 책상 모서리의 감식 표식이 확인되었다.\n• 흔적의 위치는 피해자의 두부 손상과 관련될 가능성이 있다.\n• 목 주변의 수상한 흔적만으로는 사망 경위를 설명하기 어렵다.",
            "수사 메모  |  피해자의 치명적인 머리 손상은 어떻게 발생했을까?"),
        new EvidenceDetailContent(
            "정하린의 개인 하드디스크",
            new[] { "정하린 개인 컴퓨터", "CCTV 영상 · 실험 로그", "정하린 · 이서연", "NEUROLINK-404", "2025년 11월 19일" },
            "보관 기록:  CCTV 원본 영상 / 장비 로그 / 사고 보고서",
            "• 과거 임상시험의 CCTV 원본과 장비 기록이 저장되어 있다.\n• 실험 당시 칩의 전압 상승 및 발열 이상 정황이 확인된다.\n• 공식 사고 보고서와 원본 기록 사이에 일치하지 않는 내용이 있다.",
            "수사 메모  |  공식 보고서와 원본 기록이 서로 다른 이유는 무엇일까?"),
        new EvidenceDetailContent(
            "박진호 연구비 문서",
            new[] { "박진호 교수의 책상", "연구비 집행 문서 · 관련 메시지", "박진호 · 정하린", "NEUROLINK-404 연구비", "문서 작성일 확인 필요" },
            "문서 기록:  수상한 고액 지출 / 요청 메시지",
            "• 연구비 집행 내역에서 수상한 고액 지출이 발견되었다.\n• 정하린이 박진호에게 해외 취업 추천과 논문 저자 등록을 요구한 정황이 있다.\n• 두 사람 사이에 금전적 문제와 압박 관계가 있었던 것으로 보인다.",
            "수사 메모  |  박진호는 자신의 비밀을 지키기 위해 정하린에게 해를 가할 이유가 있었을까?"),
        new EvidenceDetailContent(
            "김민수 협박 메시지",
            new[] { "김민수 연구용 PC", "협박 메시지 · 디지털 기록", "김민수 · 정하린", "연구 성과 도용 갈등", "사건 이전 · 발송 일시 미확정" },
            "메시지 기록:  연구 성과 도용 갈등 / 위협 표현",
            "• 김민수는 연구 성과 도용 문제로 정하린과 심하게 대립했다.\n• 메시지에는 정하린을 향한 분노와 위협적인 표현이 담겨 있다.\n• 그러나 메시지만으로 사건 당일 현장에 있었음을 확인할 수는 없다.",
            "수사 메모  |  강한 범행 동기가 실제 사건 개입으로 이어졌을까?"),
        new EvidenceDetailContent(
            "정하린 휴대폰 약속 기록",
            new[] { "정하린 책상 위 휴대폰", "약속 메시지 · 통신 기록", "정하린 · 이서연", "연구실 404호 사망 사건", "2026년 10월 29일 밤" },
            "통신 기록:  정하린 ↔ 이서연 / 연구실 약속",
            "• 사건 당일 이서연과 정하린이 연구실에서 만나기로 한 기록이 남아 있다.\n• 이서연은 사건 당시 자취방에서 쉬고 있었다고 진술했다.\n• 두 사람의 약속 기록은 이서연의 알리바이와 모순될 가능성이 있다.",
            "수사 메모  |  이서연은 왜 정하린과의 마지막 약속을 진술하지 않았을까?")
    };

    [SerializeField] private TMP_Text collectedCountText;
    [SerializeField] private TMP_Text requiredCountText;
    [SerializeField] private GameObject[] collectedVisuals;
    [SerializeField] private GameObject[] lockedVisuals;
    [SerializeField] private TMP_Text[] evidenceNameTexts;

    [Header("Evidence Detail")]
    [SerializeField] private GameObject evidenceGrid;
    [SerializeField] private GameObject evidenceDetailPanel;
    [SerializeField] private GameObject pageNavigationRoot;
    [SerializeField] private Button[] evidenceCardButtons;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text detailHeaderText;
    [SerializeField] private TMP_Text detailEvidenceIdText;
    [SerializeField] private TMP_Text detailMetadataIdText;
    [SerializeField] private TMP_Text detailEvidenceNameText;
    [SerializeField] private TMP_Text detailEvidenceTypeText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_Text detailStatusText;
    [SerializeField] private TMP_Text detailMetadataStatusText;
    [SerializeField] private RawImage detailEvidenceImage;
    [SerializeField] private GameObject detailFallbackIcon;
    [SerializeField] private Texture[] evidenceImages;
    [SerializeField] private TMP_Text[] detailMetadataValueTexts;
    [SerializeField] private TMP_Text detailMetadataFooterText;
    [SerializeField] private TMP_Text detailMemoText;
    [SerializeField] private TMP_Text detailPageNumberText;

    private EvidenceManager subscribedManager;
    private bool buttonListenersRegistered;

    private void OnEnable()
    {
        RegisterButtonListeners();
        Subscribe();
        ShowEvidenceGrid();
        Refresh();
    }

    private void OnDisable()
    {
        ShowEvidenceGrid();
        UnregisterButtonListeners();
        Unsubscribe();
    }

    public void Refresh()
    {
        EvidenceManager manager = EvidenceManager.Instance;
        int collectedCount = manager != null ? manager.CollectedCount : 0;
        int requiredCount = manager != null ? manager.RequiredCount : 0;

        if (collectedCountText != null)
            collectedCountText.SetText("{0} / 6", collectedCount);

        if (requiredCountText != null)
            requiredCountText.SetText("{0} / 4", requiredCount);

        for (int i = 0; i < EvidenceIds.Length; i++)
        {
            EvidenceData data = FindCollectedEvidence(manager, EvidenceIds[i]);
            bool collected = data != null;

            SetActive(collectedVisuals, i, collected);
            SetActive(lockedVisuals, i, !collected);
            SetCardInteractable(i, collected);

            if (collected && evidenceNameTexts != null && i < evidenceNameTexts.Length &&
                evidenceNameTexts[i] != null)
            {
                evidenceNameTexts[i].text = data.EvidenceName;
            }
        }
    }

    private void Subscribe()
    {
        EvidenceManager manager = EvidenceManager.Instance;
        if (manager == subscribedManager)
            return;

        Unsubscribe();
        subscribedManager = manager;
        if (subscribedManager != null)
            subscribedManager.EvidenceRegistered += HandleEvidenceRegistered;
    }

    private void Unsubscribe()
    {
        if (subscribedManager != null)
            subscribedManager.EvidenceRegistered -= HandleEvidenceRegistered;

        subscribedManager = null;
    }

    private void HandleEvidenceRegistered(EvidenceData _)
    {
        Refresh();
    }

    public void ShowEvidenceGrid()
    {
        if (evidenceGrid != null && !evidenceGrid.activeSelf)
            evidenceGrid.SetActive(true);

        if (evidenceDetailPanel != null && evidenceDetailPanel.activeSelf)
            evidenceDetailPanel.SetActive(false);

        if (pageNavigationRoot != null && !pageNavigationRoot.activeSelf)
            pageNavigationRoot.SetActive(true);
    }

    private void OpenDetail(int index)
    {
        if (index < 0 || index >= EvidenceIds.Length)
            return;

        EvidenceData data = FindCollectedEvidence(EvidenceManager.Instance, EvidenceIds[index]);
        if (data == null)
            return;

        if (detailHeaderText != null)
            detailHeaderText.text = $"CASE 404  /  EVIDENCE RECORD · {data.Id}";

        if (detailEvidenceIdText != null)
            detailEvidenceIdText.text = $"EVIDENCE {data.Id.Substring(1)}   /   RECORD";

        if (detailMetadataIdText != null)
            detailMetadataIdText.text = data.Id;

        if (detailEvidenceTypeText != null)
            detailEvidenceTypeText.text = IsRequiredEvidence(data.Id) ? "필수 증거" : "선택 증거";

        if (detailStatusText != null)
            detailStatusText.text = $"✓ {data.Id}  확보 완료";

        if (detailMetadataStatusText != null)
            detailMetadataStatusText.text = "수집 완료";

        EvidenceDetailContent content = DetailContents[index];
        if (detailEvidenceNameText != null)
            detailEvidenceNameText.text = content.Title;

        if (detailDescriptionText != null)
            detailDescriptionText.text = content.Findings;

        if (detailMetadataValueTexts != null)
        {
            int valueCount = Mathf.Min(detailMetadataValueTexts.Length, content.MetadataValues.Length);
            for (int i = 0; i < valueCount; i++)
            {
                if (detailMetadataValueTexts[i] != null)
                    detailMetadataValueTexts[i].text = content.MetadataValues[i];
            }
        }

        if (detailMetadataFooterText != null)
            detailMetadataFooterText.text = content.MetadataFooter;

        if (detailMemoText != null)
            detailMemoText.text = content.Memo;

        if (detailPageNumberText != null)
            detailPageNumberText.text = $"{data.Id} / 06";

        Texture image = evidenceImages != null && index < evidenceImages.Length
            ? evidenceImages[index]
            : null;
        if (detailEvidenceImage != null)
        {
            detailEvidenceImage.texture = image;
            detailEvidenceImage.gameObject.SetActive(image != null);
        }

        if (detailFallbackIcon != null)
            detailFallbackIcon.SetActive(image == null);

        if (evidenceGrid != null)
            evidenceGrid.SetActive(false);

        if (pageNavigationRoot != null)
            pageNavigationRoot.SetActive(false);

        if (evidenceDetailPanel != null)
            evidenceDetailPanel.SetActive(true);
    }

    private void RegisterButtonListeners()
    {
        if (buttonListenersRegistered)
            return;

        AddListener(0, ShowE01Detail);
        AddListener(1, ShowE02Detail);
        AddListener(2, ShowE03Detail);
        AddListener(3, ShowE04Detail);
        AddListener(4, ShowE05Detail);
        AddListener(5, ShowE06Detail);
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ShowEvidenceGrid);
            backButton.onClick.AddListener(ShowEvidenceGrid);
        }

        buttonListenersRegistered = true;
    }

    private void UnregisterButtonListeners()
    {
        if (!buttonListenersRegistered)
            return;

        RemoveListener(0, ShowE01Detail);
        RemoveListener(1, ShowE02Detail);
        RemoveListener(2, ShowE03Detail);
        RemoveListener(3, ShowE04Detail);
        RemoveListener(4, ShowE05Detail);
        RemoveListener(5, ShowE06Detail);
        if (backButton != null)
            backButton.onClick.RemoveListener(ShowEvidenceGrid);

        buttonListenersRegistered = false;
    }

    private void AddListener(int index, UnityEngine.Events.UnityAction action)
    {
        if (evidenceCardButtons == null || index >= evidenceCardButtons.Length ||
            evidenceCardButtons[index] == null)
        {
            return;
        }

        evidenceCardButtons[index].onClick.RemoveListener(action);
        evidenceCardButtons[index].onClick.AddListener(action);
    }

    private void RemoveListener(int index, UnityEngine.Events.UnityAction action)
    {
        if (evidenceCardButtons != null && index < evidenceCardButtons.Length &&
            evidenceCardButtons[index] != null)
        {
            evidenceCardButtons[index].onClick.RemoveListener(action);
        }
    }

    private void SetCardInteractable(int index, bool interactable)
    {
        if (evidenceCardButtons == null || index >= evidenceCardButtons.Length ||
            evidenceCardButtons[index] == null)
        {
            return;
        }

        Button button = evidenceCardButtons[index];
        button.interactable = interactable;
        if (button.targetGraphic != null)
            button.targetGraphic.raycastTarget = interactable;
    }

    private static bool IsRequiredEvidence(string id)
    {
        return id == "E01" || id == "E02" || id == "E03" || id == "E06";
    }

    private void ShowE01Detail() => OpenDetail(0);
    private void ShowE02Detail() => OpenDetail(1);
    private void ShowE03Detail() => OpenDetail(2);
    private void ShowE04Detail() => OpenDetail(3);
    private void ShowE05Detail() => OpenDetail(4);
    private void ShowE06Detail() => OpenDetail(5);

    private static EvidenceData FindCollectedEvidence(EvidenceManager manager, string id)
    {
        if (manager == null)
            return null;

        foreach (EvidenceData data in manager.CollectedEvidence)
        {
            if (data != null && data.Id == id)
                return data;
        }

        return null;
    }

    private readonly struct EvidenceDetailContent
    {
        public EvidenceDetailContent(
            string title,
            string[] metadataValues,
            string metadataFooter,
            string findings,
            string memo)
        {
            Title = title;
            MetadataValues = metadataValues;
            MetadataFooter = metadataFooter;
            Findings = findings;
            Memo = memo;
        }

        public string Title { get; }
        public string[] MetadataValues { get; }
        public string MetadataFooter { get; }
        public string Findings { get; }
        public string Memo { get; }
    }

    private static void SetActive(GameObject[] targets, int index, bool active)
    {
        if (targets != null && index < targets.Length && targets[index] != null &&
            targets[index].activeSelf != active)
        {
            targets[index].SetActive(active);
        }
    }
}
