using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 필수 증거 진행도와 사건 추리 시작 버튼만 담당한다.
/// 실제 추리판 표시, 용의자 선택 및 결과 판정은 기존 컨트롤러에 위임한다.
/// </summary>
public sealed class DeductionStartUIController : MonoBehaviour
{
    private const int DefaultRequiredEvidenceTotal = 4;

    [Header("UI")]
    [SerializeField] private GameObject startAreaRoot;
    [SerializeField] private Button deductionStartButton;
    [SerializeField] private Button continueInvestigationButton;
    [SerializeField] private TMP_Text guidanceText;
    [SerializeField] private TMP_Text requiredEvidenceProgressText;
    [SerializeField] private Image[] requiredEvidenceIndicators;

    [Header("Existing Controllers")]
    [SerializeField] private CaseTypeSelectionController caseTypeSelectionController;
    [SerializeField] private DeductionUIController deductionUIController;
    [SerializeField] private ResultController resultController;

    [Header("Progress Display")]
    [Min(1)]
    [SerializeField] private int requiredEvidenceTotal = DefaultRequiredEvidenceTotal;
    [SerializeField] private Color collectedIndicatorColor = new(0.26f, 0.65f, 0.61f, 1f);
    [SerializeField] private Color missingIndicatorColor = new(0.28f, 0.34f, 0.39f, 1f);

    private EvidenceManager subscribedManager;
    private Coroutine managerSubscribeRoutine;
    private bool unlockPromptDismissed;
    private int lastDisplayedRequiredCount = -1;

    public bool CanOpenDeduction =>
        isActiveAndEnabled &&
        EvidenceManager.Instance != null && EvidenceManager.Instance.CanDeduce &&
        resultController != null && !resultController.HasResult &&
        caseTypeSelectionController != null && caseTypeSelectionController.CanOpenPanel &&
        !caseTypeSelectionController.IsPanelOpen &&
        deductionUIController != null && !deductionUIController.IsPanelOpen;

    private void Awake()
    {
        ResolveReferences();

        // 시작 시에는 해금 모달을 표시하지 않는다.
        if (startAreaRoot != null)
            startAreaRoot.SetActive(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        RegisterButtonListener();
        SubscribeToUIEvents();

        if (!TrySubscribeToEvidenceManager())
            managerSubscribeRoutine = StartCoroutine(SubscribeWhenManagerIsReady());

        RefreshView();
    }

    private void OnDisable()
    {
        StopManagerSubscribeRoutine();
        UnsubscribeFromEvidenceManager();
        UnsubscribeFromUIEvents();
        UnregisterButtonListener();
    }

    private void OnDestroy()
    {
        StopManagerSubscribeRoutine();
        UnsubscribeFromEvidenceManager();
        UnsubscribeFromUIEvents();
        UnregisterButtonListener();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    [ContextMenu("Debug / Open Deduction Panel")]
    public void TryOpenDeductionPanel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[추리 시작 UI] 추리판은 Play Mode에서만 열 수 있습니다.", this);
            return;
        }

        ResolveReferences();
        EvidenceManager manager = EvidenceManager.Instance;

        if (manager == null || !manager.CanDeduce)
        {
            RefreshView();
            return;
        }

        if (resultController == null)
        {
            Debug.LogError("[추리 시작 UI] ResultController가 연결되지 않았습니다.", this);
            RefreshView();
            return;
        }

        if (resultController.HasResult)
        {
            RefreshView();
            return;
        }

        if (caseTypeSelectionController == null)
        {
            Debug.LogError("[추리 시작 UI] CaseTypeSelectionController가 연결되지 않았습니다.", this);
            RefreshView();
            return;
        }

        if (!CanOpenDeduction)
            return;

        caseTypeSelectionController.OpenPanel();
        if (caseTypeSelectionController.IsPanelOpen)
            unlockPromptDismissed = true;

        RefreshView();
    }

    private void HandleNextStepRequested(CaseType requestedType)
    {
        ResolveReferences();

        if (caseTypeSelectionController == null ||
            !caseTypeSelectionController.HasSelection ||
            requestedType == CaseType.None ||
            requestedType != caseTypeSelectionController.SelectedCaseType)
        {
            Debug.LogWarning("[추리 시작 UI] 유효한 사건 유형이 없어 다음 단계로 이동하지 않습니다.", this);
            return;
        }

        EvidenceManager manager = EvidenceManager.Instance;
        if (manager == null || !manager.CanDeduce)
        {
            Debug.LogWarning("[추리 시작 UI] 추리 해금 조건이 충족되지 않았습니다.", this);
            return;
        }

        if (resultController != null && resultController.HasResult)
            return;

        if (deductionUIController == null)
        {
            Debug.LogError("[추리 시작 UI] DeductionUIController가 연결되지 않았습니다.", this);
            return;
        }

        // 선택값은 CaseTypeSelectionController에 유지한 채 화면만 순서대로 전환한다.
        caseTypeSelectionController.ClosePanel();
        bool wasPanelOpen = deductionUIController.IsPanelOpen;
        deductionUIController.OpenPanel();

        if (deductionUIController.IsPanelOpen)
        {
            // Opening a closed panel already refreshes via PanelVisibilityChanged.
            if (wasPanelOpen)
                RefreshView();
            return;
        }

        Debug.LogWarning(
            "[추리 시작 UI] 용의자 선택 화면을 열지 못해 사건 유형 화면으로 복귀합니다.",
            this);
        caseTypeSelectionController.OpenPanel();
        RefreshView();
    }

    private IEnumerator SubscribeWhenManagerIsReady()
    {
        while (isActiveAndEnabled && !TrySubscribeToEvidenceManager())
            yield return null;

        managerSubscribeRoutine = null;
        RefreshView();
    }

    private bool TrySubscribeToEvidenceManager()
    {
        EvidenceManager manager = EvidenceManager.Instance;
        if (manager == null)
            return false;

        if (subscribedManager != manager)
        {
            UnsubscribeFromEvidenceManager();
            subscribedManager = manager;
            subscribedManager.EvidenceRegistered += HandleEvidenceRegistered;
            subscribedManager.DeductionUnlocked += HandleDeductionUnlocked;
        }

        return true;
    }

    private void HandleEvidenceRegistered(EvidenceData _)
    {
        // The final required evidence fires DeductionUnlocked after registration.
        if (subscribedManager != null && !subscribedManager.CanDeduce &&
            subscribedManager.RequiredCount != lastDisplayedRequiredCount)
            RefreshView();
    }

    private void HandleDeductionUnlocked()
    {
        // 해금 모달만 표시하며 추리판은 사용자가 버튼을 눌렀을 때 연다.
        unlockPromptDismissed = false;
        RefreshView();
    }

    public void ContinueInvestigation()
    {
        unlockPromptDismissed = true;
        RefreshView();
    }

    private void HandlePanelVisibilityChanged(bool _)
    {
        // A result-driven close is handled by ResultDetermined.
        if (resultController == null || !resultController.HasResult)
            RefreshView();
    }

    private void HandleResultDetermined(SuspectId _, bool __)
    {
        RefreshView();
    }

    private void RefreshView()
    {
        EvidenceManager manager = EvidenceManager.Instance;
        bool hasManager = manager != null;
        bool hasAllRequiredEvidence = hasManager && manager.CanDeduce;
        bool isCaseTypePanelOpen = caseTypeSelectionController != null &&
                                   caseTypeSelectionController.IsPanelOpen;
        bool isDeductionPanelOpen = deductionUIController != null && deductionUIController.IsPanelOpen;
        bool isPanelOpen = isCaseTypePanelOpen || isDeductionPanelOpen;
        bool hasResult = resultController != null && resultController.HasResult;
        if (startAreaRoot != null)
        {
            bool shouldShowStartArea = hasAllRequiredEvidence &&
                                       !unlockPromptDismissed &&
                                       !isPanelOpen &&
                                       !hasResult;
            if (startAreaRoot.activeSelf != shouldShowStartArea)
                startAreaRoot.SetActive(shouldShowStartArea);
        }

        if (deductionStartButton != null)
            deductionStartButton.interactable = CanOpenDeduction;

        int collectedRequired = hasManager ? manager.RequiredCount : 0;
        lastDisplayedRequiredCount = collectedRequired;
        int total = Mathf.Max(1, requiredEvidenceTotal);

        if (requiredEvidenceProgressText != null)
            requiredEvidenceProgressText.SetText("{0} / {1}", collectedRequired, total);

        if (requiredEvidenceIndicators != null)
        {
            for (int i = 0; i < requiredEvidenceIndicators.Length; i++)
            {
                if (requiredEvidenceIndicators[i] != null)
                    requiredEvidenceIndicators[i].color = i < collectedRequired
                        ? collectedIndicatorColor
                        : missingIndicatorColor;
            }
        }

        if (guidanceText == null)
            return;

        if (hasResult)
            guidanceText.text = "추리 완료";
        else if (isPanelOpen)
            guidanceText.text = "추리 진행 중";
        else if (hasAllRequiredEvidence)
            guidanceText.text = "필수 증거 확보 완료 — 추리 가능";
        else
            guidanceText.text = "필수 증거를 모두 확보해야 합니다.";
    }

    private void RegisterButtonListener()
    {
        UnregisterButtonListener();

        if (deductionStartButton != null)
            deductionStartButton.onClick.AddListener(TryOpenDeductionPanel);

        if (continueInvestigationButton != null)
            continueInvestigationButton.onClick.AddListener(ContinueInvestigation);
    }

    private void UnregisterButtonListener()
    {
        if (deductionStartButton != null)
            deductionStartButton.onClick.RemoveListener(TryOpenDeductionPanel);

        if (continueInvestigationButton != null)
            continueInvestigationButton.onClick.RemoveListener(ContinueInvestigation);
    }

    private void SubscribeToUIEvents()
    {
        if (caseTypeSelectionController != null)
        {
            caseTypeSelectionController.NextStepRequested -= HandleNextStepRequested;
            caseTypeSelectionController.NextStepRequested += HandleNextStepRequested;
        }

        if (deductionUIController != null)
        {
            deductionUIController.PanelVisibilityChanged -= HandlePanelVisibilityChanged;
            deductionUIController.PanelVisibilityChanged += HandlePanelVisibilityChanged;
        }

        if (resultController != null)
        {
            resultController.ResultDetermined -= HandleResultDetermined;
            resultController.ResultDetermined += HandleResultDetermined;
        }
    }

    private void UnsubscribeFromUIEvents()
    {
        if (caseTypeSelectionController != null)
            caseTypeSelectionController.NextStepRequested -= HandleNextStepRequested;

        if (deductionUIController != null)
            deductionUIController.PanelVisibilityChanged -= HandlePanelVisibilityChanged;

        if (resultController != null)
            resultController.ResultDetermined -= HandleResultDetermined;
    }

    private void UnsubscribeFromEvidenceManager()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.EvidenceRegistered -= HandleEvidenceRegistered;
        subscribedManager.DeductionUnlocked -= HandleDeductionUnlocked;
        subscribedManager = null;
    }

    private void StopManagerSubscribeRoutine()
    {
        if (managerSubscribeRoutine == null)
            return;

        StopCoroutine(managerSubscribeRoutine);
        managerSubscribeRoutine = null;
    }

    private void ResolveReferences()
    {
        if (caseTypeSelectionController == null)
            caseTypeSelectionController =
                FindFirstObjectByType<CaseTypeSelectionController>(FindObjectsInactive.Include);

        if (deductionUIController == null)
            deductionUIController = GetComponentInParent<DeductionUIController>(true);

        if (deductionUIController == null)
            deductionUIController = GetComponentInChildren<DeductionUIController>(true);

        if (resultController == null)
            resultController = GetComponentInParent<ResultController>(true);

        if (resultController == null)
            resultController = GetComponentInChildren<ResultController>(true);
    }
}
