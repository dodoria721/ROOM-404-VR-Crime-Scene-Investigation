
using System;
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

    // UI가 구독하는 조사 시작 / 종료 이벤트
    public static event Action<EvidenceObject> InspectionStarted;
    public static event Action<EvidenceObject> InspectionEnded;

    private XRGrabInteractable grabInteractable;
    private bool isInspecting;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
        grabInteractable.activated.AddListener(OnActivated);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
            grabInteractable.activated.RemoveListener(OnActivated);
        }

        if (isInspecting)
        {
            isInspecting = false;
            InspectionEnded?.Invoke(this);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (isInspecting)
            return;

        isInspecting = true;

        Debug.Log($"[증거 조사 시작] {evidenceId} - {evidenceName}");

        InspectionStarted?.Invoke(this);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (!isInspecting)
            return;

        isInspecting = false;

        Debug.Log($"[증거 조사 종료] {evidenceId}");

        InspectionEnded?.Invoke(this);
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
