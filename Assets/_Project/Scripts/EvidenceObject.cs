
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class EvidenceObject : MonoBehaviour
{
    [Header("Evidence Information")]
    [SerializeField] private string evidenceId = "E01";
    [SerializeField] private string evidenceName = "세정제 용기";

    [TextArea(2, 5)]
    [SerializeField] private string description =
        "사건 현장에서 발견된 세정제 용기.";

    [SerializeField] private bool isRequired = true;

    public string Id => evidenceId;
    public string EvidenceName => evidenceName;
    public string Description => description;
    public bool IsRequired => isRequired;

    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.activated.AddListener(OnActivated);
    }

    private void OnDisable()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.activated.RemoveListener(OnActivated);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        Debug.Log($"[증거 조사] {evidenceName}: {description}");
    }

    private void OnActivated(ActivateEventArgs args)
    {
        Collect();
    }

    [ContextMenu("Debug / Collect Evidence")]
    private void Collect()
    {
        if (EvidenceManager.Instance == null)
        {
            Debug.LogWarning("EvidenceManager를 찾을 수 없습니다.");
            return;
        }

        EvidenceManager.Instance.TryCollect(this);
    }
}
