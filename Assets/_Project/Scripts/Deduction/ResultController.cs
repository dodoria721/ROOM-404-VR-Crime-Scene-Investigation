using System;
using UnityEngine;

public enum SuspectId
{
    KimMinSu,
    LeeSeoYeon,
    ParkJinHo
}

public sealed class ResultController : MonoBehaviour
{
    private const CaseType CorrectCaseType = CaseType.ThirdPartyInvolvement;
    private const SuspectId CorrectSuspect = SuspectId.LeeSeoYeon;

    public event Action<SuspectId, bool> ResultDetermined;

    public bool HasResult { get; private set; }
    public CaseType SelectedCaseType { get; private set; } = CaseType.None;
    public SuspectId SelectedSuspect { get; private set; }
    public bool IsSuccess { get; private set; }

    // 기존 호출자의 컴파일 호환성은 유지하되 사건 유형을 우회한 결과 확정은 차단한다.
    internal bool TryDetermineResult(SuspectId suspect)
    {
        Debug.LogWarning("[최종 추리] 사건 유형 없이 결과를 확정할 수 없습니다.", this);
        return false;
    }

    internal bool TryDetermineResult(CaseType caseType, SuspectId suspect)
    {
        if (HasResult)
        {
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

        SelectedCaseType = caseType;
        SelectedSuspect = suspect;
        IsSuccess = IsCorrectCombination(caseType, suspect);
        HasResult = true;

        Debug.Log(
            $"[최종 추리] 사건 유형: {caseType}, 용의자: {GetDisplayName(suspect)}, 결과: {(IsSuccess ? "Success" : "Failure")}",
            this);

        ResultDetermined?.Invoke(SelectedSuspect, IsSuccess);
        return true;
    }

    public static bool IsCorrectCombination(CaseType caseType, SuspectId suspect)
    {
        return caseType == CorrectCaseType && suspect == CorrectSuspect;
    }

    private static string GetDisplayName(SuspectId suspect)
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
