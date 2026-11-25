using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class UVInspectionInteractable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private XRSimpleInteractable simpleInteractable;

    [Header("Evidence")]
    [SerializeField] private UVRevealable uvRevealable;
    [SerializeField] private EvidenceSource evidenceSource;

    [Header("Shared Inspection UI")]
    [SerializeField] private EvidenceUIController evidenceUIController;

    private bool isShowingInspection;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        UnsubscribeFromSelectEvents();
        SubscribeToSelectEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromSelectEvents();
        HideInspection();
    }

    private void HandleSelectEntered(SelectEnterEventArgs args)
    {
        if (uvRevealable == null || !uvRevealable.IsDiscovered)
        {
            return;
        }

        if (evidenceSource == null || evidenceUIController == null)
        {
            Debug.LogError("[UV Inspection] EvidenceSource 또는 EvidenceUIController 참조가 없습니다.", this);
            return;
        }

        var data = new EvidenceData(
            evidenceSource.Id,
            evidenceSource.EvidenceName,
            evidenceSource.Description);

        isShowingInspection = true;
        evidenceUIController.ShowEvidenceData(this, data);
    }

    private void HandleSelectExited(SelectExitEventArgs args)
    {
        HideInspection();
    }

    private void HideInspection()
    {
        if (!isShowingInspection)
            return;

        isShowingInspection = false;

        if (evidenceUIController != null)
            evidenceUIController.HideEvidenceData(this);
    }

    private void ResolveReferences()
    {
        if (simpleInteractable == null)
            simpleInteractable = GetComponent<XRSimpleInteractable>();

        if (uvRevealable == null)
            uvRevealable = GetComponent<UVRevealable>();

        if (evidenceSource == null)
            evidenceSource = GetComponent<EvidenceSource>();

        if (evidenceUIController == null)
        {
            evidenceUIController = FindFirstObjectByType<EvidenceUIController>(
                FindObjectsInactive.Include);
        }
    }

    private void SubscribeToSelectEvents()
    {
        if (simpleInteractable == null)
        {
            Debug.LogError("[UV Inspection] XRSimpleInteractable을 찾을 수 없습니다.", this);
            return;
        }

        simpleInteractable.selectEntered.AddListener(HandleSelectEntered);
        simpleInteractable.selectExited.AddListener(HandleSelectExited);
    }

    private void UnsubscribeFromSelectEvents()
    {
        if (simpleInteractable == null)
            return;

        simpleInteractable.selectEntered.RemoveListener(HandleSelectEntered);
        simpleInteractable.selectExited.RemoveListener(HandleSelectExited);
    }
}
