
using TMPro;
using UnityEngine;

public class EvidenceUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject inspectionPanel;
    [SerializeField] private TMP_Text evidenceIdText;
    [SerializeField] private TMP_Text evidenceNameText;
    [SerializeField] private TMP_Text descriptionText;

    private EvidenceObject currentEvidence;

    private void Awake()
    {
        // 게임 시작 시 조사 패널 숨기기
        if (inspectionPanel != null)
            inspectionPanel.SetActive(false);
    }

    private void OnEnable()
    {
        EvidenceObject.InspectionStarted += ShowEvidence;
        EvidenceObject.InspectionEnded += HideEvidence;
    }

    private void OnDisable()
    {
        EvidenceObject.InspectionStarted -= ShowEvidence;
        EvidenceObject.InspectionEnded -= HideEvidence;
    }

    private void ShowEvidence(EvidenceObject evidence)
    {
        if (evidence == null || inspectionPanel == null)
            return;

        currentEvidence = evidence;

        if (evidenceIdText != null)
            evidenceIdText.text = evidence.Id;

        if (evidenceNameText != null)
            evidenceNameText.text = evidence.EvidenceName;

        if (descriptionText != null)
            descriptionText.text = evidence.Description;

        inspectionPanel.SetActive(true);
    }

    private void HideEvidence(EvidenceObject evidence)
    {
        // 다른 증거물을 조사 중이면 닫지 않음
        if (currentEvidence != evidence)
            return;

        currentEvidence = null;

        if (inspectionPanel != null)
            inspectionPanel.SetActive(false);
    }
}
