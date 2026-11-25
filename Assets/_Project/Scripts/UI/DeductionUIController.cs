using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DeductionUIController : MonoBehaviour
{
    [Header("Game Logic")]
    [SerializeField] private CaseTypeSelectionController caseTypeSelectionController;
    [SerializeField] private DeductionController deductionController;
    [SerializeField] private ResultController resultController;

    [Header("Panel")]
    [Tooltip("Keep this controller on an always-active Canvas root, not on this panel.")]
    [SerializeField] private GameObject deductionPanel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text currentSelectionText;

    [Header("Suspect Cards")]
    [SerializeField] private Button kimMinSuButton;
    [SerializeField] private Button leeSeoYeonButton;
    [SerializeField] private Button parkJinHoButton;
    [SerializeField] private Outline kimMinSuSelectionOutline;
    [SerializeField] private Outline leeSeoYeonSelectionOutline;
    [SerializeField] private Outline parkJinHoSelectionOutline;

    [Header("Bottom Buttons")]
    [SerializeField] private Button reviewButton;
    [SerializeField] private Button confirmButton;

    [Header("Confirmation Popup")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Button confirmationCancelButton;
    [SerializeField] private Button confirmationAcceptButton;
    [SerializeField] private Image confirmationPortrait;
    [SerializeField] private TMP_Text confirmationQuestionText;
    [SerializeField] private TMP_Text confirmationSuspectText;
    [SerializeField] private TMP_Text confirmationRoleText;
    [SerializeField] private TMP_Text confirmationClueText;
    [SerializeField] private Sprite kimMinSuPortrait;
    [SerializeField] private Sprite leeSeoYeonPortrait;
    [SerializeField] private Sprite parkJinHoPortrait;

    [Header("Visual Settings")]
    [SerializeField] private Color selectedOutlineColor = new(0.82f, 0.70f, 0.43f, 1f);

    private bool hasSelection;
    private SuspectId selectedSuspect;
    private bool confirmationInProgress;

    public event Action<bool> PanelVisibilityChanged;

    public bool HasSelection => hasSelection;
    public SuspectId SelectedSuspect => selectedSuspect;
    public bool IsPanelOpen => deductionPanel != null && deductionPanel.activeSelf;

    private void Awake()
    {
        ResolveControllers();
        SetPanelVisible(false);
        SetConfirmationVisible(false);
        RefreshSelectionVisuals();
    }

    private void OnEnable()
    {
        ResolveControllers();
        RegisterButtonListeners();
        SubscribeToEvents();
        SynchronizeState();
    }

    private void OnDisable()
    {
        UnregisterButtonListeners();
        UnsubscribeFromEvents();
    }

    private void OnDestroy()
    {
        UnregisterButtonListeners();
        UnsubscribeFromEvents();
    }

    [ContextMenu("Debug / Open Deduction Panel")]
    public void OpenPanel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[사건 추리 UI] 패널 테스트는 Play Mode에서만 실행할 수 있습니다.", this);
            return;
        }

        ResolveControllers();
        if (deductionController == null)
        {
            Debug.LogError("[사건 추리 UI] DeductionController가 연결되지 않았습니다.", this);
            return;
        }

        if (!deductionController.IsDeductionAvailable)
        {
            return;
        }

        SetPanelVisible(true);
        SetConfirmationVisible(false);
        SynchronizeState();
    }

    public void ClosePanel()
    {
        SetConfirmationVisible(false);
        SetPanelVisible(false);
        if (resultController == null || !resultController.HasResult)
            ClearPreviewSelection();
    }

    public void SelectKimMinSu() => SetPreviewSelection(SuspectId.KimMinSu);
    public void SelectLeeSeoYeon() => SetPreviewSelection(SuspectId.LeeSeoYeon);
    public void SelectParkJinHo() => SetPreviewSelection(SuspectId.ParkJinHo);

    // 기존 공개 API를 유지한다. 이제 이 단계에서는 판정하지 않고 확인 팝업만 연다.
    public void ConfirmSelection()
    {
        if (!CanConfirmSelection())
            return;

        PopulateConfirmation();
        SetConfirmationVisible(true);
        RefreshInteractableState();
    }

    public void CancelConfirmation()
    {
        if (confirmationInProgress)
            return;
        SetConfirmationVisible(false);
        RefreshInteractableState();
    }

    // 확인 팝업의 "예, 지목합니다" 버튼만 최종 판정을 요청한다.
    public void ConfirmAccusation()
    {
        if (confirmationInProgress || !IsConfirmationVisible() || !CanConfirmSelection())
            return;

        confirmationInProgress = true;
        RefreshInteractableState();
        CaseType selectedCaseType = caseTypeSelectionController.SelectedCaseType;
        bool accepted = deductionController.TrySelectSuspect(selectedCaseType, selectedSuspect);
        if (!accepted && !resultController.HasResult)
        {
            confirmationInProgress = false;
            SetStatus("지목을 처리하지 못했습니다. 추리 조건을 다시 확인해 주세요.");
            RefreshInteractableState();
        }
    }

    private bool CanConfirmSelection()
    {
        ResolveControllers();
        if (caseTypeSelectionController == null || deductionController == null || resultController == null)
        {
            SetStatus("추리 시스템 연결을 확인해 주세요.");
            Debug.LogError("[사건 추리 UI] DeductionController 또는 ResultController가 연결되지 않았습니다.", this);
            return false;
        }
        if (!deductionController.IsDeductionAvailable)
        {
            SetStatus("필수 증거를 모두 수집해야 지목할 수 있습니다.");
            return false;
        }
        if (!hasSelection)
        {
            SetStatus("먼저 용의자 한 명을 선택해 주세요.");
            return false;
        }
        if (!caseTypeSelectionController.HasSelection ||
            caseTypeSelectionController.SelectedCaseType == CaseType.None)
        {
            SetStatus("먼저 사건 유형을 선택해 주세요.");
            return false;
        }
        if (resultController.HasResult)
        {
            SetStatus("최종 지목 결과가 이미 확정되었습니다.");
            return false;
        }
        return true;
    }

    private void SetPreviewSelection(SuspectId suspect)
    {
        if (!IsPanelOpen || confirmationInProgress || IsConfirmationVisible())
            return;
        if (deductionController == null || !deductionController.IsDeductionAvailable)
        {
            SetStatus("필수 증거를 모두 수집해야 용의자를 선택할 수 있습니다.");
            return;
        }
        if (resultController != null && resultController.HasResult)
            return;

        hasSelection = true;
        selectedSuspect = suspect;
        SetStatus($"{GetSuspectDisplayName(suspect)} 선택 · 결론 제출 전까지 변경할 수 있습니다.");
        RefreshSelectionVisuals();
        RefreshInteractableState();
    }

    private void ClearPreviewSelection()
    {
        hasSelection = false;
        confirmationInProgress = false;
        SetConfirmationVisible(false);
        RefreshSelectionVisuals();
        RefreshInteractableState();
    }

    private void PopulateConfirmation()
    {
        string name = GetSuspectDisplayName(selectedSuspect);
        if (confirmationQuestionText != null)
            confirmationQuestionText.text = $"{name}을(를) 범인으로 지목하시겠습니까?";
        if (confirmationSuspectText != null)
            confirmationSuspectText.text = $"{name}   /   용의자 {GetSuspectCode(selectedSuspect)}";
        if (confirmationRoleText != null)
            confirmationRoleText.text = GetSuspectRole(selectedSuspect);
        if (confirmationClueText != null)
            confirmationClueText.text = GetSuspectClue(selectedSuspect);
        if (confirmationPortrait != null)
            confirmationPortrait.sprite = GetSuspectPortrait(selectedSuspect);
    }

    private void HandleDeductionAvailabilityChanged(bool available)
    {
        if (!available && IsPanelOpen)
            ClosePanel();
        RefreshInteractableState();
    }

    private void HandleResultDetermined(SuspectId suspect, bool _)
    {
        confirmationInProgress = false;
        hasSelection = true;
        selectedSuspect = suspect;
        SetConfirmationVisible(false);
        SetPanelVisible(false);
    }

    private void SynchronizeState()
    {
        bool available = deductionController != null && deductionController.IsDeductionAvailable;
        if (resultController != null && resultController.HasResult)
        {
            hasSelection = true;
            selectedSuspect = resultController.SelectedSuspect;
            confirmationInProgress = false;
            SetConfirmationVisible(false);
        }
        else
        {
            SetStatus(available
                ? "용의자를 선택하면 최종 결론 제출 버튼이 활성화됩니다."
                : "필수 증거 4개를 모두 수집해야 사건 추리를 시작할 수 있습니다.");
        }
        RefreshSelectionVisuals();
        RefreshInteractableState();
    }

    private void RefreshSelectionVisuals()
    {
        SetOutlineSelected(kimMinSuSelectionOutline, hasSelection && selectedSuspect == SuspectId.KimMinSu);
        SetOutlineSelected(leeSeoYeonSelectionOutline, hasSelection && selectedSuspect == SuspectId.LeeSeoYeon);
        SetOutlineSelected(parkJinHoSelectionOutline, hasSelection && selectedSuspect == SuspectId.ParkJinHo);
        if (currentSelectionText != null)
            currentSelectionText.text = hasSelection ? GetSuspectDisplayName(selectedSuspect) : "없음";
    }

    private void SetOutlineSelected(Outline outline, bool selected)
    {
        if (outline == null)
            return;
        outline.effectColor = selectedOutlineColor;
        outline.effectDistance = new Vector2(4f, -4f);
        outline.enabled = selected;
    }

    private void RefreshInteractableState()
    {
        bool available = deductionController != null && deductionController.IsDeductionAvailable;
        bool hasResult = resultController != null && resultController.HasResult;
        bool popup = IsConfirmationVisible();
        bool allowCards = IsPanelOpen && available && !hasResult && !confirmationInProgress && !popup;

        if (kimMinSuButton != null) kimMinSuButton.interactable = allowCards;
        if (leeSeoYeonButton != null) leeSeoYeonButton.interactable = allowCards;
        if (parkJinHoButton != null) parkJinHoButton.interactable = allowCards;
        if (confirmButton != null) confirmButton.interactable = allowCards && hasSelection;
        if (reviewButton != null) reviewButton.interactable = IsPanelOpen && !popup;
        if (confirmationCancelButton != null) confirmationCancelButton.interactable = popup && !confirmationInProgress;
        if (confirmationAcceptButton != null) confirmationAcceptButton.interactable = popup && !confirmationInProgress;
    }

    private void SubscribeToEvents()
    {
        if (deductionController != null)
        {
            deductionController.DeductionAvailabilityChanged -= HandleDeductionAvailabilityChanged;
            deductionController.DeductionAvailabilityChanged += HandleDeductionAvailabilityChanged;
        }
        if (resultController != null)
        {
            resultController.ResultDetermined -= HandleResultDetermined;
            resultController.ResultDetermined += HandleResultDetermined;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (deductionController != null)
            deductionController.DeductionAvailabilityChanged -= HandleDeductionAvailabilityChanged;
        if (resultController != null)
            resultController.ResultDetermined -= HandleResultDetermined;
    }

    private void RegisterButtonListeners()
    {
        UnregisterButtonListeners();
        if (kimMinSuButton != null) kimMinSuButton.onClick.AddListener(SelectKimMinSu);
        if (leeSeoYeonButton != null) leeSeoYeonButton.onClick.AddListener(SelectLeeSeoYeon);
        if (parkJinHoButton != null) parkJinHoButton.onClick.AddListener(SelectParkJinHo);
        if (reviewButton != null) reviewButton.onClick.AddListener(ClosePanel);
        if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmSelection);
        if (confirmationCancelButton != null) confirmationCancelButton.onClick.AddListener(CancelConfirmation);
        if (confirmationAcceptButton != null) confirmationAcceptButton.onClick.AddListener(ConfirmAccusation);
    }

    private void UnregisterButtonListeners()
    {
        if (kimMinSuButton != null) kimMinSuButton.onClick.RemoveListener(SelectKimMinSu);
        if (leeSeoYeonButton != null) leeSeoYeonButton.onClick.RemoveListener(SelectLeeSeoYeon);
        if (parkJinHoButton != null) parkJinHoButton.onClick.RemoveListener(SelectParkJinHo);
        if (reviewButton != null) reviewButton.onClick.RemoveListener(ClosePanel);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(ConfirmSelection);
        if (confirmationCancelButton != null) confirmationCancelButton.onClick.RemoveListener(CancelConfirmation);
        if (confirmationAcceptButton != null) confirmationAcceptButton.onClick.RemoveListener(ConfirmAccusation);
    }

    private void ResolveControllers()
    {
        if (caseTypeSelectionController == null)
            caseTypeSelectionController =
                FindFirstObjectByType<CaseTypeSelectionController>(FindObjectsInactive.Include);
        if (deductionController == null)
            deductionController = GetComponentInParent<DeductionController>(true);
        if (resultController == null)
            resultController = GetComponentInParent<ResultController>(true);
    }

    private void SetPanelVisible(bool visible)
    {
        bool changed = deductionPanel != null && deductionPanel.activeSelf != visible;
        if (changed)
        {
            deductionPanel.SetActive(visible);
            PanelVisibilityChanged?.Invoke(visible);
        }
        RefreshInteractableState();
    }

    private void SetConfirmationVisible(bool visible)
    {
        if (confirmationPanel != null && confirmationPanel.activeSelf != visible)
            confirmationPanel.SetActive(visible);
    }

    private bool IsConfirmationVisible() => confirmationPanel != null && confirmationPanel.activeSelf;

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private Sprite GetSuspectPortrait(SuspectId suspect) => suspect switch
    {
        SuspectId.KimMinSu => kimMinSuPortrait,
        SuspectId.LeeSeoYeon => leeSeoYeonPortrait,
        SuspectId.ParkJinHo => parkJinHoPortrait,
        _ => null
    };

    private static string GetSuspectDisplayName(SuspectId suspect) => suspect switch
    {
        SuspectId.KimMinSu => "김민수",
        SuspectId.LeeSeoYeon => "이서연",
        SuspectId.ParkJinHo => "박진호",
        _ => suspect.ToString()
    };

    private static string GetSuspectCode(SuspectId suspect) => suspect switch
    {
        SuspectId.KimMinSu => "A",
        SuspectId.LeeSeoYeon => "B",
        SuspectId.ParkJinHo => "C",
        _ => "-"
    };

    private static string GetSuspectRole(SuspectId suspect) => suspect switch
    {
        SuspectId.KimMinSu => "생명공학 연구원",
        SuspectId.LeeSeoYeon => "약학 연구원",
        SuspectId.ParkJinHo => "연구 총괄 교수",
        _ => string.Empty
    };

    private static string GetSuspectClue(SuspectId suspect) => suspect switch
    {
        SuspectId.KimMinSu => "연구 성과 및 피해자와의 갈등 정황",
        SuspectId.LeeSeoYeon => "과거 실험 기록 및 연구 보고서 관련 정황",
        SuspectId.ParkJinHo => "연구비 집행 및 연구실 운영 관련 정황",
        _ => string.Empty
    };
}
