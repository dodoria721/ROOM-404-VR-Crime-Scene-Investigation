
using System;
using System.Collections.Generic;
using UnityEngine;

public class EvidenceManager : MonoBehaviour
{
    private static readonly HashSet<string> ValidEvidenceIds = new(StringComparer.Ordinal)
    {
        "E01", "E02", "E03", "E04", "E05", "E06"
    };

    private static readonly HashSet<string> RequiredEvidenceIds = new(StringComparer.Ordinal)
    {
        "E01", "E02", "E03", "E06"
    };

    public static EvidenceManager Instance { get; private set; }

    // 기존 Scene의 직렬화 데이터를 보존하기 위해 유지한다.
    // 실제 필수 증거 판정은 RequiredEvidenceIds를 기준으로 한다.
    [SerializeField] private int requiredTargetCount = 4;

    private readonly HashSet<string> collectedIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> requiredIds = new(StringComparer.Ordinal);
    private readonly List<EvidenceData> collectedEvidence = new();
    private bool deductionUnlocked;

    // 기존 Grab 증거 수집 이벤트. 호환성을 위해 형식과 이름을 유지한다.
    public event Action<EvidenceObject> EvidenceCollected;

    // Grab, 고정형, PC형을 포함한 모든 증거 수집 이벤트.
    public event Action<EvidenceData> EvidenceRegistered;
    public event Action DeductionUnlocked;

    public int CollectedCount => collectedIds.Count;
    public int RequiredCount => requiredIds.Count;
    public bool CanDeduce => HasAllRequiredEvidence();
    public IReadOnlyList<EvidenceData> CollectedEvidence => collectedEvidence;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("EvidenceManager가 중복 생성되었습니다.");
            enabled = false;
            return;
        }

        Instance = this;

        if (requiredTargetCount != RequiredEvidenceIds.Count)
        {
            Debug.LogWarning(
                $"필수 증거 목표 수({requiredTargetCount})가 명시된 필수 ID 수({RequiredEvidenceIds.Count})와 다릅니다. " +
                "필수 증거 판정은 E01, E02, E03, E06을 기준으로 합니다.",
                this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool TryCollect(EvidenceObject evidence)
    {
        if (evidence == null || string.IsNullOrWhiteSpace(evidence.Id))
        {
            Debug.LogWarning("유효하지 않은 증거물입니다.");
            return false;
        }

        bool isRequiredById = RequiredEvidenceIds.Contains(evidence.Id);
        if (evidence.IsRequired != isRequiredById)
        {
            Debug.LogWarning(
                $"증거 {evidence.Id}의 isRequired 설정({evidence.IsRequired})이 명시된 필수 ID 설정({isRequiredById})과 다릅니다. " +
                "필수 증거 판정은 ID 설정을 우선합니다.",
                evidence);
        }

        var data = new EvidenceData(
            evidence.Id,
            evidence.EvidenceName,
            evidence.Description);

        return TryCollectInternal(data, evidence);
    }

    public bool TryCollectData(EvidenceData data)
    {
        return TryCollectInternal(data, null);
    }

    private bool TryCollectInternal(EvidenceData data, EvidenceObject sourceObject)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.Id))
        {
            Debug.LogWarning("유효하지 않은 증거 데이터입니다.");
            return false;
        }

        if (!ValidEvidenceIds.Contains(data.Id))
        {
            Debug.LogWarning($"등록할 수 없는 증거 ID입니다: {data.Id}. E01~E06만 사용할 수 있습니다.");
            return false;
        }

        if (!collectedIds.Add(data.Id))
        {
            Debug.Log($"이미 수집한 증거: {data.Id}");
            return false;
        }

        // 호출자가 가진 데이터의 변경 가능성과 관계없이 수집 시점의 값을 보관한다.
        var storedData = new EvidenceData(data.Id, data.EvidenceName, data.Description);
        collectedEvidence.Add(storedData);

        if (RequiredEvidenceIds.Contains(storedData.Id))
            requiredIds.Add(storedData.Id);

        Debug.Log($"[증거 수집] {storedData.Id} - {storedData.EvidenceName}");
        Debug.Log(
            $"수집 현황: {CollectedCount}/{ValidEvidenceIds.Count}, " +
            $"필수: {RequiredCount}/{RequiredEvidenceIds.Count}");

        EvidenceRegistered?.Invoke(storedData);

        if (sourceObject != null)
            EvidenceCollected?.Invoke(sourceObject);

        TryUnlockDeduction();
        return true;
    }

    private bool HasAllRequiredEvidence()
    {
        foreach (string requiredId in RequiredEvidenceIds)
        {
            if (!collectedIds.Contains(requiredId))
                return false;
        }

        return true;
    }

    private void TryUnlockDeduction()
    {
        if (deductionUnlocked || !CanDeduce)
            return;

        // 이벤트 처리 중 재진입하더라도 다시 발생하지 않도록 먼저 상태를 변경한다.
        deductionUnlocked = true;
        Debug.Log("필수 증거 확보 완료! 범인 선택 가능");
        DeductionUnlocked?.Invoke();
    }
}
