using UnityEngine;

public class EvidenceSource : MonoBehaviour
{
    [Header("Evidence Information")]
    [SerializeField] private string evidenceId = "E02";
    [SerializeField] private string evidenceName = "고정형 증거";

    [TextArea(2, 5)]
    [SerializeField] private string description = "현장에서 발견된 증거.";

    public string Id => evidenceId;
    public string EvidenceName => evidenceName;
    public string Description => description;

    [ContextMenu("Debug / Collect Evidence")]
    public void Collect()
    {
        if (EvidenceManager.Instance == null)
        {
            Debug.LogWarning("EvidenceManager를 찾을 수 없습니다.", this);
            return;
        }

        var data = new EvidenceData(evidenceId, evidenceName, description);
        EvidenceManager.Instance.TryCollectData(data);
    }
}
