using System;
using System.Collections;
using UnityEngine;

public sealed class DeductionController : MonoBehaviour
{
    [SerializeField] private ResultController resultController;

    private EvidenceManager subscribedManager;
    private Coroutine subscribeRoutine;

    public event Action<bool> DeductionAvailabilityChanged;

    public bool IsDeductionAvailable { get; private set; }

    private void Awake()
    {
        ResolveResultController();
    }

    private void OnEnable()
    {
        if (!TrySubscribeToEvidenceManager())
            subscribeRoutine = StartCoroutine(SubscribeWhenManagerIsReady());
    }

    private void OnDisable()
    {
        StopSubscribeRoutine();
        UnsubscribeFromEvidenceManager();
    }

    private void OnDestroy()
    {
        StopSubscribeRoutine();
        UnsubscribeFromEvidenceManager();
    }

    private void Reset()
    {
        resultController = GetComponent<ResultController>();
    }

    [ContextMenu("Debug / Select Kim Min Su")]
    public void SelectKimMinSu()
    {
        TrySelectSuspect(SuspectId.KimMinSu);
    }

    [ContextMenu("Debug / Select Lee Seo Yeon")]
    public void SelectLeeSeoYeon()
    {
        TrySelectSuspect(SuspectId.LeeSeoYeon);
    }

    [ContextMenu("Debug / Select Park Jin Ho")]
    public void SelectParkJinHo()
    {
        TrySelectSuspect(SuspectId.ParkJinHo);
    }

    public bool TrySelectSuspect(SuspectId suspect)
    {
        Debug.LogWarning(
            "[최종 추리] 사건 유형 없이 용의자만 제출할 수 없습니다. 사건 유형을 함께 전달하세요.",
            this);
        return false;
    }

    public bool TrySelectSuspect(CaseType caseType, SuspectId suspect)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[최종 추리] 용의자 선택 테스트는 Play Mode에서만 실행할 수 있습니다.", this);
            return false;
        }

        EvidenceManager manager = EvidenceManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[최종 추리] EvidenceManager를 찾을 수 없어 선택할 수 없습니다.", this);
            return false;
        }

        // 이벤트를 놓친 경우에도 현재 매니저 상태를 기준으로 즉시 동기화한다.
        SetDeductionAvailability(manager.CanDeduce);

        if (!IsDeductionAvailable)
        {
            Debug.Log("[최종 추리] 필수 증거가 부족하여 아직 범인을 선택할 수 없습니다.", this);
            return false;
        }

        if (!Enum.IsDefined(typeof(CaseType), caseType) || caseType == CaseType.None)
        {
            Debug.LogWarning($"[최종 추리] 유효한 사건 유형이 선택되지 않았습니다: {caseType}", this);
            return false;
        }

        if (!Enum.IsDefined(typeof(SuspectId), suspect))
        {
            Debug.LogWarning($"[최종 추리] 유효하지 않은 용의자 ID입니다: {suspect}", this);
            return false;
        }

        ResolveResultController();
        if (resultController == null)
        {
            Debug.LogError("[최종 추리] ResultController가 연결되지 않았습니다.", this);
            return false;
        }

        if (resultController.HasResult)
        {
            Debug.Log("[최종 추리] 결과가 이미 확정되어 추가 선택을 무시합니다.", this);
            return false;
        }

        return resultController.TryDetermineResult(caseType, suspect);
    }

    private IEnumerator SubscribeWhenManagerIsReady()
    {
        while (isActiveAndEnabled && !TrySubscribeToEvidenceManager())
            yield return null;

        subscribeRoutine = null;
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
            subscribedManager.DeductionUnlocked += HandleDeductionUnlocked;
        }

        SetDeductionAvailability(manager.CanDeduce);
        return true;
    }

    private void HandleDeductionUnlocked()
    {
        EvidenceManager manager = EvidenceManager.Instance;
        SetDeductionAvailability(manager != null && manager.CanDeduce);
    }

    private void SetDeductionAvailability(bool isAvailable)
    {
        if (IsDeductionAvailable == isAvailable)
            return;

        IsDeductionAvailable = isAvailable;
        Debug.Log($"[최종 추리] 범인 선택 가능 상태: {IsDeductionAvailable}", this);
        DeductionAvailabilityChanged?.Invoke(IsDeductionAvailable);
    }

    private void ResolveResultController()
    {
        if (resultController == null)
            resultController = GetComponent<ResultController>();
    }

    private void StopSubscribeRoutine()
    {
        if (subscribeRoutine == null)
            return;

        StopCoroutine(subscribeRoutine);
        subscribeRoutine = null;
    }

    private void UnsubscribeFromEvidenceManager()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.DeductionUnlocked -= HandleDeductionUnlocked;
        subscribedManager = null;
    }
}
