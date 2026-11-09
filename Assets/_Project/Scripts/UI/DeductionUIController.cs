using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DeductionUIController : MonoBehaviour
{
    [Header("Game Logic")]
    [SerializeField] private DeductionController deductionController;
    [SerializeField] private ResultController resultController;

    [Header("Panel")]
    [Tooltip("Keep this controller on an always-active Canvas root, not on this panel.")]
    [SerializeField] private GameObject deductionPanel;
    [SerializeField] private TMP_Text statusText;

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

    [Header("Result")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Image resultBackground;
    [SerializeField] private TMP_Text resultIconText;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultDescriptionText;
    [SerializeField] private TMP_Text resultSupportText;

    [Header("Visual Settings")]
    [SerializeField] private Color selectedOutlineColor = new(1f, 0.78f, 0.18f, 1f);
    [SerializeField] private Color successColor = new(0.57f, 0.88f, 0.72f, 1f);
    [SerializeField] private Color failureColor = new(1f, 0.58f, 0.52f, 1f);
    [SerializeField] private Color successBackgroundColor = new(0.08f, 0.17f, 0.14f, 0.98f);
    [SerializeField] private Color failureBackgroundColor = new(0.24f, 0.07f, 0.06f, 0.98f);

    private bool hasSelection;
    private SuspectId selectedSuspect;
    private bool confirmationInProgress;

    public event Action<SuspectId> SuspectPreviewChanged;
    public event Action<SuspectId, bool> ResultPresented;

    public bool HasSelection => hasSelection;
    public SuspectId SelectedSuspect => selectedSuspect;
    public bool IsPanelOpen => deductionPanel != null && deductionPanel.activeSelf;

    private void Awake()
    {
        ResolveControllers();
        ResolveVisualReferences();
        SetPanelVisible(false);
        SetResultVisible(false);
        RefreshSelectionVisuals();
    }

    private void OnEnable()
    {
        ResolveControllers();
        ResolveVisualReferences();
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

    private void Reset()
    {
        ResolveControllers();
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
            Debug.Log("[사건 추리 UI] 필수 증거가 부족하여 사건 추리 패널을 열 수 없습니다.", this);
            return;
        }

        SetPanelVisible(true);
        SynchronizeState();
    }

    public void ClosePanel()
    {
        SetPanelVisible(false);

        if (resultController == null || !resultController.HasResult)
            ClearPreviewSelection();
    }

    public void SelectKimMinSu()
    {
        SetPreviewSelection(SuspectId.KimMinSu);
    }

    public void SelectLeeSeoYeon()
    {
        SetPreviewSelection(SuspectId.LeeSeoYeon);
    }

    public void SelectParkJinHo()
    {
        SetPreviewSelection(SuspectId.ParkJinHo);
    }

    public void ConfirmSelection()
    {
        if (confirmationInProgress)
            return;

        ResolveControllers();

        if (deductionController == null || resultController == null)
        {
            SetStatus("추리 시스템 연결을 확인해 주세요.");
            Debug.LogError("[사건 추리 UI] DeductionController 또는 ResultController가 연결되지 않았습니다.", this);
            return;
        }

        if (!deductionController.IsDeductionAvailable)
        {
            SetStatus("필수 증거를 모두 수집해야 지목할 수 있습니다.");
            Debug.Log("[사건 추리 UI] 추리가 잠겨 있어 지목을 차단했습니다.", this);
            RefreshInteractableState();
            return;
        }

        if (!hasSelection)
        {
            SetStatus("먼저 용의자 한 명을 선택해 주세요.");
            Debug.Log("[사건 추리 UI] 선택된 용의자가 없어 지목을 차단했습니다.", this);
            RefreshInteractableState();
            return;
        }

        if (resultController.HasResult)
        {
            SetStatus("최종 지목 결과가 이미 확정되었습니다.");
            RefreshInteractableState();
            return;
        }

        confirmationInProgress = true;
        RefreshInteractableState();

        bool accepted = deductionController.TrySelectSuspect(selectedSuspect);
        if (!accepted && !resultController.HasResult)
        {
            confirmationInProgress = false;
            SetStatus("지목을 처리하지 못했습니다. 추리 조건을 확인해 주세요.");
            RefreshInteractableState();
        }
    }

    private void SetPreviewSelection(SuspectId suspect)
    {
        if (!IsPanelOpen || confirmationInProgress)
            return;

        if (deductionController == null || !deductionController.IsDeductionAvailable)
        {
            SetStatus("필수 증거를 모두 수집해야 용의자를 선택할 수 있습니다.");
            return;
        }

        if (resultController != null && resultController.HasResult)
        {
            SetStatus("최종 지목 결과가 이미 확정되었습니다.");
            return;
        }

        hasSelection = true;
        selectedSuspect = suspect;
        SetResultVisible(false);
        SetStatus($"{GetSuspectDisplayName(suspect)} 선택 — 지목 버튼을 눌러 확정하세요.");
        RefreshSelectionVisuals();
        RefreshInteractableState();
        SuspectPreviewChanged?.Invoke(suspect);
    }

    private void ClearPreviewSelection()
    {
        hasSelection = false;
        confirmationInProgress = false;
        RefreshSelectionVisuals();
        RefreshInteractableState();
    }

    private void HandleDeductionAvailabilityChanged(bool isAvailable)
    {
        if (!isAvailable && IsPanelOpen)
            SetPanelVisible(false);

        SetStatus(isAvailable
            ? "수집한 증거와 용의자 정보를 비교해 최종 범인을 지목하세요."
            : "필수 증거 4개를 모두 수집해야 사건 추리를 시작할 수 있습니다.");
        RefreshInteractableState();
    }

    private void HandleResultDetermined(SuspectId suspect, bool isSuccess)
    {
        confirmationInProgress = false;
        hasSelection = true;
        selectedSuspect = suspect;
        RefreshSelectionVisuals();
        ShowResult(suspect, isSuccess);
        RefreshInteractableState();
        ResultPresented?.Invoke(suspect, isSuccess);
    }

    private void ShowResult(SuspectId suspect, bool isSuccess)
    {
        SetResultVisible(true);

        if (resultBackground != null)
            resultBackground.color = isSuccess ? successBackgroundColor : failureBackgroundColor;

        if (resultIconText != null)
        {
            resultIconText.text = isSuccess ? "✓" : "✕";
            resultIconText.color = isSuccess ? successColor : failureColor;
        }

        if (resultTitleText != null)
        {
            resultTitleText.text = isSuccess ? "사건 해결" : "추리 실패";
            resultTitleText.color = Color.white;
        }

        if (resultDescriptionText != null)
        {
            string suspectName = GetSuspectDisplayName(suspect);
            resultDescriptionText.text = $"{suspectName}을 지목했습니다.";
        }

        if (resultSupportText != null)
        {
            resultSupportText.text = isSuccess
                ? "수집한 증거를 통해 사건의 진실을 밝혀냈습니다."
                : "수집한 증거를 다시 검토해 보세요.";
        }

        SetStatus("최종 지목 결과가 확정되었습니다.");
    }

    private void SynchronizeState()
    {
        bool isAvailable = deductionController != null && deductionController.IsDeductionAvailable;

        if (resultController != null && resultController.HasResult)
        {
            hasSelection = true;
            selectedSuspect = resultController.SelectedSuspect;
            confirmationInProgress = false;
            ShowResult(resultController.SelectedSuspect, resultController.IsSuccess);
        }
        else
        {
            SetResultVisible(false);
            SetStatus(isAvailable
                ? "수집한 증거와 용의자 정보를 비교해 최종 범인을 지목하세요."
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
    }

    private void SetOutlineSelected(Outline outline, bool selected)
    {
        if (outline == null)
            return;

        outline.effectColor = selectedOutlineColor;
        outline.enabled = selected;
    }

    private void RefreshInteractableState()
    {
        bool isAvailable = deductionController != null && deductionController.IsDeductionAvailable;
        bool hasResult = resultController != null && resultController.HasResult;
        bool allowCardInput = IsPanelOpen && isAvailable && !hasResult && !confirmationInProgress;

        if (kimMinSuButton != null)
            kimMinSuButton.interactable = allowCardInput;

        if (leeSeoYeonButton != null)
            leeSeoYeonButton.interactable = allowCardInput;

        if (parkJinHoButton != null)
            parkJinHoButton.interactable = allowCardInput;

        if (confirmButton != null)
            confirmButton.interactable = allowCardInput && hasSelection;

        if (reviewButton != null)
            reviewButton.interactable = IsPanelOpen;
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

    private void RegisterButtonListeners()
    {
        UnregisterButtonListeners();

        if (kimMinSuButton != null)
            kimMinSuButton.onClick.AddListener(SelectKimMinSu);

        if (leeSeoYeonButton != null)
            leeSeoYeonButton.onClick.AddListener(SelectLeeSeoYeon);

        if (parkJinHoButton != null)
            parkJinHoButton.onClick.AddListener(SelectParkJinHo);

        if (reviewButton != null)
            reviewButton.onClick.AddListener(ClosePanel);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmSelection);
    }

    private void UnregisterButtonListeners()
    {
        if (kimMinSuButton != null)
            kimMinSuButton.onClick.RemoveListener(SelectKimMinSu);

        if (leeSeoYeonButton != null)
            leeSeoYeonButton.onClick.RemoveListener(SelectLeeSeoYeon);

        if (parkJinHoButton != null)
            parkJinHoButton.onClick.RemoveListener(SelectParkJinHo);

        if (reviewButton != null)
            reviewButton.onClick.RemoveListener(ClosePanel);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmSelection);
    }

    private void UnsubscribeFromEvents()
    {
        if (deductionController != null)
            deductionController.DeductionAvailabilityChanged -= HandleDeductionAvailabilityChanged;

        if (resultController != null)
            resultController.ResultDetermined -= HandleResultDetermined;
    }

    private void ResolveControllers()
    {
        if (deductionController == null)
            deductionController = GetComponentInParent<DeductionController>(true);

        if (resultController == null)
            resultController = GetComponentInParent<ResultController>(true);
    }

    private void ResolveVisualReferences()
    {
        if (resultBackground == null && resultPanel != null)
            resultBackground = resultPanel.GetComponent<Image>();
    }

    private void SetPanelVisible(bool visible)
    {
        if (deductionPanel != null && deductionPanel.activeSelf != visible)
            deductionPanel.SetActive(visible);

        RefreshInteractableState();
    }

    private void SetResultVisible(bool visible)
    {
        if (resultPanel != null && resultPanel.activeSelf != visible)
            resultPanel.SetActive(visible);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private static string GetSuspectDisplayName(SuspectId suspect)
    {
        return suspect switch
        {
            SuspectId.KimMinSu => "김민수",
            SuspectId.LeeSeoYeon => "이서연",
            SuspectId.ParkJinHo => "박진호",
            _ => suspect.ToString()
        };
    }
}
