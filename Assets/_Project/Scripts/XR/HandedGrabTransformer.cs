using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class HandedGrabTransformer : XRGeneralGrabTransformer
{
    [SerializeField] private Transform leftAttach;
    [SerializeField] private Transform rightAttach;

    public Transform LeftAttach => leftAttach;
    public Transform RightAttach => rightAttach;

    // XRI's default transformer lookup finds this XRGeneralGrabTransformer subtype.
    protected override RegistrationMode registrationMode => RegistrationMode.None;

    public Transform GetHandAttach(InteractorHandedness handedness)
    {
        return handedness switch
        {
            InteractorHandedness.Left => leftAttach,
            InteractorHandedness.Right => rightAttach,
            _ => null
        };
    }

    public override void OnGrab(XRGrabInteractable grabInteractable)
    {
        if (grabInteractable.selectMode != InteractableSelectMode.Single ||
            grabInteractable.useDynamicAttach || grabInteractable.predictedVisualsTransform != null ||
            grabInteractable.interactorsSelecting.Count != 1)
        {
            base.OnGrab(grabInteractable);
            return;
        }

        Transform sharedAttach = grabInteractable.attachTransform;
        Transform handAttach = GetHandAttach(grabInteractable.interactorsSelecting[0].handedness);
        if (handAttach == null)
        {
            base.OnGrab(grabInteractable);
            return;
        }

        // OnGrab caches offsets before Process computes the first target pose.
        // Other XRI code continues to see the original Primary Attach.
        grabInteractable.attachTransform = handAttach;
        try
        {
            base.OnGrab(grabInteractable);
        }
        finally
        {
            grabInteractable.attachTransform = sharedAttach;
        }
    }
}
