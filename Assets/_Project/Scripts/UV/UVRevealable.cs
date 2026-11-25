using System;
using System.Collections;
using UnityEngine;

public sealed class UVRevealable : MonoBehaviour
{
    [Header("Hidden Visual")]
    [Tooltip("Keep this object active. Only its Renderer components are shown or hidden.")]
    [SerializeField] private GameObject hiddenVisualRoot;
    [SerializeField] private Renderer[] hiddenRenderers;

    [Header("Discovery")]
    [Min(0f)]
    [SerializeField] private float requiredIlluminationTime = 1.5f;
    [SerializeField] private bool resetProgressWhenLightLeaves = true;

    [Header("Evidence Collection")]
    [SerializeField] private EvidenceSource evidenceSource;

    private EvidenceManager subscribedManager;
    private Coroutine managerSubscriptionRoutine;
    private float illuminationTime;

    public event Action<UVRevealable> Discovered;

    public bool IsIlluminated { get; private set; }
    public bool IsDiscovered { get; private set; }
    public bool IsEvidenceCollected { get; private set; }
    public float IlluminationProgress => requiredIlluminationTime <= 0f
        ? (IsDiscovered ? 1f : 0f)
        : Mathf.Clamp01(illuminationTime / requiredIlluminationTime);

    private void Awake()
    {
        ResolveReferences();
        SetVisualVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        IsIlluminated = false;
        SetVisualVisible(false);

        if (!TrySubscribeToEvidenceManager())
            managerSubscriptionRoutine = StartCoroutine(WaitForEvidenceManager());
    }

    private void OnDisable()
    {
        IsIlluminated = false;
        SetVisualVisible(false);
        StopManagerSubscriptionRoutine();
        UnsubscribeFromEvidenceManager();
    }

    private void OnDestroy()
    {
        StopManagerSubscriptionRoutine();
        UnsubscribeFromEvidenceManager();
    }

    private void Update()
    {
        if (!IsIlluminated || IsDiscovered)
            return;

        illuminationTime += Time.deltaTime;
        if (illuminationTime >= requiredIlluminationTime)
            MarkDiscovered();
    }

    public void SetIlluminated(bool illuminated)
    {
        if (IsIlluminated == illuminated)
            return;

        IsIlluminated = illuminated;
        SetVisualVisible(illuminated);

        if (illuminated && !IsDiscovered && requiredIlluminationTime <= 0f)
        {
            MarkDiscovered();
        }
        else if (!illuminated && !IsDiscovered && resetProgressWhenLightLeaves)
        {
            illuminationTime = 0f;
        }
    }

    public bool TryConfirmEvidence()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UV Evidence] 증거 확인은 Play Mode에서만 실행할 수 있습니다.", this);
            return false;
        }

        if (!IsDiscovered)
        {
            return false;
        }

        if (evidenceSource == null)
        {
            Debug.LogError("[UV Evidence] EvidenceSource가 연결되지 않았습니다.", this);
            return false;
        }

        SynchronizeCollectionState();
        if (IsEvidenceCollected)
        {
            return false;
        }

        evidenceSource.Collect();
        SynchronizeCollectionState();
        return IsEvidenceCollected;
    }

    // XR Simple Interactable의 UnityEvent에는 반환값 없는 메서드를 연결한다.
    public void ConfirmEvidence()
    {
        TryConfirmEvidence();
    }

    [ContextMenu("Debug / Confirm Evidence")]
    private void DebugConfirmEvidence()
    {
        TryConfirmEvidence();
    }

    private void MarkDiscovered()
    {
        if (IsDiscovered)
            return;

        IsDiscovered = true;
        illuminationTime = Mathf.Max(illuminationTime, requiredIlluminationTime);
        Debug.Log($"[UV Evidence] 숨겨진 흔적 발견: {name}", this);
        Discovered?.Invoke(this);
    }

    private void SetVisualVisible(bool visible)
    {
        if (hiddenRenderers == null)
            return;

        foreach (Renderer rendererComponent in hiddenRenderers)
        {
            if (rendererComponent != null)
                rendererComponent.enabled = visible;
        }
    }

    private void ResolveReferences()
    {
        if (evidenceSource == null)
            evidenceSource = GetComponent<EvidenceSource>();

        if ((hiddenRenderers == null || hiddenRenderers.Length == 0) && hiddenVisualRoot != null)
            hiddenRenderers = hiddenVisualRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    private IEnumerator WaitForEvidenceManager()
    {
        while (isActiveAndEnabled && EvidenceManager.Instance == null)
            yield return null;

        managerSubscriptionRoutine = null;

        if (isActiveAndEnabled)
            TrySubscribeToEvidenceManager();
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
        }

        SynchronizeCollectionState();
        return true;
    }

    private void HandleEvidenceRegistered(EvidenceData data)
    {
        if (data == null || evidenceSource == null)
            return;

        if (string.Equals(data.Id, evidenceSource.Id, StringComparison.Ordinal))
            IsEvidenceCollected = true;
    }

    private void SynchronizeCollectionState()
    {
        if (evidenceSource == null || EvidenceManager.Instance == null)
            return;

        foreach (EvidenceData data in EvidenceManager.Instance.CollectedEvidence)
        {
            if (data != null && string.Equals(data.Id, evidenceSource.Id, StringComparison.Ordinal))
            {
                IsEvidenceCollected = true;
                return;
            }
        }

        IsEvidenceCollected = false;
    }

    private void StopManagerSubscriptionRoutine()
    {
        if (managerSubscriptionRoutine == null)
            return;

        StopCoroutine(managerSubscriptionRoutine);
        managerSubscriptionRoutine = null;
    }

    private void UnsubscribeFromEvidenceManager()
    {
        if (subscribedManager != null)
            subscribedManager.EvidenceRegistered -= HandleEvidenceRegistered;

        subscribedManager = null;
    }

    private void OnValidate()
    {
        requiredIlluminationTime = Mathf.Max(0f, requiredIlluminationTime);
    }
}
