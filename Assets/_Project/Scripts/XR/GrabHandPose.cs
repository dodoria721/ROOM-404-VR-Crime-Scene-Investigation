using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class GrabHandPose : MonoBehaviour
{
    [SerializeField] private HandPoseData leftHandPose;
    [SerializeField] private HandPoseData rightHandPose;

    private XRGrabInteractable grabInteractable;
    private readonly Dictionary<IXRSelectInteractor, HandPoseController> activeHands = new();

    public HandPoseData LeftHandPose => leftHandPose;
    public HandPoseData RightHandPose => rightHandPose;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        grabInteractable.selectEntered.AddListener(HandleSelectEntered);
        grabInteractable.selectExited.AddListener(HandleSelectExited);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
            grabInteractable.selectExited.RemoveListener(HandleSelectExited);
        }

        foreach (HandPoseController controller in activeHands.Values)
        {
            if (controller != null)
                controller.ClearGrabPose(this);
        }
        activeHands.Clear();
    }

    private void HandleSelectEntered(SelectEnterEventArgs args)
    {
        HandPoseController controller = FindHandController(args.interactorObject.transform);
        if (controller == null)
            return;

        HandPoseData pose = controller.Side == HandSide.Left ? leftHandPose : rightHandPose;
        if (pose == null)
            return;

        activeHands[args.interactorObject] = controller;
        controller.SetGrabPose(this, pose);
    }

    private void HandleSelectExited(SelectExitEventArgs args)
    {
        if (!activeHands.TryGetValue(args.interactorObject, out HandPoseController controller))
            return;

        activeHands.Remove(args.interactorObject);
        if (controller != null)
            controller.ClearGrabPose(this);
    }

    private static HandPoseController FindHandController(Transform interactorTransform)
    {
        Transform current = interactorTransform;
        while (current != null)
        {
            HandPoseController controller = current.GetComponentInChildren<HandPoseController>(true);
            if (controller != null)
                return controller;
            current = current.parent;
        }

        return null;
    }
}
