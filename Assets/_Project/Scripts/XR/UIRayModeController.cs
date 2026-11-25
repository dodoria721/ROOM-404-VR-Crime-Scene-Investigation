using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Evaluate the final modal state once, before the hand applies its LateUpdate pose.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class UIRayModeController : MonoBehaviour
{
    [SerializeField] private HandPoseController rightHand;
    [SerializeField] private XRDirectInteractor rightDirectInteractor;
    [SerializeField] private XRRayInteractor rightUIRay;
    [SerializeField] private GameObject[] interactivePanels;

    private CanvasGroup[][][] panelGroups;
    private bool pointActive;

    private void Awake()
    {
        if (rightHand == null || rightDirectInteractor == null || rightUIRay == null ||
            interactivePanels == null || interactivePanels.Length == 0)
        {
            Debug.LogError("[UI Ray Mode] Configure the hand, interactors, and interactive panels.", this);
            enabled = false;
            return;
        }

        panelGroups = new CanvasGroup[interactivePanels.Length][][];
        for (int i = 0; i < interactivePanels.Length; i++)
        {
            var levels = new List<CanvasGroup[]>();
            var panel = interactivePanels[i];
            for (var parent = panel != null ? panel.transform : null; parent != null; parent = parent.parent)
            {
                var groups = parent.GetComponents<CanvasGroup>();
                if (groups.Length > 0)
                    levels.Add(groups);
            }
            panelGroups[i] = levels.ToArray();
        }

        SetRayActive(false);
    }

    private void LateUpdate()
    {
        bool point = !IsHoldingGrabObject() && HasInteractivePanel();
        if (pointActive != point)
        {
            pointActive = point;
            rightHand.SetUIPointMode(point);
        }

        // VR_Test uses a shared UI Press action reference, readable while the
        // Ray is inactive. Require release before registering a fresh pointer.
        SetRayActive(point && (rightUIRay.gameObject.activeSelf || !rightUIRay.uiPressInput.ReadIsPerformed()));
    }

    private void OnDisable()
    {
        pointActive = false;
        if (rightHand != null)
            rightHand.SetUIPointMode(false);
        if (rightUIRay != null && panelGroups != null)
            SetRayActive(false);
    }

    private void SetRayActive(bool active)
    {
        var rayObject = rightUIRay.gameObject;
        if (rayObject.activeSelf != active)
            rayObject.SetActive(active);
    }

    private bool IsHoldingGrabObject()
    {
        var selections = rightDirectInteractor.interactablesSelected;
        for (int i = 0; i < selections.Count; i++)
        {
            if (selections[i] is XRGrabInteractable)
                return true;
        }
        return false;
    }

    private bool HasInteractivePanel()
    {
        for (int i = 0; i < interactivePanels.Length; i++)
        {
            var panel = interactivePanels[i];
            if (panel != null && panel.activeInHierarchy && GroupsAllowInteraction(panelGroups[i]))
                return true;
        }
        return false;
    }

    private static bool GroupsAllowInteraction(CanvasGroup[][] levels)
    {
        foreach (var groups in levels)
        {
            bool ignoreParents = false;
            foreach (var group in groups)
            {
                if (group == null || !group.isActiveAndEnabled)
                    continue;
                if (!group.interactable || !group.blocksRaycasts)
                    return false;
                ignoreParents |= group.ignoreParentGroups;
            }
            if (ignoreParents)
                break;
        }
        return true;
    }
}
