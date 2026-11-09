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
    private const SuspectId CorrectSuspect = SuspectId.LeeSeoYeon;

    public event Action<SuspectId, bool> ResultDetermined;

    public bool HasResult { get; private set; }
    public SuspectId SelectedSuspect { get; private set; }
    public bool IsSuccess { get; private set; }

    // 범인 선택 진입점은 DeductionController로 제한해 추리 잠금 조건을 우회하지 않게 한다.
    internal bool TryDetermineResult(SuspectId suspect)
    {
        if (HasResult)
        {
            Debug.Log("[최종 추리] 결과가 이미 확정되어 추가 선택을 무시합니다.", this);
            return false;
        }

        if (!Enum.IsDefined(typeof(SuspectId), suspect))
        {
            Debug.LogWarning($"[최종 추리] 유효하지 않은 용의자 ID입니다: {suspect}", this);
            return false;
        }

        // 콜백에서 다시 선택을 시도해도 중복 판정되지 않도록 이벤트보다 상태를 먼저 확정한다.
        SelectedSuspect = suspect;
        IsSuccess = suspect == CorrectSuspect;
        HasResult = true;

        Debug.Log(
            $"[최종 추리] 선택: {GetDisplayName(suspect)}, 결과: {(IsSuccess ? "Success" : "Failure")}",
            this);

        ResultDetermined?.Invoke(SelectedSuspect, IsSuccess);
        return true;
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
