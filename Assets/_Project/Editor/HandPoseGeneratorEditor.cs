using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HandPoseGeneratorEditor
{
    private const string PoseRoot = "Assets/_Project/HandPoses/Generated";
    private const string DefaultPoseRoot = PoseRoot + "/Defaults";

    private sealed class PoseProfile
    {
        public readonly Dictionary<HandFinger, float[]> Angles = new();

        public PoseProfile Set(HandFinger finger, params float[] angles)
        {
            Angles[finger] = angles;
            return this;
        }
    }

    private readonly struct PalmFrame
    {
        public readonly Vector3 Center;
        public readonly Vector3 Normal;
        public readonly Vector3 IndexBase;

        public PalmFrame(Vector3 center, Vector3 normal, Vector3 indexBase)
        {
            Center = center;
            Normal = normal;
            IndexBase = indexBase;
        }
    }

    [MenuItem("ROOM 404/XR/Generate Hand Poses")]
    public static void GenerateDefaultPoses()
    {
        if (!CanEditScene())
            return;

        HandPoseController[] controllers = FindSceneObjects<HandPoseController>();
        if (controllers.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 Hand Pose",
                "HandPoseController가 없습니다. 먼저 Configure Hand Pose Controllers를 실행하세요.",
                "확인");
            return;
        }

        foreach (HandPoseController controller in controllers)
            GenerateDefaultPosesForController(controller, true);

        AssetDatabase.SaveAssets();
        Debug.Log("[Hand Pose] 좌우 손의 기본 자세 초안 생성을 완료했습니다. Scene View에서 관절 교차와 엄지 방향을 확인하세요.");
    }

    public static void GenerateDefaultPosesForController(
        HandPoseController controller, bool askBeforeOverwrite)
    {
        if (controller == null)
            return;

        EnsureFolder(DefaultPoseRoot);
        controller.RestoreOpenPose();

        PoseProfile open = new();
        PoseProfile idle = new PoseProfile()
            .Set(HandFinger.Thumb, 4f, 6f, 4f, 2f)
            .Set(HandFinger.Index, 3f, 5f, 4f)
            .Set(HandFinger.Middle, 5f, 8f, 6f)
            .Set(HandFinger.Ring, 7f, 10f, 8f)
            .Set(HandFinger.Little, 9f, 13f, 10f);
        PoseProfile grip = new PoseProfile()
            .Set(HandFinger.Thumb, 12f, 25f, 30f, 18f)
            .Set(HandFinger.Middle, 18f, 50f, 58f)
            .Set(HandFinger.Ring, 20f, 54f, 62f)
            .Set(HandFinger.Little, 22f, 58f, 66f);
        PoseProfile trigger = new PoseProfile()
            .Set(HandFinger.Index, 10f, 42f, 48f);
        PoseProfile point = new PoseProfile()
            .Set(HandFinger.Thumb, 10f, 22f, 26f, 14f)
            .Set(HandFinger.Index, 0f, 0f, 0f)
            .Set(HandFinger.Middle, 20f, 55f, 64f)
            .Set(HandFinger.Ring, 22f, 59f, 68f)
            .Set(HandFinger.Little, 24f, 63f, 72f);
        PoseProfile fullGrip = new PoseProfile()
            // Must match the runtime result of Grip=1 and Trigger=1 exactly.
            .Set(HandFinger.Thumb, 12f, 25f, 30f, 18f)
            .Set(HandFinger.Index, 10f, 42f, 48f)
            .Set(HandFinger.Middle, 18f, 50f, 58f)
            .Set(HandFinger.Ring, 20f, 54f, 62f)
            .Set(HandFinger.Little, 22f, 58f, 66f);

        string side = controller.Side.ToString();
        // Open is the calibration source. Never regenerate an existing Open asset.
        CreateOrUpdatePose(controller, open, HandPoseKind.Open, $"{side}_Open",
            "현재 Open Pose를 그대로 캡처한 기준 자세.",
            false);
        CreateOrUpdatePose(controller, idle, HandPoseKind.Idle, $"{side}_Idle",
            "Open 기준의 가벼운 자연 굽힘 초안.",
            askBeforeOverwrite);
        HandPoseData gripPose = CreateOrUpdatePose(controller, grip, HandPoseKind.Grip,
            $"{side}_Grip", "Grip 입력용: 중지·약지·소지·엄지 굽힘 초안.",
            askBeforeOverwrite);
        HandPoseData triggerPose = CreateOrUpdatePose(controller, trigger, HandPoseKind.Trigger,
            $"{side}_Trigger", "Trigger 입력용: 검지 굽힘 초안.",
            askBeforeOverwrite);
        CreateOrUpdatePose(controller, point, HandPoseKind.Point, $"{side}_Point",
            "검지는 펴고 나머지 손가락을 굽힌 포인팅 초안.",
            askBeforeOverwrite);
        HandPoseData fullGripPose = CreateOrUpdatePose(
            controller, fullGrip, HandPoseKind.FullGrip, $"{side}_FullGrip",
            "모든 손가락을 손바닥 방향으로 굽힌 주먹 초안.",
            askBeforeOverwrite);

        if (gripPose != null && triggerPose != null)
        {
            Undo.RecordObject(controller, "Apply Generated Input Hand Poses");
            controller.ApplyGeneratedInputPoses(gripPose, triggerPose);
            SerializedObject serialized = new(controller);
            serialized.FindProperty("generatedGripPose").objectReferenceValue = gripPose;
            serialized.FindProperty("generatedTriggerPose").objectReferenceValue = triggerPose;
            serialized.FindProperty("closedPoseCaptured").boolValue = true;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            if (controller.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        ValidateFullGripMatchesRuntime(
            controller, gripPose, triggerPose, fullGripPose);

        controller.RestoreOpenPose();
    }

    private static void ValidateFullGripMatchesRuntime(
        HandPoseController controller,
        HandPoseData gripPose,
        HandPoseData triggerPose,
        HandPoseData fullGripPose)
    {
        if (gripPose == null || triggerPose == null || fullGripPose == null)
            return;

        int mismatches = 0;
        float largestDifference = 0f;
        foreach (HandFinger finger in Enum.GetValues(typeof(HandFinger)))
        {
            HandPoseData runtimeSource = finger == HandFinger.Index
                ? triggerPose
                : gripPose;
            foreach (Transform joint in controller.GetFingerJoints(finger))
            {
                bool hasRuntime = runtimeSource.TryGetLocalRotation(
                    controller.transform, joint, out Quaternion runtimeRotation);
                bool hasFullGrip = fullGripPose.TryGetLocalRotation(
                    controller.transform, joint, out Quaternion fullRotation);
                if (!hasRuntime || !hasFullGrip)
                {
                    mismatches++;
                    continue;
                }

                float difference = Quaternion.Angle(runtimeRotation, fullRotation);
                largestDifference = Mathf.Max(largestDifference, difference);
                if (difference > 0.01f)
                    mismatches++;
            }
        }

        if (mismatches == 0)
        {
            Debug.Log(
                $"[Hand Pose] {controller.Side} FullGrip 검증 완료: " +
                "Grip=1 + Trigger=1 런타임 회전과 일치합니다.");
        }
        else
        {
            Debug.LogWarning(
                $"[Hand Pose] {controller.Side} FullGrip과 런타임 조합 사이에 " +
                $"{mismatches}개 불일치가 있습니다. 최대 {largestDifference:F2}°.",
                controller);
        }
    }

    private static HandPoseData CreateOrUpdatePose(
        HandPoseController controller,
        PoseProfile profile,
        HandPoseKind kind,
        string assetName,
        string notes,
        bool askBeforeOverwrite)
    {
        string path = $"{DefaultPoseRoot}/{SanitizeFileName(assetName)}.asset";
        HandPoseData pose = AssetDatabase.LoadAssetAtPath<HandPoseData>(path);
        bool existed = pose != null;
        Dictionary<string, Quaternion> previousRotations = pose != null
            ? pose.JointRotations.ToDictionary(
                entry => entry.TransformPath, entry => entry.LocalRotation)
            : new Dictionary<string, Quaternion>();
        if (pose != null)
        {
            if (!pose.IsGeneratedDraft)
            {
                Debug.Log($"[Hand Pose] 사용자 수정 Pose를 보존했습니다: {path}");
                return pose;
            }

            if (!askBeforeOverwrite)
            {
                Debug.Log($"[Hand Pose] 기존 생성 Pose를 보존했습니다: {path}");
                return pose;
            }

            bool overwrite = EditorUtility.DisplayDialog(
                "기존 Hand Pose",
                $"{assetName} 에셋이 이미 있습니다. 자동 생성 초안으로 덮어쓸까요?\n\n{path}",
                "덮어쓰기", "보존");
            if (!overwrite)
                return pose;
            Undo.RecordObject(pose, "Regenerate Hand Pose");
        }
        else
        {
            pose = ScriptableObject.CreateInstance<HandPoseData>();
            AssetDatabase.CreateAsset(pose, path);
        }

        List<HandPoseData.JointRotation> rotations = BuildRotations(controller, profile);
        pose.ReplaceData(
            controller.Side, kind, assetName,
            notes + " 자동 생성 결과는 모델별 미세 조정이 필요한 초안입니다.",
            Vector3.zero, true, rotations);
        EditorUtility.SetDirty(pose);
        ReportPoseChanges(path, existed, previousRotations, rotations);
        return pose;
    }

    private static void ReportPoseChanges(
        string path,
        bool existed,
        IReadOnlyDictionary<string, Quaternion> previous,
        IReadOnlyList<HandPoseData.JointRotation> current)
    {
        if (!existed)
        {
            Debug.Log($"[Hand Pose] Pose 생성: {path} ({current.Count} joints)");
            return;
        }

        int changed = 0;
        float largestAngle = 0f;
        foreach (HandPoseData.JointRotation entry in current)
        {
            if (!previous.TryGetValue(entry.TransformPath, out Quaternion oldRotation))
            {
                changed++;
                continue;
            }

            float delta = Quaternion.Angle(oldRotation, entry.LocalRotation);
            if (delta > 0.01f)
                changed++;
            largestAngle = Mathf.Max(largestAngle, delta);
        }

        Debug.Log(
            $"[Hand Pose] Pose 갱신: {path}, 변경 관절 {changed}/{current.Count}, " +
            $"최대 회전 변화 {largestAngle:F2}°");
    }

    private static List<HandPoseData.JointRotation> BuildRotations(
        HandPoseController controller, PoseProfile profile)
    {
        List<HandPoseData.JointRotation> result = new();
        PalmFrame palm = CalculatePalmFrame(controller);
        Undo.RecordObject(controller, "Update Generated Joint Curl Axes");

        foreach (HandFinger finger in Enum.GetValues(typeof(HandFinger)))
        {
            Transform[] joints = controller.GetFingerJoints(finger);
            profile.Angles.TryGetValue(finger, out float[] angles);
            Vector3 autoAxisRoot = CalculateFingerFlexionAxis(
                controller, finger, joints, palm);
            for (int i = 0; i < joints.Length; i++)
            {
                Transform joint = joints[i];
                if (joint == null)
                    continue;

                float generatedAngle = angles != null && angles.Length > 0
                    ? angles[Mathf.Min(i, angles.Length - 1)]
                    : 0f;
                Quaternion rotation = CalculatePlanarLocalRotation(
                    controller, joint, palm, autoAxisRoot, generatedAngle, finger, i);
                result.Add(new HandPoseData.JointRotation(
                    HandPoseData.GetRelativePath(controller.transform, joint), rotation));
            }
        }

        EditorUtility.SetDirty(controller);
        return result;
    }

    private static Vector3 CalculateFingerFlexionAxis(
        HandPoseController controller,
        HandFinger finger,
        Transform[] joints,
        PalmFrame palm)
    {
        if (joints == null || joints.Length == 0)
            return Vector3.right;

        Transform root = controller.transform;
        Transform first = joints[0];
        Transform tip = FindDeepestBoneChild(joints[joints.Length - 1]);
        if (tip == null)
            tip = FindDeepestBoneChild(first);
        if (first == null || tip == null)
            return Vector3.right;

        Vector3 firstPosition = root.InverseTransformPoint(first.position);
        Vector3 tipPosition = root.InverseTransformPoint(tip.position);
        Vector3 fingerForward = (tipPosition - firstPosition).normalized;
        if (fingerForward.sqrMagnitude < 0.0001f)
            return Vector3.right;

        Vector3 axis;
        if (finger == HandFinger.Thumb)
        {
            // Thumb opposition is a separate plane: its open direction and the index-side palm target.
            Vector3 thumbTarget = Vector3.Lerp(palm.Center, palm.IndexBase, 0.4f);
            Vector3 towardPalm = Vector3.ProjectOnPlane(
                thumbTarget - tipPosition, fingerForward);
            axis = Vector3.Cross(fingerForward, towardPalm.normalized);
        }
        else
        {
            // Long fingers curl in four parallel, independent planes. No shared center target is used.
            Vector3 palmDirection = Vector3.ProjectOnPlane(palm.Normal, fingerForward);
            axis = Vector3.Cross(fingerForward, palmDirection.normalized);
        }

        return axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;
    }

    private static Quaternion CalculatePlanarLocalRotation(
        HandPoseController controller,
        Transform joint,
        PalmFrame palm,
        Vector3 autoAxisRoot,
        float generatedAngle,
        HandFinger finger,
        int jointIndex)
    {
        Quaternion openLocal = joint.localRotation;
        if (Mathf.Abs(generatedAngle) < 0.001f || joint.parent == null)
            return openLocal;

        Transform handRoot = controller.transform;
        float naturalSign = DetectPlanarCurlSign(
            handRoot, joint, autoAxisRoot, palm, finger);
        if (Mathf.Approximately(naturalSign, 0f))
        {
            Debug.LogWarning(
                $"[Hand Pose] 굽힘 평면을 확정할 수 없어 Open 회전을 유지합니다: " +
                $"{controller.Side}/{finger}/{joint.name}", controller);
            return openLocal;
        }

        Vector3 naturalAxisRoot = autoAxisRoot * naturalSign;
        Quaternion jointRootRotation = GetRotationRelativeToRoot(handRoot, joint);
        Vector3 calculatedLocalAxis =
            (Quaternion.Inverse(jointRootRotation) * naturalAxisRoot).normalized;

        HandPoseController.JointCurlCalibration calibration =
            controller.GetJointCurlCalibration(finger, jointIndex);
        controller.SetGeneratedLocalCurlAxis(finger, jointIndex, calculatedLocalAxis);
        Vector3 localAxis = calibration.LocalCurlAxis;
        float angle = calibration.ResolveAngle(
            generatedAngle, controller.GetGeneratedAngleMultiplier(finger, jointIndex));
        float direction = controller.GetGeneratedCurlSign(finger, 1f);
        if (calibration.InvertDirection)
            direction = -direction;

        // Post-multiply: rotate around the joint's Open-pose local axis only. This avoids
        // introducing swing/twist from parent-space or world-space quaternion composition.
        return openLocal * Quaternion.AngleAxis(angle * direction, localAxis);
    }

    private static float DetectPlanarCurlSign(
        Transform handRoot,
        Transform joint,
        Vector3 axisRoot,
        PalmFrame palm,
        HandFinger finger)
    {
        Transform tip = FindDeepestBoneChild(joint);
        if (tip == null || axisRoot.sqrMagnitude < 0.0001f)
            return 0f;
        Vector3 jointPosition = handRoot.InverseTransformPoint(joint.position);
        Vector3 tipPosition = handRoot.InverseTransformPoint(tip.position);
        Vector3 tipVector = tipPosition - jointPosition;
        if (tipVector.sqrMagnitude < 0.0000001f)
            return 0f;

        const float probeAngle = 6f;
        Vector3 plus = jointPosition + Quaternion.AngleAxis(probeAngle, axisRoot) * tipVector;
        Vector3 minus = jointPosition + Quaternion.AngleAxis(-probeAngle, axisRoot) * tipVector;
        if (finger == HandFinger.Thumb)
        {
            Vector3 target = Vector3.Lerp(palm.Center, palm.IndexBase, 0.4f);
            float plusDistance = Vector3.Distance(plus, target);
            float minusDistance = Vector3.Distance(minus, target);
            return Mathf.Abs(plusDistance - minusDistance) < 0.000001f
                ? 0f
                : plusDistance < minusDistance ? 1f : -1f;
        }

        float plusPalmMotion = Vector3.Dot(plus - tipPosition, palm.Normal);
        float minusPalmMotion = Vector3.Dot(minus - tipPosition, palm.Normal);
        return Mathf.Abs(plusPalmMotion - minusPalmMotion) < 0.000001f
            ? 0f
            : plusPalmMotion > minusPalmMotion ? 1f : -1f;
    }

    private static PalmFrame CalculatePalmFrame(HandPoseController controller)
    {
        Transform root = controller.transform;
        Transform index = controller.GetFingerJoints(HandFinger.Index).FirstOrDefault();
        Transform middle = controller.GetFingerJoints(HandFinger.Middle).FirstOrDefault();
        Transform ring = controller.GetFingerJoints(HandFinger.Ring).FirstOrDefault();
        Transform little = controller.GetFingerJoints(HandFinger.Little).FirstOrDefault();
        if (index == null || middle == null || ring == null || little == null)
        {
            Vector3 fallbackNormal = controller.PalmNormalLocal.normalized;
            return new PalmFrame(Vector3.zero, fallbackNormal, Vector3.zero);
        }

        Vector3 indexPosition = root.InverseTransformPoint(index.position);
        Vector3 middlePosition = root.InverseTransformPoint(middle.position);
        Vector3 ringPosition = root.InverseTransformPoint(ring.position);
        Vector3 littlePosition = root.InverseTransformPoint(little.position);
        Vector3 knuckleCenter =
            (indexPosition + middlePosition + ringPosition + littlePosition) * 0.25f;
        Vector3 center = Vector3.Lerp(knuckleCenter, Vector3.zero, 0.48f);

        Vector3 configuredNormal = controller.PalmNormalLocal.normalized;
        return new PalmFrame(center, configuredNormal, indexPosition);
    }

    private static Quaternion GetRotationRelativeToRoot(Transform root, Transform target)
    {
        if (target == null || target == root)
            return Quaternion.identity;

        Stack<Transform> chain = new();
        Transform current = target;
        while (current != null && current != root)
        {
            chain.Push(current);
            current = current.parent;
        }
        if (current != root)
            return target.localRotation;

        Quaternion rotation = Quaternion.identity;
        while (chain.Count > 0)
            rotation *= chain.Pop().localRotation;
        return rotation;
    }

    private static Transform FindDeepestBoneChild(Transform joint)
    {
        Transform current = joint;
        int guard = 0;
        while (current != null && current.childCount > 0 && guard++ < 8)
            current = current.GetChild(0);
        return current == joint ? null : current;
    }

    private static bool CanEditScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Hand Pose] Play Mode를 종료한 후 실행하세요.");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[Hand Pose] 유효한 Scene이 열려 있지 않습니다.");
            return false;
        }
        return true;
    }

    private static T[] FindSceneObjects<T>() where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(item => item != null && item.gameObject.scene.IsValid() &&
                           !EditorUtility.IsPersistent(item))
            .ToArray();
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value.Replace(' ', '_');
    }
}
