
using TMPro;
using UnityEngine;

public class EvidenceUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject inspectionPanel;
    [SerializeField] private TMP_Text evidenceIdText;
    [SerializeField] private TMP_Text evidenceNameText;
    [SerializeField] private TMP_Text descriptionText;

    private Object currentInspectionSource;

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

        currentInspectionSource = null;
        SetPanelVisible(false);
    }

    private void ShowEvidence(EvidenceObject evidence)
    {
        if (evidence == null)
            return;

        var data = new EvidenceData(
            evidence.Id,
            evidence.EvidenceName,
            evidence.Description);

        ShowEvidenceData(evidence, data);
    }

    private void HideEvidence(EvidenceObject evidence)
    {
        HideEvidenceData(evidence);
    }

    public void ShowEvidenceData(Object source, EvidenceData data)
    {
        if (source == null || data == null || inspectionPanel == null)
            return;

        currentInspectionSource = source;

        if (evidenceIdText != null)
            evidenceIdText.text = data.Id;

        if (evidenceNameText != null)
            evidenceNameText.text = data.EvidenceName;

        if (descriptionText != null)
            descriptionText.text = data.Description;

        SetPanelVisible(true);
    }

    public void HideEvidenceData(Object source)
    {
        // 다른 증거가 현재 UI를 사용 중이면 이전 증거의 종료 이벤트로 닫지 않는다.
        if (source == null || currentInspectionSource != source)
            return;

        currentInspectionSource = null;
        SetPanelVisible(false);
    }

    private void SetPanelVisible(bool visible)
    {
        if (inspectionPanel != null && inspectionPanel.activeSelf != visible)
            inspectionPanel.SetActive(visible);
    }
}
