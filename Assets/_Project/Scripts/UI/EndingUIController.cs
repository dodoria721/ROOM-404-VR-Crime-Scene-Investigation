using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ResultController가 확정한 결과를 성공/실패 엔딩 UI에 표시하고
/// 재수사 및 메인 메뉴 Scene 전환을 기존 SceneFlowController에 위임한다.
/// 이 컴포넌트가 붙은 GameObject는 항상 활성 상태로 유지해야 한다.
/// </summary>
public sealed class EndingUIController : MonoBehaviour
{
    [Header("Game Logic")]
    [SerializeField] private ResultController resultController;
    [SerializeField] private SceneFlowController sceneFlowController;

    [Header("Ending UI")]
    [Tooltip("엔딩 시각 요소의 공통 루트입니다. 이 컴포넌트가 붙은 GameObject 자체를 연결하지 마세요.")]
    [SerializeField] private GameObject endingRoot;
    [SerializeField] private GameObject successPanel;
    [SerializeField] private GameObject failurePanel;
    [SerializeField] private GameObject caseRecordPanel;

    [Header("Buttons")]
    [SerializeField] private Button caseRecordButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button successMainMenuButton;
    [SerializeField] private Button failureMainMenuButton;

    [Header("Deduction UI")]
    [Tooltip("결과 확정 시 기존 추리 패널을 닫는 데 사용합니다.")]
    [SerializeField] private DeductionUIController deductionUIController;
    [Tooltip("DeductionCanvas 등 추리 UI의 별도 시각 루트입니다. 필요할 때만 연결하세요.")]
    [SerializeField] private GameObject deductionUIRoot;

    private bool hasPresentedResult;
    private SuspectId presentedSuspect;
    private bool presentedSuccess;

    private void Awake()
    {
        ResolveReferences();
        HideEnding();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ValidateReferences();
        RegisterButtonListeners();
        SubscribeToResult();
        SynchronizeResultState();
    }

    private void OnDisable()
    {
        UnregisterButtonListeners();
        UnsubscribeFromResult();
    }

    private void OnDestroy()
    {
        UnregisterButtonListeners();
        UnsubscribeFromResult();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 성공 엔딩의 사건 기록 패널을 열거나 닫는다.
    /// 실패 엔딩에서는 정답 정보 노출을 막기 위해 동작하지 않는다.
    /// </summary>
    public void ToggleCaseRecord()
    {
        if (resultController == null || !resultController.HasResult || !resultController.IsSuccess)
        {
            Debug.LogWarning("[엔딩 UI] 성공 결과가 확정되기 전에는 사건 기록을 열 수 없습니다.", this);
            return;
        }

        if (caseRecordPanel == null)
        {
            Debug.LogError("[엔딩 UI] CaseRecordPanel이 연결되지 않았습니다.", this);
            return;
        }

        caseRecordPanel.SetActive(!caseRecordPanel.activeSelf);
    }

    public void RetryInvestigation()
    {
        if (sceneFlowController == null)
        {
            Debug.LogError("[엔딩 UI] SceneFlowController가 연결되지 않아 재수사를 시작할 수 없습니다.", this);
            return;
        }

        sceneFlowController.RestartInvestigation();
    }

    public void ReturnToMainMenu()
    {
        if (sceneFlowController == null)
        {
            Debug.LogError("[엔딩 UI] SceneFlowController가 연결되지 않아 메인 메뉴로 이동할 수 없습니다.", this);
            return;
        }

        sceneFlowController.ReturnToMainMenu();
    }

    private void HandleResultDetermined(SuspectId suspect, bool isSuccess)
    {
        PresentResult(suspect, isSuccess, false);
    }

    private void SynchronizeResultState()
    {
        if (resultController == null || !resultController.HasResult)
        {
            hasPresentedResult = false;
            HideEnding();
            return;
        }

        PresentResult(
            resultController.SelectedSuspect,
            resultController.IsSuccess,
            true);
    }

    private void PresentResult(SuspectId suspect, bool isSuccess, bool forceRefresh)
    {
        bool isDuplicate = hasPresentedResult &&
                           presentedSuspect == suspect &&
                           presentedSuccess == isSuccess;
        if (isDuplicate && !forceRefresh)
            return;

        hasPresentedResult = true;
        presentedSuspect = suspect;
        presentedSuccess = isSuccess;

        HideDeductionUI();

        SetActive(endingRoot, true);
        SetActive(successPanel, isSuccess);
        SetActive(failurePanel, !isSuccess);
        SetActive(caseRecordPanel, false);
    }

    private void HideDeductionUI()
    {
        if (deductionUIController != null)
            deductionUIController.ClosePanel();

        if (deductionUIRoot != null && deductionUIRoot != gameObject)
            deductionUIRoot.SetActive(false);
    }

    private void HideEnding()
    {
        SetActive(successPanel, false);
        SetActive(failurePanel, false);
        SetActive(caseRecordPanel, false);

        if (endingRoot != gameObject)
            SetActive(endingRoot, false);
    }

    private void RegisterButtonListeners()
    {
        UnregisterButtonListeners();

        if (caseRecordButton != null)
            caseRecordButton.onClick.AddListener(ToggleCaseRecord);

        if (retryButton != null)
            retryButton.onClick.AddListener(RetryInvestigation);

        if (successMainMenuButton != null)
            successMainMenuButton.onClick.AddListener(ReturnToMainMenu);

        if (failureMainMenuButton != null)
            failureMainMenuButton.onClick.AddListener(ReturnToMainMenu);
    }

    private void UnregisterButtonListeners()
    {
        if (caseRecordButton != null)
            caseRecordButton.onClick.RemoveListener(ToggleCaseRecord);

        if (retryButton != null)
            retryButton.onClick.RemoveListener(RetryInvestigation);

        if (successMainMenuButton != null)
            successMainMenuButton.onClick.RemoveListener(ReturnToMainMenu);

        if (failureMainMenuButton != null)
            failureMainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
    }

    private void SubscribeToResult()
    {
        if (resultController == null)
            return;

        resultController.ResultDetermined -= HandleResultDetermined;
        resultController.ResultDetermined += HandleResultDetermined;
    }

    private void UnsubscribeFromResult()
    {
        if (resultController != null)
            resultController.ResultDetermined -= HandleResultDetermined;
    }

    private void ResolveReferences()
    {
        if (resultController == null)
            resultController = FindFirstObjectByType<ResultController>(FindObjectsInactive.Include);

        if (sceneFlowController == null)
            sceneFlowController = FindFirstObjectByType<SceneFlowController>(FindObjectsInactive.Include);

        if (deductionUIController == null)
            deductionUIController = FindFirstObjectByType<DeductionUIController>(FindObjectsInactive.Include);
    }

    private void ValidateReferences()
    {
        LogMissing(resultController, nameof(resultController));
        LogMissing(sceneFlowController, nameof(sceneFlowController));
        LogMissing(endingRoot, nameof(endingRoot));
        LogMissing(successPanel, nameof(successPanel));
        LogMissing(failurePanel, nameof(failurePanel));
        LogMissing(caseRecordPanel, nameof(caseRecordPanel));
        LogMissing(caseRecordButton, nameof(caseRecordButton));
        LogMissing(retryButton, nameof(retryButton));
        LogMissing(successMainMenuButton, nameof(successMainMenuButton));
        LogMissing(failureMainMenuButton, nameof(failureMainMenuButton));

        if (endingRoot == gameObject)
        {
            Debug.LogError(
                "[엔딩 UI] EndingRoot에는 EndingUIController가 붙은 GameObject가 아닌 자식 시각 루트를 연결해야 합니다.",
                this);
        }

        if (deductionUIRoot == gameObject)
        {
            Debug.LogError(
                "[엔딩 UI] DeductionUIRoot에 EndingUIController GameObject를 연결할 수 없습니다.",
                this);
        }
    }

    private void LogMissing(Object reference, string fieldName)
    {
        if (reference == null)
            Debug.LogError($"[엔딩 UI] Inspector 참조가 누락되었습니다: {fieldName}", this);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
