using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum CaseType
{
    None,
    Suicide,
    Accident,
    ThirdPartyInvolvement
}

/// <summary>
/// 사건 유형 선택 화면의 표시 상태와 임시 선택만 관리한다.
/// 최종 판정 및 용의자 선택 화면 전환은 다음 단계의 구독자가 담당한다.
/// </summary>
public sealed class CaseTypeSelectionController : MonoBehaviour
{
    private const int RequiredEvidenceTotal = 4;

    [Header("Panel")]
    [SerializeField] private GameObject caseTypeRoot;

    [Header("Cards")]
    [SerializeField] private Button suicideButton;
    [SerializeField] private Button accidentButton;
    [SerializeField] private Button thirdPartyButton;
    [SerializeField] private Outline suicideOutline;
    [SerializeField] private Outline accidentOutline;
    [SerializeField] private Outline thirdPartyOutline;
    [SerializeField] private GameObject suicideRadioSelected;
    [SerializeField] private GameObject accidentRadioSelected;
    [SerializeField] private GameObject thirdPartyRadioSelected;

    [Header("Summary")]
    [SerializeField] private TMP_Text currentSelectionText;
    [SerializeField] private TMP_Text selectionHintText;
    [SerializeField] private TMP_Text requiredEvidenceText;

    [Header("Footer")]
    [SerializeField] private Button openNotebookButton;
    [SerializeField] private Button nextStepButton;
    [SerializeField] private Image nextStepBackground;
    [SerializeField] private TMP_Text nextStepLabel;
    [SerializeField] private NotebookToggleController notebookToggleController;

    [Header("Visual State")]
    [SerializeField] private Color defaultCardBorder = new(0.35f, 0.40f, 0.46f, 1f);
    [SerializeField] private Color selectedCardBorder = new(0.82f, 0.69f, 0.42f, 1f);
    [SerializeField] private Color disabledNextBackground = new(0.45f, 0.42f, 0.33f, 1f);
    [SerializeField] private Color enabledNextBackground = new(0.85f, 0.73f, 0.41f, 1f);
    [SerializeField] private Color disabledNextText = new(0.20f, 0.21f, 0.22f, 1f);
    [SerializeField] private Color enabledNextText = new(0.07f, 0.10f, 0.14f, 1f);

    private CaseType selectedCaseType = CaseType.None;
    private bool listenersRegistered;

    public event Action<CaseType> SelectionChanged;
    public event Action<CaseType> NextStepRequested;

    public bool HasSelection => selectedCaseType != CaseType.None;
    public CaseType SelectedCaseType => selectedCaseType;
    public bool IsPanelOpen => caseTypeRoot != null && caseTypeRoot.activeInHierarchy;
    public bool CanOpenPanel => isActiveAndEnabled && caseTypeRoot != null &&
                                (caseTypeRoot.transform.parent == null ||
                                 caseTypeRoot.transform.parent.gameObject.activeInHierarchy);

    private void Awake()
    {
        selectedCaseType = CaseType.None;
        RefreshSelectionVisuals();

        if (caseTypeRoot != null)
            caseTypeRoot.SetActive(false);
    }

    private void OnEnable()
    {
        RegisterListeners();
        SubscribeEvidenceEvents();
        RefreshRequiredEvidence();
    }

    private void Start()
    {
        // EvidenceManager가 이 컴포넌트보다 늦게 Awake되는 Scene 구성도 지원한다.
        SubscribeEvidenceEvents();
        RefreshRequiredEvidence();
    }

    private void OnDisable()
    {
        UnregisterListeners();
        UnsubscribeEvidenceEvents();
    }

    public void OpenPanel()
    {
        if (caseTypeRoot == null)
        {
            Debug.LogError("[Case Type] CaseTypeRoot가 연결되지 않았습니다.", this);
            return;
        }

        if (!CanOpenPanel)
            return;

        RefreshRequiredEvidence();
        RefreshSelectionVisuals();
        caseTypeRoot.SetActive(true);

        if (IsPanelOpen && notebookToggleController != null && notebookToggleController.IsNotebookOpen)
            notebookToggleController.CloseNotebook();
    }

    public void ClosePanel()
    {
        if (caseTypeRoot != null)
            caseTypeRoot.SetActive(false);
    }

    public void SelectCaseType(CaseType type)
    {
        if (type == CaseType.None)
        {
            ClearSelection();
            return;
        }

        if (selectedCaseType == type)
            return;

        selectedCaseType = type;
        RefreshSelectionVisuals();
        SelectionChanged?.Invoke(selectedCaseType);
    }

    public void ClearSelection()
    {
        bool changed = selectedCaseType != CaseType.None;
        selectedCaseType = CaseType.None;
        RefreshSelectionVisuals();

        if (changed)
            SelectionChanged?.Invoke(CaseType.None);
    }

    public void SelectSuicide() => SelectCaseType(CaseType.Suicide);
    public void SelectAccident() => SelectCaseType(CaseType.Accident);
    public void SelectThirdPartyInvolvement() => SelectCaseType(CaseType.ThirdPartyInvolvement);

    public void RequestNextStep()
    {
        if (!HasSelection)
            return;

        NextStepRequested?.Invoke(selectedCaseType);
        Debug.Log($"[Case Type] 다음 단계 요청: {GetDisplayName(selectedCaseType)}", this);
    }

    private void OpenNotebook()
    {
        if (notebookToggleController == null)
        {
            Debug.LogWarning("[Case Type] NotebookToggleController가 연결되지 않았습니다.", this);
            return;
        }

        // 기존 수첩의 모달 차단 규칙을 우회하지 않는다. 현재 모달을 먼저 닫은 뒤
        // NotebookToggleController의 공개 API가 다른 차단 요소를 최종 판단한다.
        ClosePanel();
        notebookToggleController.OpenNotebook();
    }

    private void RegisterListeners()
    {
        if (listenersRegistered)
            return;

        AddListener(suicideButton, SelectSuicide);
        AddListener(accidentButton, SelectAccident);
        AddListener(thirdPartyButton, SelectThirdPartyInvolvement);
        AddListener(openNotebookButton, OpenNotebook);
        AddListener(nextStepButton, RequestNextStep);
        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered)
            return;

        RemoveListener(suicideButton, SelectSuicide);
        RemoveListener(accidentButton, SelectAccident);
        RemoveListener(thirdPartyButton, SelectThirdPartyInvolvement);
        RemoveListener(openNotebookButton, OpenNotebook);
        RemoveListener(nextStepButton, RequestNextStep);
        listenersRegistered = false;
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    private void SubscribeEvidenceEvents()
    {
        EvidenceManager manager = EvidenceManager.Instance;
        if (manager == null)
            return;

        manager.EvidenceRegistered -= OnEvidenceRegistered;
        manager.EvidenceRegistered += OnEvidenceRegistered;
    }

    private void UnsubscribeEvidenceEvents()
    {
        EvidenceManager manager = EvidenceManager.Instance;
        if (manager != null)
            manager.EvidenceRegistered -= OnEvidenceRegistered;
    }

    private void OnEvidenceRegistered(EvidenceData _)
    {
        RefreshRequiredEvidence();
    }

    private void RefreshRequiredEvidence()
    {
        if (requiredEvidenceText == null)
            return;

        int count = EvidenceManager.Instance != null ? EvidenceManager.Instance.RequiredCount : 0;
        requiredEvidenceText.text = $"✓  필수 {count}/{RequiredEvidenceTotal}";
    }

    private void RefreshSelectionVisuals()
    {
        SetCardState(suicideOutline, suicideRadioSelected, selectedCaseType == CaseType.Suicide);
        SetCardState(accidentOutline, accidentRadioSelected, selectedCaseType == CaseType.Accident);
        SetCardState(thirdPartyOutline, thirdPartyRadioSelected,
            selectedCaseType == CaseType.ThirdPartyInvolvement);

        string displayName = GetDisplayName(selectedCaseType);
        if (currentSelectionText != null)
            currentSelectionText.text = displayName;

        if (selectionHintText != null)
        {
            selectionHintText.text = HasSelection
                ? $"{GetSelectionObjectPhrase(selectedCaseType)} 선택했습니다. 다음 단계에서 용의자를 지목하세요."
                : "사건 유형을 선택하면 용의자 선택 단계로 이동합니다.";
        }

        if (nextStepButton != null)
            nextStepButton.interactable = HasSelection;

        if (nextStepBackground != null)
            nextStepBackground.color = HasSelection ? enabledNextBackground : disabledNextBackground;

        if (nextStepLabel != null)
            nextStepLabel.color = HasSelection ? enabledNextText : disabledNextText;
    }

    private void SetCardState(Outline outline, GameObject selectedRadio, bool selected)
    {
        if (outline != null)
        {
            outline.effectColor = selected ? selectedCardBorder : defaultCardBorder;
            outline.effectDistance = selected ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);
        }

        if (selectedRadio != null)
            selectedRadio.SetActive(selected);
    }

    private static string GetDisplayName(CaseType type)
    {
        return type switch
        {
            CaseType.Suicide => "단독 자살",
            CaseType.Accident => "단독 사고",
            CaseType.ThirdPartyInvolvement => "타인의 개입",
            _ => "없음"
        };
    }

    private static string GetSelectionObjectPhrase(CaseType type)
    {
        return type switch
        {
            CaseType.Accident => "단독 사고를",
            CaseType.Suicide => "단독 자살을",
            CaseType.ThirdPartyInvolvement => "타인의 개입을",
            _ => string.Empty
        };
    }

#if UNITY_EDITOR
    [ContextMenu("Debug / Open Case Type Panel")]
    private void DebugOpenPanel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Case Type] 미리보기는 Play Mode에서만 실행할 수 있습니다.", this);
            return;
        }

        OpenPanel();
    }

    [ContextMenu("Debug / Clear Case Type Selection")]
    private void DebugClearSelection()
    {
        if (Application.isPlaying)
            ClearSelection();
    }
#endif
}
