using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(HandPoseController))]
public sealed class HandPoseControllerEditor : Editor
{
    private enum PreviewMode
    {
        None,
        PoseAsset,
        Input,
        Joint
    }

    private sealed class ManualEditSession
    {
        public HandPoseController Controller;
        public HandPoseData Pose;
        public readonly Dictionary<Transform, Quaternion> Backup = new();
    }

    private static ManualEditSession manualEditSession;
    private readonly Dictionary<Transform, Quaternion> previewSnapshot = new();
    private bool previewActive;
    private PreviewMode previewMode;
    private float previewGrip;
    private float previewTrigger;
    private float previewPoseWeight;
    private HandPoseData[] availablePoses = System.Array.Empty<HandPoseData>();
    private string[] availablePoseNames = System.Array.Empty<string>();
    private int selectedPoseIndex = -1;
    private HandFinger selectedFinger = HandFinger.Index;
    private int selectedJointIndex;
    private float previewJointAngle;

    private void OnEnable()
    {
        RefreshPoseAssets();
        HandPoseController controller = target as HandPoseController;
        if (IsManualEditing(controller) && manualEditSession.Pose != null)
        {
            int index = System.Array.IndexOf(availablePoses, manualEditSession.Pose);
            if (index >= 0)
                selectedPoseIndex = index;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        HandPoseController controller = (HandPoseController)target;
        bool editing = IsManualEditing(controller);

        using (new EditorGUI.DisabledScope(editing))
        {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Pose Capture", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generate Default Poses derives a draft curl direction from this hand's actual bones. " +
            "Use Scene View and Capture Current Pose for model-specific fine tuning.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Open Pose"))
            {
                EndPreview();
                Undo.RecordObject(controller, "Capture Hand Open Pose");
                controller.CaptureOpenPose();
                MarkControllerDirty(controller);
            }

            if (GUILayout.Button("Capture Closed Pose"))
            {
                EndPreview();
                Undo.RecordObject(controller, "Capture Hand Closed Pose");
                controller.CaptureClosedPose();
                RecordJoints(controller, "Restore Hand Open Pose");
                controller.RestoreOpenPose();
                MarkControllerDirty(controller);
            }
        }

        if (GUILayout.Button("Restore Open Pose"))
        {
            EndPreview();
            RecordJoints(controller, "Restore Hand Open Pose");
            controller.RestoreOpenPose();
            MarkControllerDirty(controller);
        }

        if (GUILayout.Button("Generate / Regenerate Default Poses"))
        {
            EndPreview();
            HandPoseGeneratorEditor.GenerateDefaultPosesForController(controller, true);
            RefreshPoseAssets();
        }
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Pose Asset", EditorStyles.boldLabel);
        if (availablePoses.Length == 0)
        {
            EditorGUILayout.HelpBox("Generate poses first or create a HandPoseData asset.", MessageType.Info);
        }
        else
        {
            using (new EditorGUI.DisabledScope(editing))
            {
                int newIndex = EditorGUILayout.Popup(
                    "Select Pose", selectedPoseIndex, availablePoseNames);
                if (newIndex != selectedPoseIndex)
                {
                    EndPreview();
                    selectedPoseIndex = newIndex;
                }
            }

            HandPoseData selectedPose = GetSelectedPose();
            if (editing)
            {
                EditorGUILayout.HelpBox(
                    $"Manual editing: {manualEditSession.Pose.name}\n" +
                    "Select finger joints in the Hierarchy and edit localRotation in Scene View.",
                    MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Save Edited Pose"))
                        SaveManualEditing(controller);
                    if (GUILayout.Button("Cancel Editing"))
                        CancelManualEditing(controller);
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Edit Selected Pose") && selectedPose != null)
                        BeginManualEditing(controller, selectedPose);

                    if (GUILayout.Button("Capture Current Pose") && selectedPose != null)
                    {
                        // Capture before EndPreview restores the backed-up rotations.
                        Undo.RecordObject(selectedPose, "Capture Current Hand Pose");
                        selectedPose.CaptureCurrent(controller);
                        EditorUtility.SetDirty(selectedPose);
                        EndPreview();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Save Pose") && selectedPose != null)
                    {
                        EditorUtility.SetDirty(selectedPose);
                        AssetDatabase.SaveAssets();
                    }

                    using (new EditorGUI.DisabledScope(
                               selectedPose == null ||
                               selectedPose.Kind != HandPoseKind.FullGrip))
                    {
                        if (GUILayout.Button("Apply FullGrip To Input Poses"))
                            ApplyFullGripToInputPoses(controller, selectedPose);
                    }
                }

                EditorGUI.BeginChangeCheck();
                previewPoseWeight = EditorGUILayout.Slider(
                    "Preview Pose", previewPoseWeight, 0f, 1f);
                if (EditorGUI.EndChangeCheck() && selectedPose != null)
                {
                    BeginPreview(controller, PreviewMode.PoseAsset);
                    controller.ApplyDataPoseImmediate(selectedPose, previewPoseWeight);
                    SceneView.RepaintAll();
                }
            }
        }

        if (editing)
            return;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);
        if (!controller.HasClosedPose)
        {
            EditorGUILayout.HelpBox(
                "Grip/Trigger Closed Pose data is not connected. Run Generate / Regenerate " +
                "Default Poses, or capture a manual Closed Pose.",
                MessageType.Warning);
        }
        else if (controller.GeneratedGripPose != null && controller.GeneratedTriggerPose != null)
        {
            EditorGUILayout.HelpBox(
                $"Generated input poses connected: {controller.GeneratedGripPose.name} / " +
                controller.GeneratedTriggerPose.name,
                MessageType.Info);
        }

        EditorGUI.BeginChangeCheck();
        previewGrip = EditorGUILayout.Slider("Preview Grip", previewGrip, 0f, 1f);
        previewTrigger = EditorGUILayout.Slider("Preview Trigger", previewTrigger, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            BeginPreview(controller, PreviewMode.Input);
            controller.ApplyPoseImmediate(previewGrip, previewTrigger);
            SceneView.RepaintAll();
        }

        using (new EditorGUI.DisabledScope(!previewActive))
        {
            if (GUILayout.Button("Reset Preview / Restore Previous Pose"))
                EndPreview();
        }

        DrawSingleJointPreview(controller);
    }

    private void DrawSingleJointPreview(HandPoseController controller)
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Individual Joint Preview", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        HandFinger newFinger = (HandFinger)EditorGUILayout.EnumPopup(
            "Select Finger", selectedFinger);
        if (EditorGUI.EndChangeCheck())
        {
            EndPreview();
            selectedFinger = newFinger;
            selectedJointIndex = 0;
            previewJointAngle = 0f;
        }

        int jointCount = Mathf.Max(1, controller.GetFingerJointCount(selectedFinger));
        selectedJointIndex = Mathf.Clamp(selectedJointIndex, 0, jointCount - 1);
        string[] jointNames = GetJointNames(selectedFinger, jointCount);
        EditorGUI.BeginChangeCheck();
        int newJoint = EditorGUILayout.Popup(
            "Select Joint", selectedJointIndex, jointNames);
        if (EditorGUI.EndChangeCheck())
        {
            EndPreview();
            selectedJointIndex = newJoint;
            previewJointAngle = 0f;
        }

        HandPoseController.JointCurlCalibration calibration =
            controller.GetJointCurlCalibration(selectedFinger, selectedJointIndex);
        EditorGUILayout.LabelField("Axis Mode", calibration.AxisMode.ToString());
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector3Field("Resolved Local Curl Axis", calibration.LocalCurlAxis);

        EditorGUI.BeginChangeCheck();
        previewJointAngle = EditorGUILayout.Slider(
            "Preview Curl Angle", previewJointAngle, 0f, 120f);
        if (EditorGUI.EndChangeCheck())
        {
            BeginPreview(controller, PreviewMode.Joint);
            controller.RestoreOpenPose();
            controller.PreviewSingleJoint(
                selectedFinger, selectedJointIndex, previewJointAngle);
            SceneView.RepaintAll();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reset Joint"))
            {
                BeginPreview(controller, PreviewMode.Joint);
                controller.ResetSingleJoint(selectedFinger, selectedJointIndex);
                previewJointAngle = 0f;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Restore Open Pose"))
            {
                EndPreview();
                RecordJoints(controller, "Restore Hand Open Pose");
                controller.RestoreOpenPose();
                previewJointAngle = 0f;
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.HelpBox(
            "Cyan: selected joint curl axis. Yellow: Palm Normal Local. " +
            "Edit Auto/Manual Axis, angle override and joint inversion in Automatic Curl Calibration.",
            MessageType.None);
    }

    private void OnSceneGUI()
    {
        HandPoseController controller = (HandPoseController)target;
        if (!controller.TryGetFingerJoint(
                selectedFinger, selectedJointIndex, out Transform joint, out _))
            return;

        HandPoseController.JointCurlCalibration calibration =
            controller.GetJointCurlCalibration(selectedFinger, selectedJointIndex);
        Vector3 axisWorld = joint.localToWorldMatrix
            .MultiplyVector(calibration.LocalCurlAxis).normalized;
        float axisSize = HandleUtility.GetHandleSize(joint.position) * 0.22f;
        Handles.color = Color.cyan;
        Handles.ArrowHandleCap(
            0, joint.position, Quaternion.LookRotation(axisWorld), axisSize,
            EventType.Repaint);

        Vector3 palmOrigin = CalculatePalmGizmoOrigin(controller);
        Vector3 palmNormalWorld = controller.transform.localToWorldMatrix
            .MultiplyVector(controller.PalmNormalLocal).normalized;
        float palmSize = HandleUtility.GetHandleSize(palmOrigin) * 0.28f;
        Handles.color = Color.yellow;
        Handles.ArrowHandleCap(
            0, palmOrigin, Quaternion.LookRotation(palmNormalWorld), palmSize,
            EventType.Repaint);
    }

    private static Vector3 CalculatePalmGizmoOrigin(HandPoseController controller)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (HandFinger finger in new[]
                 {
                     HandFinger.Index, HandFinger.Middle, HandFinger.Ring, HandFinger.Little
                 })
        {
            if (controller.TryGetFingerJoint(finger, 0, out Transform joint, out _))
            {
                sum += joint.position;
                count++;
            }
        }
        return count > 0 ? sum / count : controller.transform.position;
    }

    private static string[] GetJointNames(HandFinger finger, int count)
    {
        string[] thumbNames = { "Metacarpal", "Proximal", "Intermediate", "Distal" };
        string[] fingerNames = { "Proximal", "Intermediate", "Distal" };
        string[] source = finger == HandFinger.Thumb ? thumbNames : fingerNames;
        return source.Take(count).ToArray();
    }

    private void OnDisable()
    {
        EndPreview();
    }

    private void BeginPreview(HandPoseController controller, PreviewMode mode)
    {
        if (IsManualEditing(controller))
            return;
        if (previewActive && previewMode == mode)
            return;
        if (previewActive)
            EndPreview(false);

        switch (mode)
        {
            case PreviewMode.PoseAsset:
                previewGrip = 0f;
                previewTrigger = 0f;
                previewJointAngle = 0f;
                break;
            case PreviewMode.Input:
                previewPoseWeight = 0f;
                previewJointAngle = 0f;
                break;
            case PreviewMode.Joint:
                previewPoseWeight = 0f;
                previewGrip = 0f;
                previewTrigger = 0f;
                break;
        }

        previewSnapshot.Clear();
        foreach (Transform joint in controller.GetControlledJoints())
            previewSnapshot[joint] = joint.localRotation;

        previewActive = true;
        previewMode = mode;
    }

    private void EndPreview(bool resetControls = true)
    {
        if (!previewActive)
            return;

        foreach (KeyValuePair<Transform, Quaternion> pair in previewSnapshot)
        {
            if (pair.Key != null)
                pair.Key.localRotation = pair.Value;
        }

        previewSnapshot.Clear();
        previewActive = false;
        previewMode = PreviewMode.None;
        if (resetControls)
        {
            previewGrip = 0f;
            previewTrigger = 0f;
            previewPoseWeight = 0f;
            previewJointAngle = 0f;
        }
        SceneView.RepaintAll();
    }

    private static bool IsManualEditing(HandPoseController controller)
    {
        return manualEditSession != null &&
               manualEditSession.Controller == controller &&
               manualEditSession.Pose != null;
    }

    private void BeginManualEditing(
        HandPoseController controller, HandPoseData pose)
    {
        if (controller == null || pose == null)
            return;
        if (manualEditSession != null && manualEditSession.Controller != controller)
        {
            EditorUtility.DisplayDialog(
                "Hand Pose Editing",
                "Another hand is already being edited. Save or cancel that edit first.",
                "OK");
            return;
        }

        EndPreview();
        ManualEditSession session = new()
        {
            Controller = controller,
            Pose = pose
        };
        foreach (Transform joint in controller.GetControlledJoints())
            session.Backup[joint] = joint.localRotation;
        manualEditSession = session;

        RecordJoints(controller, "Edit Selected Hand Pose");
        controller.ApplyDataPoseImmediate(pose, 1f);
        previewPoseWeight = 1f;
        SceneView.RepaintAll();
        Repaint();
    }

    private void SaveManualEditing(HandPoseController controller)
    {
        if (!IsManualEditing(controller))
            return;

        HandPoseData pose = manualEditSession.Pose;
        IReadOnlyList<HandPoseData.JointRotation> before =
            pose.JointRotations.ToArray();
        List<HandPoseData.JointRotation> current = CaptureCurrentRotations(controller);

        Undo.RecordObject(pose, "Save Edited Hand Pose");
        pose.ReplaceJointRotations(current, true);
        EditorUtility.SetDirty(pose);
        SaveAndValidatePoseAsset(controller, pose, before, current, true);

        // Do not restore the backup. Keeping the saved pose visible makes visual verification possible.
        manualEditSession = null;
        previewPoseWeight = 1f;
        RefreshPoseAssets();
        SceneView.RepaintAll();
        Repaint();
    }

    private void CancelManualEditing(HandPoseController controller)
    {
        if (!IsManualEditing(controller))
            return;

        Undo.RecordObjects(
            manualEditSession.Backup.Keys.Where(item => item != null).ToArray(),
            "Cancel Hand Pose Editing");
        foreach (KeyValuePair<Transform, Quaternion> pair in manualEditSession.Backup)
        {
            if (pair.Key != null)
                pair.Key.localRotation = pair.Value;
        }

        manualEditSession = null;
        previewPoseWeight = 0f;
        SceneView.RepaintAll();
        Repaint();
    }

    private void ApplyFullGripToInputPoses(
        HandPoseController controller, HandPoseData fullGrip)
    {
        if (controller == null || fullGrip == null || fullGrip.Kind != HandPoseKind.FullGrip)
            return;

        HandPoseData grip = controller.GeneratedGripPose ?? availablePoses.FirstOrDefault(
            pose => pose.Kind == HandPoseKind.Grip && pose.Side == controller.Side);
        HandPoseData trigger = controller.GeneratedTriggerPose ?? availablePoses.FirstOrDefault(
            pose => pose.Kind == HandPoseKind.Trigger && pose.Side == controller.Side);
        if (grip == null || trigger == null)
        {
            EditorUtility.DisplayDialog(
                "Apply FullGrip",
                "The matching Grip and Trigger pose assets could not be found.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply FullGrip To Input Poses",
            $"Apply {fullGrip.name} to these existing assets?\n\n" +
            $"Grip: {grip.name} (thumb, middle, ring, little)\n" +
            $"Trigger: {trigger.name} (index)",
            "Apply", "Cancel");
        if (!confirmed)
            return;

        IReadOnlyList<HandPoseData.JointRotation> gripBefore = grip.JointRotations.ToArray();
        IReadOnlyList<HandPoseData.JointRotation> triggerBefore =
            trigger.JointRotations.ToArray();
        List<HandPoseData.JointRotation> gripAfter = MergeFingerRotations(
            controller, grip, fullGrip,
            HandFinger.Thumb, HandFinger.Middle, HandFinger.Ring, HandFinger.Little);
        List<HandPoseData.JointRotation> triggerAfter = MergeFingerRotations(
            controller, trigger, fullGrip, HandFinger.Index);

        Undo.RecordObjects(new UnityEngine.Object[] { grip, trigger },
            "Apply FullGrip To Input Poses");
        grip.ReplaceJointRotations(gripAfter, true);
        trigger.ReplaceJointRotations(triggerAfter, true);
        EditorUtility.SetDirty(grip);
        EditorUtility.SetDirty(trigger);
        SaveAndValidatePoseAsset(controller, grip, gripBefore, gripAfter, false);
        SaveAndValidatePoseAsset(controller, trigger, triggerBefore, triggerAfter, false);

        Undo.RecordObject(controller, "Reconnect Edited Input Poses");
        controller.ApplyGeneratedInputPoses(grip, trigger);
        MarkControllerDirty(controller);
    }

    private static List<HandPoseData.JointRotation> MergeFingerRotations(
        HandPoseController controller,
        HandPoseData destination,
        HandPoseData source,
        params HandFinger[] fingers)
    {
        Dictionary<string, HandPoseData.JointRotation> merged = destination.JointRotations
            .ToDictionary(entry => entry.TransformPath, entry => entry);
        foreach (HandFinger finger in fingers)
        {
            foreach (Transform joint in controller.GetFingerJoints(finger))
            {
                if (!source.TryGetLocalRotation(
                        controller.transform, joint, out Quaternion rotation))
                    continue;
                string path = HandPoseData.GetRelativePath(controller.transform, joint);
                merged[path] = new HandPoseData.JointRotation(path, rotation);
            }
        }
        return merged.Values.OrderBy(entry => entry.TransformPath).ToList();
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

    private static void SaveAndValidatePoseAsset(
        HandPoseController controller,
        HandPoseData pose,
        IReadOnlyList<HandPoseData.JointRotation> before,
        IReadOnlyList<HandPoseData.JointRotation> expected,
        bool warnWhenOpen)
    {
        string path = AssetDatabase.GetAssetPath(pose);
        Dictionary<string, Quaternion> previous = before.ToDictionary(
            entry => entry.TransformPath, entry => entry.LocalRotation);
        int changed = 0;
        float largestChange = 0f;
        foreach (HandPoseData.JointRotation entry in expected)
        {
            if (!previous.TryGetValue(entry.TransformPath, out Quaternion oldRotation))
            {
                changed++;
                continue;
            }
            float delta = Quaternion.Angle(oldRotation, entry.LocalRotation);
            largestChange = Mathf.Max(largestChange, delta);
            if (delta > 0.01f)
                changed++;
        }

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(path))
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        HandPoseData reloaded = AssetDatabase.LoadAssetAtPath<HandPoseData>(path);
        int reloadMismatches = CountPoseMismatches(expected, reloaded);
        bool diskSaved = !string.IsNullOrEmpty(path) &&
                         File.Exists(Path.GetFullPath(path)) &&
                         reloaded != null && !EditorUtility.IsDirty(reloaded);

        Debug.Log(
            $"[Hand Pose] 저장: {path}, 변경 관절 {changed}/{expected.Count}, " +
            $"최대 회전 변화 {largestChange:F2}°, 디스크 저장={diskSaved}, " +
            $"재로드 불일치={reloadMismatches}");
        if (changed == 0)
            Debug.LogWarning($"[Hand Pose] 변경된 관절이 없습니다: {path}", pose);
        if (reloadMismatches > 0)
            Debug.LogError($"[Hand Pose] 저장 후 재로드 검증에 실패했습니다: {path}", pose);
        if (warnWhenOpen && IsPoseEqualToOpen(controller, expected))
            Debug.LogWarning($"[Hand Pose] 저장된 Pose가 Open Pose와 동일합니다: {path}", pose);
    }

    private static int CountPoseMismatches(
        IReadOnlyList<HandPoseData.JointRotation> expected, HandPoseData reloaded)
    {
        if (reloaded == null)
            return expected.Count;
        Dictionary<string, Quaternion> actual = reloaded.JointRotations.ToDictionary(
            entry => entry.TransformPath, entry => entry.LocalRotation);
        int mismatches = 0;
        foreach (HandPoseData.JointRotation entry in expected)
        {
            if (!actual.TryGetValue(entry.TransformPath, out Quaternion rotation) ||
                Quaternion.Angle(rotation, entry.LocalRotation) > 0.01f)
                mismatches++;
        }
        return mismatches;
    }

    private static bool IsPoseEqualToOpen(
        HandPoseController controller,
        IReadOnlyList<HandPoseData.JointRotation> rotations)
    {
        Dictionary<string, Quaternion> pose = rotations.ToDictionary(
            entry => entry.TransformPath, entry => entry.LocalRotation);
        foreach (HandFinger finger in Enum.GetValues(typeof(HandFinger)))
        {
            for (int i = 0; i < controller.GetFingerJointCount(finger); i++)
            {
                if (!controller.TryGetFingerJoint(
                        finger, i, out Transform joint, out Quaternion open))
                    continue;
                string path = HandPoseData.GetRelativePath(controller.transform, joint);
                if (!pose.TryGetValue(path, out Quaternion rotation) ||
                    Quaternion.Angle(rotation, open) > 0.01f)
                    return false;
            }
        }
        return true;
    }

    private void RefreshPoseAssets()
    {
        HandPoseController controller = (HandPoseController)target;
        availablePoses = AssetDatabase.FindAssets("t:HandPoseData")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<HandPoseData>)
            .Where(pose => pose != null && pose.Side == controller.Side)
            .OrderBy(pose => pose.Kind)
            .ThenBy(pose => pose.name)
            .ToArray();
        availablePoseNames = availablePoses
            .Select(pose => $"{pose.Kind} / {pose.name}")
            .ToArray();
        selectedPoseIndex = availablePoses.Length > 0
            ? Mathf.Clamp(selectedPoseIndex, 0, availablePoses.Length - 1)
            : -1;
    }

    private HandPoseData GetSelectedPose()
    {
        return selectedPoseIndex >= 0 && selectedPoseIndex < availablePoses.Length
            ? availablePoses[selectedPoseIndex]
            : null;
    }

    private static void RecordJoints(HandPoseController controller, string operation)
    {
        Transform[] joints = controller.GetControlledJoints();
        if (joints.Length > 0)
            Undo.RecordObjects(joints, operation);
    }

    private static void MarkControllerDirty(HandPoseController controller)
    {
        EditorUtility.SetDirty(controller);
        if (controller.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        SceneView.RepaintAll();
    }
}

public static class HandPoseSetupEditor
{
    private const string ScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string InputActionsPath =
        "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/XRI Default Input Actions.inputactions";

    [MenuItem("ROOM 404/XR/Configure Hand Pose Controllers")]
    public static void ConfigureHandPoseControllers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Hand Pose] Exit Play Mode before configuring hand poses.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 Hand Pose",
                "Open VR_Test.unity before running this command. The current Scene was not changed.",
                "OK");
            return;
        }

        InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (inputAsset == null)
        {
            Debug.LogError($"[Hand Pose] Input Action Asset was not found: {InputActionsPath}");
            return;
        }

        ConfigureHand(scene, "LeftHandVisual", "XRI Left Interaction", inputAsset);
        ConfigureHand(scene, "RightHandVisual", "XRI Right Interaction", inputAsset);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = FindInScene(scene, "RightHandVisual")?.gameObject;
        Debug.Log(
            "[Hand Pose] Left/right controllers and existing analog actions are connected. " +
            "Run Generate Hand Poses to create geometry-derived draft poses, then inspect them in Scene View.");
    }

    private static void ConfigureHand(
        Scene scene, string visualName, string actionMapName, InputActionAsset inputAsset)
    {
        Transform visual = FindInScene(scene, visualName);
        if (visual == null)
        {
            Debug.LogError($"[Hand Pose] Could not find {visualName} in VR_Test.");
            return;
        }

        HandPoseController controller = visual.GetComponent<HandPoseController>();
        if (controller == null)
            controller = Undo.AddComponent<HandPoseController>(visual.gameObject);

        SerializedObject serialized = new(controller);
        serialized.FindProperty("handSide").enumValueIndex =
            visualName.StartsWith("Left") ? (int)HandSide.Left : (int)HandSide.Right;
        AssignFinger(serialized, "thumb", visual,
            "thumb_trapez", "thumb_meta", "thumb_prox", "thumb_dist");
        AssignFinger(serialized, "index", visual,
            null, "index_prox", "index_midd", "index_dist");
        AssignFinger(serialized, "middle", visual,
            null, "midd_prox", "midd_midd", "midd_dist");
        AssignFinger(serialized, "ring", visual,
            null, "ring_prox", "ring_midd", "ring_dist");
        AssignFinger(serialized, "little", visual,
            null, "pinky_prox", "pinky_midd", "pinky_dist");

        SetReference(serialized, "gripInput",
            FindActionReference(inputAsset, $"{actionMapName}/Select Value"));
        SetReference(serialized, "triggerInput",
            FindActionReference(inputAsset, $"{actionMapName}/Activate Value"));
        serialized.ApplyModifiedProperties();

        Undo.RecordObject(controller, "Capture Initial Hand Open Pose");
        controller.CaptureOpenPose();
        EditorUtility.SetDirty(controller);
    }

    private static void AssignFinger(
        SerializedObject serialized, string propertyName, Transform root,
        string metacarpal, string proximal, string intermediate, string distal)
    {
        SetTransform(serialized, $"{propertyName}.metacarpal", FindDescendant(root, metacarpal));
        SetTransform(serialized, $"{propertyName}.proximal", FindDescendant(root, proximal));
        SetTransform(serialized, $"{propertyName}.intermediate", FindDescendant(root, intermediate));
        SetTransform(serialized, $"{propertyName}.distal", FindDescendant(root, distal));
    }

    private static InputActionReference FindActionReference(
        InputActionAsset asset, string actionPath)
    {
        InputAction action = asset.FindAction(actionPath, true);
        InputActionReference reference = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
            .OfType<InputActionReference>()
            .FirstOrDefault(candidate => candidate.action != null && candidate.action.id == action.id);

        if (reference == null)
            throw new MissingReferenceException($"InputActionReference not found: {actionPath}");
        return reference;
    }

    private static void SetReference(
        SerializedObject serialized, string propertyName, InputActionReference reference)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new MissingReferenceException($"Serialized property not found: {propertyName}");
        property.objectReferenceValue = reference;
    }

    private static void SetTransform(
        SerializedObject serialized, string propertyPath, Transform value)
    {
        SerializedProperty property = serialized.FindProperty(propertyPath);
        if (property == null)
            throw new MissingReferenceException($"Serialized property not found: {propertyPath}");
        property.objectReferenceValue = value;
    }

    private static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDescendant(root.transform, name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;
        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
