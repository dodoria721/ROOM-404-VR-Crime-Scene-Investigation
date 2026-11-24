using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class ObjectGrabPoseEditorWindow : EditorWindow
{
    private GameObject targetObject;
    private HandSide handSide = HandSide.Right;
    private XRGrabInteractable grabInteractable;
    private GrabHandPose grabHandPose;
    private HandPoseController handController;
    private XRBaseInteractor interactor;
    private HandPoseData selectedPose;

    private GameObject previewClone;
    private Transform interactorAttach;
    private Vector3 stagedAttachLocalPosition;
    private Quaternion stagedAttachLocalRotation = Quaternion.identity;
    private bool previewActive;
    private bool editFingerPose;
    private bool editAttachTransform;
    private bool createAttachOnSave;
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
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= CleanupPreview;
        EditorApplication.quitting -= CleanupPreview;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        CleanupPreview();
    }

    private void Update()
    {
        if (previewActive && (targetObject == null || handController == null || previewClone == null))
            CleanupPreview();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ROOM 404 Object Grab Pose Editor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The original object is never moved. A non-interactive temporary clone is aligned " +
            "using the selected XRI Interactor Attach and the interactable Primary Attach.",
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
        editFingerPose = EditorGUILayout.ToggleLeft("Edit Finger Pose", editFingerPose);
        editAttachTransform = EditorGUILayout.ToggleLeft(
            "Edit Attach Transform (Scene handles)", editAttachTransform);

        if (editFingerPose)
        {
            EditorGUILayout.HelpBox(
                "Select finger joints under the hand visual in the Hierarchy and edit localRotation. " +
                "The preview remains active while selection changes.", MessageType.None);
            if (GUILayout.Button("Select Hand Visual"))
                Selection.activeGameObject = handController.gameObject;
        }

        DrawAttachEditor();

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Grab Setup", GUILayout.Height(30f)))
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
            EditorGUILayout.ObjectField("Grab Hand Pose", grabHandPose,
                typeof(GrabHandPose), true);
            EditorGUILayout.ObjectField("Hand Controller", handController,
                typeof(HandPoseController), true);
            EditorGUILayout.ObjectField("Connected Pose", selectedPose,
                typeof(HandPoseData), false);
        }

        using (new EditorGUI.DisabledScope(previewActive))
        {
            XRBaseInteractor selectedInteractor = (XRBaseInteractor)EditorGUILayout.ObjectField(
                "Grab Interactor", interactor, typeof(XRBaseInteractor), true);
            if (selectedInteractor != interactor)
                interactor = selectedInteractor;
        }

        if (grabInteractable != null)
        {
            EditorGUILayout.LabelField("Use Dynamic Attach",
                grabInteractable.useDynamicAttach.ToString());
            EditorGUILayout.ObjectField("Primary Attach", grabInteractable.attachTransform,
                typeof(Transform), true);
            EditorGUILayout.ObjectField("Secondary Attach (second selector)",
                grabInteractable.secondaryAttachTransform, typeof(Transform), true);
            if (grabInteractable.useDynamicAttach)
            {
                EditorGUILayout.HelpBox(
                    "Dynamic Attach depends on the runtime selection hit pose. This preview uses " +
                    "the configured Primary Attach and does not change the Dynamic Attach setting.",
                    MessageType.Warning);
            }
        }
    }

    private void DrawAttachEditor()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Primary Attach Staging", EditorStyles.boldLabel);

        if (grabInteractable.attachTransform == null && !createAttachOnSave)
        {
            EditorGUILayout.HelpBox(
                "Attach Transform is None. XRI currently uses the interactable Transform itself.",
                MessageType.Info);
            if (GUILayout.Button("Create GrabAttach On Save"))
            {
                createAttachOnSave = EditorUtility.DisplayDialog(
                    "Create GrabAttach",
                    "Create a GrabAttach child and assign it as the Primary Attach when saving?",
                    "Create On Save", "Cancel");
            }
        }
        else if (createAttachOnSave)
        {
            EditorGUILayout.HelpBox(
                "A GrabAttach child will be created only when Save Grab Setup is pressed.",
                MessageType.Info);
        }

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
        grabHandPose = null;
        handController = null;
        interactor = null;
        selectedPose = null;
        createAttachOnSave = false;

        if (targetObject == null || !targetObject.scene.IsValid())
            return;

        grabInteractable = targetObject.GetComponent<XRGrabInteractable>() ??
                           targetObject.GetComponentInParent<XRGrabInteractable>();
        if (grabInteractable == null)
            return;
        targetObject = grabInteractable.gameObject;
        grabHandPose = grabInteractable.GetComponent<GrabHandPose>();

        handController = FindSceneObjects<HandPoseController>()
            .FirstOrDefault(controller => controller.Side == handSide);
        if (grabHandPose != null)
        {
            selectedPose = handSide == HandSide.Left
                ? grabHandPose.LeftHandPose
                : grabHandPose.RightHandPose;
        }
        interactor = FindBestInteractor(handController);
    }

    private bool CanPreview()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               targetObject != null && grabInteractable != null &&
               grabHandPose != null && handController != null &&
               selectedPose != null && interactor != null;
    }

    private void BeginPreview()
    {
        if (!CanPreview())
            return;

        CleanupPreview();
        previewActive = true;
        editFingerPose = false;
        editAttachTransform = false;
        handBackup.Clear();
        foreach (Transform joint in handController.GetControlledJoints())
            handBackup[joint] = joint.localRotation;

        Transform configuredAttach = grabInteractable.attachTransform;
        if (configuredAttach != null)
        {
            stagedAttachLocalPosition = targetObject.transform.InverseTransformPoint(
                configuredAttach.position);
            stagedAttachLocalRotation = Quaternion.Inverse(targetObject.transform.rotation) *
                                        configuredAttach.rotation;
        }
        else
        {
            stagedAttachLocalPosition = Vector3.zero;
            stagedAttachLocalRotation = Quaternion.identity;
        }

        interactorAttach = interactor.attachTransform != null
            ? interactor.attachTransform
            : interactor.transform;
        previewClone = Instantiate(targetObject);
        previewClone.name = $"[Grab Pose Preview] {targetObject.name}";
        SetPreviewFlagsAndDisableInteraction(previewClone);
        previewClone.SetActive(true);

        handController.ApplyDataPoseImmediate(selectedPose, 1f);
        AlignPreviewCloneToInteractor();
        SceneView.RepaintAll();
        Repaint();
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
        Quaternion rotationDelta = interactorAttach.rotation *
                                   Quaternion.Inverse(attachWorldRotation);
        previewClone.transform.rotation = rotationDelta * previewClone.transform.rotation;
        attachWorldPosition = previewClone.transform.TransformPoint(stagedAttachLocalPosition);
        previewClone.transform.position += interactorAttach.position - attachWorldPosition;
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (!previewActive || !editAttachTransform || previewClone == null ||
            interactorAttach == null)
            return;

        EditorGUI.BeginChangeCheck();
        Vector3 position = Handles.PositionHandle(
            previewClone.transform.position, previewClone.transform.rotation);
        Quaternion rotation = Handles.RotationHandle(
            previewClone.transform.rotation, previewClone.transform.position);
        if (!EditorGUI.EndChangeCheck())
            return;

        previewClone.transform.SetPositionAndRotation(position, rotation);
        // The hand/interactor attach stays fixed. Moving the preview object derives the
        // interactable-local attach pose that would produce this exact runtime placement.
        stagedAttachLocalPosition = previewClone.transform.InverseTransformPoint(
            interactorAttach.position);
        stagedAttachLocalRotation = Quaternion.Inverse(previewClone.transform.rotation) *
                                    interactorAttach.rotation;
        Repaint();
    }

    private void SaveGrabSetup()
    {
        if (!previewActive || selectedPose == null)
            return;

        IReadOnlyList<HandPoseData.JointRotation> before =
            selectedPose.JointRotations.ToArray();
        List<HandPoseData.JointRotation> rotations = CaptureCurrentRotations(handController);
        Undo.RecordObject(selectedPose, "Save Object Grab Hand Pose");
        selectedPose.ReplaceJointRotations(rotations, true);
        EditorUtility.SetDirty(selectedPose);

        bool attachChanged = SaveAttachTransform();
        AssetDatabase.SaveAssets();
        string posePath = AssetDatabase.GetAssetPath(selectedPose);
        bool diskSaved = !string.IsNullOrEmpty(posePath) &&
                         File.Exists(Path.GetFullPath(posePath));
        LogPoseChanges(posePath, before, rotations, diskSaved);

        if (attachChanged && targetObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(targetObject.scene);
        CleanupPreview();
    }

    private bool SaveAttachTransform()
    {
        Transform attach = grabInteractable.attachTransform;
        if (attach == null && !createAttachOnSave)
            return false;

        if (attach == null)
        {
            GameObject attachObject = new("GrabAttach");
            Undo.RegisterCreatedObjectUndo(attachObject, "Create Grab Attach Transform");
            attach = attachObject.transform;
            attach.SetParent(targetObject.transform, false);
            Undo.RecordObject(grabInteractable, "Assign Grab Attach Transform");
            grabInteractable.attachTransform = attach;
            EditorUtility.SetDirty(grabInteractable);
        }
        else
        {
            Undo.RecordObject(attach, "Edit Grab Attach Transform");
        }

        attach.position = targetObject.transform.TransformPoint(stagedAttachLocalPosition);
        attach.rotation = targetObject.transform.rotation * stagedAttachLocalRotation;
        EditorUtility.SetDirty(attach);
        return true;
    }

    private void CleanupPreview()
    {
        foreach (KeyValuePair<Transform, Quaternion> pair in handBackup)
        {
            if (pair.Key != null)
                pair.Key.localRotation = pair.Value;
        }
        handBackup.Clear();

        if (previewClone != null)
            DestroyImmediate(previewClone);
        previewClone = null;
        interactorAttach = null;
        previewActive = false;
        editFingerPose = false;
        editAttachTransform = false;
        createAttachOnSave = false;
        SceneView.RepaintAll();
        Repaint();
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

    private static XRBaseInteractor FindBestInteractor(HandPoseController controller)
    {
        if (controller == null)
            return null;
        Transform searchRoot = controller.transform;
        while (searchRoot.parent != null &&
               searchRoot.name.IndexOf("XR Controller", StringComparison.OrdinalIgnoreCase) < 0)
            searchRoot = searchRoot.parent;

        XRBaseInteractor[] candidates = searchRoot.GetComponentsInChildren<XRBaseInteractor>(true);
        return candidates.FirstOrDefault(candidate => candidate is XRDirectInteractor) ??
               candidates.FirstOrDefault(candidate => candidate is not XRRayInteractor) ??
               candidates.FirstOrDefault();
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

    private static List<HandPoseData.JointRotation> CaptureCurrentRotations(
        HandPoseController controller)
    {
        List<HandPoseData.JointRotation> rotations = new();
        foreach (Transform joint in controller.GetControlledJoints())
        {
            rotations.Add(new HandPoseData.JointRotation(
                HandPoseData.GetRelativePath(controller.transform, joint),
                joint.localRotation));
        }
        return rotations;
    }

    private static void LogPoseChanges(
        string path,
        IReadOnlyList<HandPoseData.JointRotation> before,
        IReadOnlyList<HandPoseData.JointRotation> after,
        bool diskSaved)
    {
        Dictionary<string, Quaternion> oldValues = before.ToDictionary(
            entry => entry.TransformPath, entry => entry.LocalRotation);
        int changed = 0;
        float maximum = 0f;
        foreach (HandPoseData.JointRotation entry in after)
        {
            if (!oldValues.TryGetValue(entry.TransformPath, out Quaternion oldRotation))
            {
                changed++;
                continue;
            }
            float angle = Quaternion.Angle(oldRotation, entry.LocalRotation);
            maximum = Mathf.Max(maximum, angle);
            if (angle > 0.01f)
                changed++;
        }

        Debug.Log(
            $"[Object Grab Pose] 저장: {path}, 변경 관절 {changed}/{after.Count}, " +
            $"최대 회전 변화 {maximum:F2}°, 디스크 저장={diskSaved}");
        if (changed == 0)
            Debug.LogWarning($"[Object Grab Pose] 변경된 손가락 관절이 없습니다: {path}");
    }

    private static T[] FindSceneObjects<T>() where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(item => item != null && item.gameObject.scene.IsValid() &&
                           !EditorUtility.IsPersistent(item))
            .ToArray();
    }
}
