using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class ObjectGrabPoseEditorWindow : EditorWindow
{
    private enum PreviewPose { Grab, FullGrip }

    private GameObject targetObject;
    private HandSide handSide = HandSide.Right;
    private XRGrabInteractable grabInteractable;
    private HandPoseController handController;
    private XRBaseInteractor interactor;
    private PreviewPose previewPose;

    private GameObject previewRoot;
    private GameObject previewClone;
    private Transform interactorAttach;
    private Vector3 stagedAttachLocalPosition;
    private Quaternion stagedAttachLocalRotation = Quaternion.identity;
    private bool previewActive;
    private Vector3 initialAttachLocalPosition;
    private Quaternion initialAttachLocalRotation;
    private bool editAttachTransform;
    private HandedGrabTransformer handedTransformer;
    private Transform editedAttach;
    private InteractorHandedness SelectedHandedness =>
        handSide == HandSide.Left ? InteractorHandedness.Left : InteractorHandedness.Right;
    private readonly Dictionary<Transform, Quaternion> handBackup = new();

    [MenuItem("ROOM 404/XR/Object Grab Pose Editor")]
    public static void OpenWindow()
    {
        GetWindow<ObjectGrabPoseEditorWindow>("Object Grab Pose");
    }

    private void OnEnable()
    {
        minSize = new Vector2(430f, 520f);
        SceneView.duringSceneGui += DuringSceneGUI;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += CleanupPreview;
        EditorApplication.quitting += CleanupPreview;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= CleanupPreview;
        EditorApplication.quitting -= CleanupPreview;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        CleanupPreview();
    }

    private void Update()
    {
        if (previewActive && (targetObject == null || grabInteractable == null || handController == null ||
                              previewClone == null || interactorAttach == null || editedAttach == null))
            CleanupPreview();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ROOM 404 Object Grab Pose Editor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The original object is never moved. A non-interactive temporary clone is aligned " +
            "using the selected XRI Interactor and its matching Left or Right Attach.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(previewActive))
        {
            EditorGUI.BeginChangeCheck();
            GameObject newTarget = (GameObject)EditorGUILayout.ObjectField(
                "Target Object", targetObject, typeof(GameObject), true);
            HandSide newSide = (HandSide)EditorGUILayout.EnumPopup("Hand", handSide);
            if (EditorGUI.EndChangeCheck())
            {
                targetObject = newTarget;
                handSide = newSide;
                ResolveSelection();
            }
        }

        DrawResolvedReferences();
        if (!previewActive && !DrawSetup())
            return;

        EditorGUI.BeginChangeCheck();
        previewPose = (PreviewPose)EditorGUILayout.EnumPopup("Common Hand Pose", previewPose);
        if (EditorGUI.EndChangeCheck() && previewActive)
            ApplyPreviewPose();

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!CanPreview()))
        {
            if (!previewActive && GUILayout.Button("Preview Object In Hand", GUILayout.Height(30f)))
                BeginPreview();
        }

        if (!previewActive)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Preview Editing", EditorStyles.boldLabel);
        editAttachTransform = EditorGUILayout.ToggleLeft(
            "Edit Attach Transform (Scene handles)", editAttachTransform);

        EditorGUILayout.HelpBox(
            "Saving changes only the selected hand's Attach. The hand pose is read-only.",
            MessageType.None);

        DrawAttachEditor();

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button($"Save {handSide} Attach", GUILayout.Height(30f)))
                SaveGrabSetup();
            if (GUILayout.Button("Cancel Preview", GUILayout.Height(30f)))
                CleanupPreview();
        }
    }

    private void DrawResolvedReferences()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("XR Grab Interactable", grabInteractable,
                typeof(XRGrabInteractable), true);
            EditorGUILayout.ObjectField("Hand Controller", handController,
                typeof(HandPoseController), true);
        }

        using (new EditorGUI.DisabledScope(previewActive))
        {
            XRBaseInteractor selectedInteractor = (XRBaseInteractor)EditorGUILayout.ObjectField(
                "Grab Interactor", interactor, typeof(XRBaseInteractor), true);
            if (selectedInteractor != interactor)
                interactor = selectedInteractor;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Left Attach", handedTransformer != null ? handedTransformer.LeftAttach : null,
                typeof(Transform), true);
            EditorGUILayout.ObjectField("Right Attach", handedTransformer != null ? handedTransformer.RightAttach : null,
                typeof(Transform), true);
            if (grabInteractable != null)
                EditorGUILayout.ObjectField("Shared Primary (fallback)", grabInteractable.attachTransform,
                    typeof(Transform), true);
        }
    }

    private bool DrawSetup()
    {
        if (grabInteractable == null)
            return false;

        var issue = HandedGrabAttachSetupEditor.GetCompatibilityIssue(grabInteractable);
        if (issue != null)
        {
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
            return false;
        }

        if (handedTransformer == null || handedTransformer.LeftAttach == null || handedTransformer.RightAttach == null)
        {
            EditorGUILayout.HelpBox("Create the missing hand attaches from the current shared Primary Attach.", MessageType.Info);
            if (GUILayout.Button("Configure Left and Right Attaches"))
                handedTransformer = HandedGrabAttachSetupEditor.Configure(grabInteractable);
            return false;
        }

        if (interactor == null || interactor.handedness != SelectedHandedness)
            EditorGUILayout.HelpBox($"Choose a Grab Interactor with Handedness set to {SelectedHandedness}.", MessageType.Warning);
        return true;
    }

    private void DrawAttachEditor()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"{handSide} Attach Staging", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        Vector3 localPosition = EditorGUILayout.Vector3Field(
            "Local Position", stagedAttachLocalPosition);
        Vector3 localEuler = EditorGUILayout.Vector3Field(
            "Local Rotation", stagedAttachLocalRotation.eulerAngles);
        if (EditorGUI.EndChangeCheck())
        {
            stagedAttachLocalPosition = localPosition;
            stagedAttachLocalRotation = Quaternion.Euler(localEuler);
            AlignPreviewCloneToInteractor();
            SceneView.RepaintAll();
        }
    }

    private void ResolveSelection()
    {
        grabInteractable = null;
        handController = null;
        interactor = null;
        handedTransformer = null;

        if (targetObject == null || !targetObject.scene.IsValid())
            return;

        grabInteractable = targetObject.GetComponent<XRGrabInteractable>() ??
                           targetObject.GetComponentInParent<XRGrabInteractable>();
        if (grabInteractable == null)
            return;
        targetObject = grabInteractable.gameObject;
        handedTransformer = grabInteractable.GetComponent<HandedGrabTransformer>();

        handController = FindSceneObjects<HandPoseController>()
            .FirstOrDefault(controller => controller.Side == handSide && controller.gameObject.scene == targetObject.scene);
        interactor = FindBestInteractor(handController, SelectedHandedness);
    }

    private bool CanPreview()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               targetObject != null && grabInteractable != null &&
               handController != null && handController.HasOpenPose &&
               interactor != null && interactor.handedness == SelectedHandedness &&
               handedTransformer != null && handedTransformer.GetHandAttach(interactor.handedness) != null;
    }

    private void BeginPreview()
    {
        if (!CanPreview())
            return;

        CleanupPreview();
        previewActive = true;
        editAttachTransform = false;
        handBackup.Clear();
        foreach (Transform joint in handController.GetControlledJoints())
            handBackup[joint] = joint.localRotation;

        editedAttach = handedTransformer.GetHandAttach(interactor.handedness);
        stagedAttachLocalPosition = targetObject.transform.InverseTransformPoint(editedAttach.position);
        stagedAttachLocalRotation = Quaternion.Inverse(targetObject.transform.rotation) * editedAttach.rotation;

        initialAttachLocalPosition = stagedAttachLocalPosition;
        initialAttachLocalRotation = stagedAttachLocalRotation;
        interactorAttach = interactor.GetAttachTransform(grabInteractable);
        if (interactorAttach == null)
        {
            CleanupPreview();
            return;
        }
        previewRoot = new GameObject("[Grab Pose Preview]");
        previewRoot.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
        previewRoot.SetActive(false);
        previewClone = Instantiate(targetObject, previewRoot.transform, true);
        previewClone.name = targetObject.name;
        SetPreviewFlagsAndDisableInteraction(previewClone);
        previewClone.SetActive(true);
        previewRoot.SetActive(true);

        ApplyPreviewPose();
        AlignPreviewCloneToInteractor();
        Repaint();
    }

    private void ApplyPreviewPose()
    {
        handController.PreviewInputPose(1f, previewPose == PreviewPose.FullGrip ? 1f : 0f);
        SceneView.RepaintAll();
    }

    private void AlignPreviewCloneToInteractor()
    {
        if (!previewActive || previewClone == null || interactorAttach == null)
            return;

        previewClone.transform.position = targetObject.transform.position;
        previewClone.transform.rotation = targetObject.transform.rotation;

        Vector3 attachWorldPosition = previewClone.transform.TransformPoint(
            stagedAttachLocalPosition);
        Quaternion attachWorldRotation = previewClone.transform.rotation *
                                         stagedAttachLocalRotation;
        if (grabInteractable.trackRotation)
        {
            Quaternion rotationDelta = interactorAttach.rotation * Quaternion.Inverse(attachWorldRotation);
            previewClone.transform.rotation = rotationDelta * previewClone.transform.rotation;
            attachWorldPosition = previewClone.transform.TransformPoint(stagedAttachLocalPosition);
        }

        if (grabInteractable.trackPosition)
            previewClone.transform.position += interactorAttach.position - attachWorldPosition;
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (!previewActive || !editAttachTransform || previewClone == null ||
            interactorAttach == null)
            return;

        EditorGUI.BeginChangeCheck();
        var preview = previewClone.transform;
        Vector3 position = grabInteractable.trackPosition
            ? Handles.PositionHandle(preview.position, preview.rotation) : preview.position;
        Quaternion rotation = grabInteractable.trackRotation
            ? Handles.RotationHandle(preview.rotation, preview.position) : preview.rotation;
        if (!EditorGUI.EndChangeCheck())
            return;

        previewClone.transform.SetPositionAndRotation(position, rotation);
        // The hand/interactor attach stays fixed. Moving the preview object derives the
        // interactable-local attach pose that would produce this exact runtime placement.
        if (grabInteractable.trackPosition)
            stagedAttachLocalPosition = preview.InverseTransformPoint(interactorAttach.position);
        if (grabInteractable.trackRotation)
            stagedAttachLocalRotation = Quaternion.Inverse(preview.rotation) * interactorAttach.rotation;
        Repaint();
    }

    private void SaveGrabSetup()
    {
        if (!previewActive || grabInteractable == null)
            return;

        var issue = HandedGrabAttachSetupEditor.GetCompatibilityIssue(grabInteractable);
        if (issue != null || handedTransformer == null || editedAttach == null ||
            editedAttach != handedTransformer.GetHandAttach(SelectedHandedness))
        {
            EditorUtility.DisplayDialog("Hand Attach", issue ?? "Attach references changed during preview.", "OK");
            CleanupPreview();
            return;
        }

        if (stagedAttachLocalPosition != initialAttachLocalPosition ||
            stagedAttachLocalRotation != initialAttachLocalRotation)
        {
            Undo.RecordObject(editedAttach, $"Save {handSide} Attach");
            editedAttach.SetPositionAndRotation(targetObject.transform.TransformPoint(stagedAttachLocalPosition),
                targetObject.transform.rotation * stagedAttachLocalRotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(editedAttach);
            EditorSceneManager.MarkSceneDirty(targetObject.scene);
        }

        CleanupPreview();
    }

    private void CleanupPreview()
    {
        foreach (KeyValuePair<Transform, Quaternion> pair in handBackup)
        {
            if (pair.Key != null)
                pair.Key.localRotation = pair.Value;
        }
        handBackup.Clear();

        if (previewRoot != null)
            DestroyImmediate(previewRoot);
        previewRoot = null;
        previewClone = null;
        interactorAttach = null;
        previewActive = false;
        editAttachTransform = false;
        editedAttach = null;
        SceneView.RepaintAll();
        Repaint();
    }

    private void OnSceneSaving(Scene scene, string path)
    {
        if (previewActive)
            CleanupPreview();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            CleanupPreview();
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        CleanupPreview();
        ResolveSelection();
    }

    private static XRBaseInteractor FindBestInteractor(HandPoseController controller, InteractorHandedness handedness)
    {
        if (controller == null)
            return null;
        Transform searchRoot = controller.transform;
        while (searchRoot != null)
        {
            var candidates = searchRoot.GetComponentsInChildren<XRBaseInteractor>(true)
                .Where(candidate => candidate.handedness == handedness && candidate.isActiveAndEnabled)
                .ToArray();
            var best = candidates.FirstOrDefault(candidate => candidate is XRDirectInteractor) ??
                       candidates.FirstOrDefault(candidate => candidate is not XRRayInteractor);
            if (best != null)
                return best;
            searchRoot = searchRoot.parent;
        }
        return null;
    }

    private static void SetPreviewFlagsAndDisableInteraction(GameObject root)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            transform.gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    private static T[] FindSceneObjects<T>() where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(item => item != null && item.gameObject.scene.IsValid() &&
                           !EditorUtility.IsPersistent(item))
            .ToArray();
    }
}
