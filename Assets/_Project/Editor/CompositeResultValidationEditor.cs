using System;
using UnityEditor;

public static class CompositeResultValidationEditor
{
    public static void RunBatchValidation()
    {
        CaseType[] caseTypes =
        {
            CaseType.Suicide,
            CaseType.Accident,
            CaseType.ThirdPartyInvolvement
        };

        SuspectId[] suspects =
        {
            SuspectId.KimMinSu,
            SuspectId.LeeSeoYeon,
            SuspectId.ParkJinHo
        };

        int checkedCount = 0;
        foreach (CaseType caseType in caseTypes)
        {
            foreach (SuspectId suspect in suspects)
            {
                bool expected = caseType == CaseType.ThirdPartyInvolvement &&
                                suspect == SuspectId.LeeSeoYeon;
                bool actual = ResultController.IsCorrectCombination(caseType, suspect);
                if (actual != expected)
                {
                    throw new InvalidOperationException(
                        $"Composite result mismatch: {caseType} + {suspect}, expected={expected}, actual={actual}");
                }

                checkedCount++;
            }
        }

        if (ResultController.IsCorrectCombination(CaseType.None, SuspectId.LeeSeoYeon))
            throw new InvalidOperationException("CaseType.None must never be a successful result.");

        UnityEngine.Debug.Log($"[ROOM 404] Composite result validation passed: {checkedCount}/9 combinations.");
        EditorApplication.Exit(0);
    }
}
