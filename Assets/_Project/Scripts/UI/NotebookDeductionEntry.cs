using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NotebookDeductionEntry : MonoBehaviour
{
    [SerializeField] private DeductionStartUIController deductionStart;
    [SerializeField] private ResultController result;
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text progressText;

    private EvidenceManager evidence;
    private Coroutine evidenceSubscriptionRoutine;

    private void OnEnable()
    {
        if (!TryBindEvidence() && evidenceSubscriptionRoutine == null)
            evidenceSubscriptionRoutine = StartCoroutine(WaitForEvidenceManager());
        if (result != null)
            result.ResultDetermined += OnResultDetermined;
        if (startButton != null)
            startButton.onClick.AddListener(OpenDeduction);
        Refresh();
    }

    private void OnDisable()
    {
        if (evidenceSubscriptionRoutine != null)
        {
            StopCoroutine(evidenceSubscriptionRoutine);
            evidenceSubscriptionRoutine = null;
        }
        UnbindEvidence();
        if (result != null)
            result.ResultDetermined -= OnResultDetermined;
        if (startButton != null)
            startButton.onClick.RemoveListener(OpenDeduction);
    }

    private bool TryBindEvidence()
    {
        var current = EvidenceManager.Instance;
        if (current == null)
            return false;

        if (evidence == current)
            return true;

        UnbindEvidence();
        evidence = current;
        evidence.EvidenceRegistered += OnEvidenceRegistered;
        return true;
    }

    private void UnbindEvidence()
    {
        if (evidence is not null)
            evidence.EvidenceRegistered -= OnEvidenceRegistered;
        evidence = null;
    }

    private IEnumerator WaitForEvidenceManager()
    {
        do
            yield return null;
        while (!TryBindEvidence());

        evidenceSubscriptionRoutine = null;
        Refresh();
    }

    private void OnEvidenceRegistered(EvidenceData _) => Refresh();

    private void OnResultDetermined(SuspectId _, bool __) => Refresh();

    private void Refresh()
    {
        if (progressText != null)
            progressText.SetText("{0} / 4", evidence != null ? evidence.RequiredCount : 0);
        if (startButton != null)
            startButton.interactable = deductionStart != null && deductionStart.CanOpenDeduction;
    }

    private void OpenDeduction()
    {
        if (deductionStart != null && deductionStart.CanOpenDeduction)
            deductionStart.TryOpenDeductionPanel();
        if (isActiveAndEnabled)
            Refresh();
    }
}
