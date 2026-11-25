using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

internal static class HandedGrabAttachSetupEditor
{
    private const string MenuPath = "ROOM 404/XR/Configure Handed Grab Attaches";

    [MenuItem(MenuPath, validate = true)]
    private static bool CanConfigureScene() =>
        !EditorApplication.isPlayingOrWillChangePlaymode &&
        PrefabStageUtility.GetCurrentPrefabStage() == null &&
        SceneManager.GetActiveScene().isLoaded;

    [MenuItem(MenuPath)]
    private static void ConfigureScene()
    {
        if (!CanConfigureScene())
            return;

        var scene = SceneManager.GetActiveScene();
        var targets = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<XRGrabInteractable>())
            .Where(grab => grab.enabled && (grab.GetComponent<EvidenceObject>() != null ||
                                             grab.GetComponent<UVLightController>() != null))
            .ToArray();
        if (targets.Length == 0)
        {
            EditorUtility.DisplayDialog("Handed Grab Attaches",
                "No active grabbable evidence or UV flashlight found in the current scene.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Handed Grab Attaches",
                $"Configure {targets.Length} grabbable objects in {scene.name}? Missing hand attaches " +
                "will copy the shared Primary Attach. Existing transforms are kept. Save the scene manually.",
                "Configure", "Cancel"))
            return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Handed Grab Attaches");
        int ready = 0;
        foreach (var grab in targets)
        {
            if (GetCompatibilityIssue(grab) is string issue)
            {
                Debug.LogWarning($"[Handed Grab] {grab.name}: {issue}", grab);
                continue;
            }

            Configure(grab);
            ready++;
        }

        Undo.CollapseUndoOperations(group);
        EditorUtility.DisplayDialog("Handed Grab Attaches",
            $"Ready: {ready}/{targets.Length}. Check skipped objects in the Console. " +
            "Review each hand in Object Grab Pose Editor and save the scene when ready.", "OK");
    }

    internal static string GetCompatibilityIssue(XRGrabInteractable grab)
    {
        if (grab == null || EditorUtility.IsPersistent(grab) || !grab.gameObject.scene.isLoaded ||
            EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetPrefabStage(grab.gameObject) != null)
            return "Select a scene Grab Interactable in Edit Mode.";
        if (grab.selectMode != InteractableSelectMode.Single || grab.useDynamicAttach ||
            grab.predictedVisualsTransform != null)
            return "Handed Attach requires Single selection, static Attach, and no predicted visuals.";
        if (!grab.addDefaultGrabTransformers || grab.startingSingleGrabTransformers.Count != 0 ||
            grab.startingMultipleGrabTransformers.Count != 0 ||
            grab.GetComponents<XRBaseGrabTransformer>().Any(item => item is not HandedGrabTransformer))
            return "Existing custom Grab Transformer configuration was preserved.";

        var transformer = grab.GetComponent<HandedGrabTransformer>();
        if (transformer != null && !transformer.enabled)
            return "Enable the existing Handed Grab Transformer to use its hand attaches.";
        var left = transformer != null && transformer.LeftAttach != null
            ? transformer.LeftAttach : grab.transform.Find("GrabAttach_Left");
        var right = transformer != null && transformer.RightAttach != null
            ? transformer.RightAttach : grab.transform.Find("GrabAttach_Right");
        if (!IsIndependent(left, grab) || !IsIndependent(right, grab) ||
            (left != null && right != null && (left == right || left.IsChildOf(right) || right.IsChildOf(left))))
            return "Hand Attach references must be independent children and distinct from the shared Attach.";

        return null;
    }

    private static bool IsIndependent(Transform attach, XRGrabInteractable grab)
    {
        if (attach == null)
            return true;

        return attach != grab.transform && attach.IsChildOf(grab.transform) &&
               (grab.attachTransform == null ||
                (attach != grab.attachTransform && !grab.attachTransform.IsChildOf(attach))) &&
               (grab.secondaryAttachTransform == null ||
                (attach != grab.secondaryAttachTransform && !grab.secondaryAttachTransform.IsChildOf(attach)));
    }

    internal static HandedGrabTransformer Configure(XRGrabInteractable grab)
    {
        var transformer = grab.GetComponent<HandedGrabTransformer>();
        if (transformer != null && transformer.LeftAttach != null && transformer.RightAttach != null)
            return transformer;

        if (transformer == null)
            transformer = Undo.AddComponent<HandedGrabTransformer>(grab.gameObject);
        var left = transformer.LeftAttach != null
            ? transformer.LeftAttach : GetOrCreateAttach(grab, "GrabAttach_Left");
        var right = transformer.RightAttach != null
            ? transformer.RightAttach : GetOrCreateAttach(grab, "GrabAttach_Right");
        var serialized = new SerializedObject(transformer);
        serialized.FindProperty("leftAttach").objectReferenceValue = left;
        serialized.FindProperty("rightAttach").objectReferenceValue = right;
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(transformer);
        EditorSceneManager.MarkSceneDirty(grab.gameObject.scene);
        return transformer;
    }

    private static Transform GetOrCreateAttach(XRGrabInteractable grab, string name)
    {
        Transform root = grab.transform;
        Transform existing = root.Find(name);
        if (existing != null)
            return existing;

        var child = new GameObject(name);
        if (child.scene != root.gameObject.scene)
            SceneManager.MoveGameObjectToScene(child, root.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        Undo.SetTransformParent(child.transform, root, "Parent " + name);
        Undo.RecordObject(child.transform, "Initialize " + name);
        var source = grab.attachTransform;
        if (source == null || source == root)
            child.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        else if (source.parent == root)
            child.transform.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
        else
            child.transform.SetPositionAndRotation(source.position, source.rotation);
        child.transform.localScale = Vector3.one;
        return child.transform;
    }
}
