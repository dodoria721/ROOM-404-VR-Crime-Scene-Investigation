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

    private void OnEnable()
    {
        BindEvidence();
        if (result != null)
            result.ResultDetermined += OnResultDetermined;
        if (startButton != null)
            startButton.onClick.AddListener(OpenDeduction);
        Refresh();
    }

    private void Start()
    {
        // Covers a page enabled during scene initialization before EvidenceManager.Awake.
        BindEvidence();
        Refresh();
    }

    private void OnDisable()
    {
        if (evidence != null)
            evidence.EvidenceRegistered -= OnEvidenceRegistered;
        evidence = null;
        if (result != null)
            result.ResultDetermined -= OnResultDetermined;
        if (startButton != null)
            startButton.onClick.RemoveListener(OpenDeduction);
    }

    private void BindEvidence()
    {
        var current = EvidenceManager.Instance;
        if (evidence == current)
            return;

        if (evidence != null)
            evidence.EvidenceRegistered -= OnEvidenceRegistered;
        evidence = current;
        if (evidence != null)
            evidence.EvidenceRegistered += OnEvidenceRegistered;
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
