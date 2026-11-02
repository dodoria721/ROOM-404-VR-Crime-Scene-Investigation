
using System;
using System.Collections.Generic;
using UnityEngine;

public class EvidenceManager : MonoBehaviour
{
    public static EvidenceManager Instance { get; private set; }

    [SerializeField] private int requiredTargetCount = 4;

    private readonly HashSet<string> collectedIds = new();
    private readonly HashSet<string> requiredIds = new();

    public event Action<EvidenceObject> EvidenceCollected;

    public int CollectedCount => collectedIds.Count;
    public int RequiredCount => requiredIds.Count;
    public bool CanDeduce => RequiredCount >= requiredTargetCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("EvidenceManager가 중복 생성되었습니다.");
            enabled = false;
            return;
        }

        Instance = this;
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

        if (!collectedIds.Add(evidence.Id))
        {
            Debug.Log($"이미 수집한 증거: {evidence.Id}");
            return false;
        }

        if (evidence.IsRequired)
            requiredIds.Add(evidence.Id);

        Debug.Log($"[증거 수집] {evidence.Id} - {evidence.EvidenceName}");
        Debug.Log($"수집 현황: {CollectedCount}/6, 필수: {RequiredCount}/4");

        EvidenceCollected?.Invoke(evidence);

        if (CanDeduce)
            Debug.Log("필수 증거 확보 완료! 범인 선택 가능");

        return true;
    }
}
