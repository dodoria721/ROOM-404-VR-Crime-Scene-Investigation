using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class HandPoseController : MonoBehaviour
{
    public enum CurlDirectionOverride
    {
        Auto,
        Normal,
        Inverted
    }

    public enum CurlAxisMode
    {
        Auto,
        Manual
    }

    [Serializable]
    public sealed class JointCurlCalibration
    {
        [SerializeField] private CurlAxisMode axisMode;
        [Tooltip("Curl axis in this joint's Open-pose local coordinate system.")]
        [SerializeField] private Vector3 localCurlAxis = Vector3.right;
        [Tooltip("Use this angle instead of the generated pose profile angle.")]
        [SerializeField] private bool overrideCurlAngle;
        [Range(0f, 120f)]
        [SerializeField] private float curlAngleDegrees = 45f;
        [SerializeField] private bool invertDirection;

        public CurlAxisMode AxisMode => axisMode;
        public Vector3 LocalCurlAxis =>
            localCurlAxis.sqrMagnitude > 0.0001f ? localCurlAxis.normalized : Vector3.right;
        public bool OverrideCurlAngle => overrideCurlAngle;
        public float CurlAngleDegrees => curlAngleDegrees;
        public bool InvertDirection => invertDirection;

        public void SetAutoAxis(Vector3 axis)
        {
            if (axisMode == CurlAxisMode.Auto && axis.sqrMagnitude > 0.0001f)
                localCurlAxis = axis.normalized;
        }

        public float ResolveAngle(float generatedAngle, float legacyMultiplier)
        {
            float angle = overrideCurlAngle ? curlAngleDegrees : generatedAngle * legacyMultiplier;
            return Mathf.Max(0f, angle);
        }
    }

    [Serializable]
    public sealed class FingerCurlCalibration
    {
        [SerializeField] private CurlDirectionOverride directionOverride;
        [Tooltip("Metacarpal, Proximal, Intermediate, Distal angle multipliers.")]
        [SerializeField] private Vector4 jointAngleMultipliers = Vector4.one;
        [Tooltip("Thumb Metacarpal; Proximal for the other fingers.")]
        [SerializeField] private JointCurlCalibration joint0 = new();
        [Tooltip("Thumb Proximal; Intermediate for the other fingers.")]
        [SerializeField] private JointCurlCalibration joint1 = new();
        [Tooltip("Thumb Intermediate; Distal for the other fingers.")]
        [SerializeField] private JointCurlCalibration joint2 = new();
        [Tooltip("Thumb Distal only.")]
        [SerializeField] private JointCurlCalibration joint3 = new();

        public CurlDirectionOverride DirectionOverride => directionOverride;

        public float GetAngleMultiplier(int jointIndex)
        {
            return jointIndex switch
            {
                0 => jointAngleMultipliers.x,
                1 => jointAngleMultipliers.y,
                2 => jointAngleMultipliers.z,
                _ => jointAngleMultipliers.w
            };
        }

        public JointCurlCalibration GetJoint(int jointIndex)
        {
            return jointIndex switch
            {
                0 => joint0,
                1 => joint1,
                2 => joint2,
                _ => joint3
            };
        }
    }

    [Serializable]
    public sealed class FingerDefinition
    {
        [Tooltip("Optional. Use this for thumb_trapez or another metacarpal joint when needed.")]
        [SerializeField] private Transform metacarpal;
        [SerializeField] private Transform proximal;
        [SerializeField] private Transform intermediate;
        [SerializeField] private Transform distal;

        [HideInInspector, SerializeField] private Quaternion openMetacarpal = Quaternion.identity;
        [HideInInspector, SerializeField] private Quaternion openProximal = Quaternion.identity;
        [HideInInspector, SerializeField] private Quaternion openIntermediate = Quaternion.identity;
        [HideInInspector, SerializeField] private Quaternion openDistal = Quaternion.identity;
        [HideInInspector, SerializeField] private Quaternion closedMetacarpal = Quaternion.identity;
        [HideInInspector, SerializeField] private Quaternion closedProximal = Quaternion.identity;
        [HideInInspector, SerializeField] private Quaternion closedIntermediate = Quaternion.identity;
        [HideInInspector, SerializeField] private Quaternion closedDistal = Quaternion.identity;

        private Quaternion pointMetacarpal;
        private Quaternion pointProximal;
        private Quaternion pointIntermediate;
        private Quaternion pointDistal;

        public void CaptureOpen()
        {
            Capture(metacarpal, ref openMetacarpal);
            Capture(proximal, ref openProximal);
            Capture(intermediate, ref openIntermediate);
            Capture(distal, ref openDistal);
        }

        public void CaptureClosed()
        {
            Capture(metacarpal, ref closedMetacarpal);
            Capture(proximal, ref closedProximal);
            Capture(intermediate, ref closedIntermediate);
            Capture(distal, ref closedDistal);
        }

        public void CopyOpenToClosed()
        {
            closedMetacarpal = openMetacarpal;
            closedProximal = openProximal;
            closedIntermediate = openIntermediate;
            closedDistal = openDistal;
        }

        public void SetClosedFromPose(Transform handRoot, HandPoseData pose)
        {
            SetFromPose(handRoot, pose, metacarpal, ref closedMetacarpal);
            SetFromPose(handRoot, pose, proximal, ref closedProximal);
            SetFromPose(handRoot, pose, intermediate, ref closedIntermediate);
            SetFromPose(handRoot, pose, distal, ref closedDistal);
        }

        public void CachePointPose(Transform handRoot, HandPoseData pose)
        {
            pointMetacarpal = openMetacarpal;
            pointProximal = openProximal;
            pointIntermediate = openIntermediate;
            pointDistal = openDistal;
            SetFromPose(handRoot, pose, metacarpal, ref pointMetacarpal);
            SetFromPose(handRoot, pose, proximal, ref pointProximal);
            SetFromPose(handRoot, pose, intermediate, ref pointIntermediate);
            SetFromPose(handRoot, pose, distal, ref pointDistal);
        }

        public void Apply(float inputAmount, float pointWeight)
        {
            ApplyJoint(metacarpal, openMetacarpal, closedMetacarpal, inputAmount, pointMetacarpal, pointWeight);
            ApplyJoint(proximal, openProximal, closedProximal, inputAmount, pointProximal, pointWeight);
            ApplyJoint(intermediate, openIntermediate, closedIntermediate, inputAmount, pointIntermediate, pointWeight);
            ApplyJoint(distal, openDistal, closedDistal, inputAmount, pointDistal, pointWeight);
        }

#if UNITY_EDITOR
        public void PreviewAsset(Transform handRoot, HandPoseData pose, float amount, bool useCapturedClosed)
        {
            PreviewJoint(metacarpal, openMetacarpal, closedMetacarpal);
            PreviewJoint(proximal, openProximal, closedProximal);
            PreviewJoint(intermediate, openIntermediate, closedIntermediate);
            PreviewJoint(distal, openDistal, closedDistal);

            void PreviewJoint(Transform joint, Quaternion open, Quaternion capturedClosed)
            {
                Quaternion closed = useCapturedClosed ? capturedClosed : open;
                SetFromPose(handRoot, pose, joint, ref closed);
                ApplyJoint(joint, open, closed, amount);
            }
        }
#endif

        public void AddJoints(List<Transform> destination)
        {
            AddUnique(destination, metacarpal);
            AddUnique(destination, proximal);
            AddUnique(destination, intermediate);
            AddUnique(destination, distal);
        }

        public Transform[] GetJoints()
        {
            List<Transform> joints = new(4);
            AddJoints(joints);
            return joints.ToArray();
        }

        public bool TryGetJointAndOpenRotation(
            int jointIndex, out Transform joint, out Quaternion openRotation)
        {
            Transform[] joints = GetJoints();
            if (jointIndex < 0 || jointIndex >= joints.Length)
            {
                joint = null;
                openRotation = Quaternion.identity;
                return false;
            }

            joint = joints[jointIndex];
            if (joint == metacarpal)
                openRotation = openMetacarpal;
            else if (joint == proximal)
                openRotation = openProximal;
            else if (joint == intermediate)
                openRotation = openIntermediate;
            else
                openRotation = openDistal;
            return joint != null;
        }

        private static void Capture(Transform joint, ref Quaternion destination)
        {
            if (joint != null)
                destination = joint.localRotation;
        }

        private static void SetFromPose(
            Transform handRoot, HandPoseData pose, Transform joint, ref Quaternion destination)
        {
            if (pose != null && pose.TryGetLocalRotation(handRoot, joint, out Quaternion rotation))
                destination = rotation;
        }

        private static void ApplyJoint(
            Transform joint, Quaternion open, Quaternion closed, float inputAmount,
            Quaternion point = default, float pointWeight = 0f)
        {
            if (joint != null)
            {
                Quaternion rotation = Quaternion.Slerp(open, closed, inputAmount);
                joint.localRotation = pointWeight > 0f ? Quaternion.Slerp(rotation, point, pointWeight) : rotation;
            }
        }

        private static void AddUnique(List<Transform> destination, Transform joint)
        {
            if (joint != null && !destination.Contains(joint))
                destination.Add(joint);
        }
    }

    [Header("Hand")]
    [SerializeField] private HandSide handSide;

    [Header("Analog Input (existing shared actions)")]
    [SerializeField] private InputActionReference gripInput;
    [SerializeField] private InputActionReference triggerInput;

    [Header("Finger Joints")]
    [SerializeField] private FingerDefinition thumb = new();
    [SerializeField] private FingerDefinition index = new();
    [SerializeField] private FingerDefinition middle = new();
    [SerializeField] private FingerDefinition ring = new();
    [SerializeField] private FingerDefinition little = new();

    [Header("Motion")]
    [Min(0.001f)]
    [SerializeField] private float inputSmoothTime = 0.06f;
    [SerializeField] private bool clampInput = true;

    [Header("Automatic Curl Calibration")]
    [Tooltip("Inverts automatically detected curl directions for this hand only.")]
    [SerializeField] private bool invertCurlDirection;
    [Tooltip("Palm-facing normal in HandPoseController local space. Used to define the palm plane.")]
    [SerializeField] private Vector3 palmNormalLocal = Vector3.forward;
    [SerializeField] private FingerCurlCalibration thumbCurl = new();
    [SerializeField] private FingerCurlCalibration indexCurl = new();
    [SerializeField] private FingerCurlCalibration middleCurl = new();
    [SerializeField] private FingerCurlCalibration ringCurl = new();
    [SerializeField] private FingerCurlCalibration littleCurl = new();

    [Header("Generated Input Pose References")]
    [SerializeField] private HandPoseData generatedGripPose;
    [SerializeField] private HandPoseData generatedTriggerPose;
    [SerializeField] private HandPoseData uiPointPose;

    [Header("Captured Pose State")]
    [SerializeField, HideInInspector] private bool openPoseCaptured;
    [SerializeField, HideInInspector] private bool closedPoseCaptured;

    private float currentGrip;
    private float currentTrigger;
    private float gripVelocity;
    private float triggerVelocity;
    private bool warnedMissingInput;
    private bool hasUIPointPose;
    private bool uiPointRequested;
    private float uiPointWeight;
    private float uiPointVelocity;

    public HandSide Side => handSide;
    public float CurrentGrip => currentGrip;
    public float CurrentTrigger => currentTrigger;
    public bool HasOpenPose => openPoseCaptured;
    public bool HasClosedPose => closedPoseCaptured;
    public bool InvertCurlDirection => invertCurlDirection;
    public Vector3 PalmNormalLocal => palmNormalLocal;
    public HandPoseData GeneratedGripPose => generatedGripPose;
    public HandPoseData GeneratedTriggerPose => generatedTriggerPose;

    private void Awake()
    {
        if (!openPoseCaptured)
            CaptureOpenPose();
        if (generatedGripPose != null && generatedTriggerPose != null)
            ApplyGeneratedInputPoses(generatedGripPose, generatedTriggerPose);
        else if (!closedPoseCaptured)
            CopyOpenPoseToClosed();

        hasUIPointPose = handSide == HandSide.Right && uiPointPose != null &&
                         uiPointPose.Side == handSide && uiPointPose.Kind == HandPoseKind.Point;
        if (hasUIPointPose)
            ForEachFinger(finger => finger.CachePointPose(transform, uiPointPose));
    }

    private void OnEnable()
    {
        currentGrip = 0f;
        currentTrigger = 0f;
        gripVelocity = 0f;
        triggerVelocity = 0f;
        warnedMissingInput = false;
    }

    private void Start()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isActiveAndEnabled)
        {
            Debug.LogWarning(
                "[Hand Pose] An enabled Animator may overwrite joint rotations applied in LateUpdate.",
                this);
        }
    }

    private void LateUpdate()
    {
        currentGrip = Mathf.SmoothDamp(
            currentGrip, ReadInput(gripInput), ref gripVelocity, inputSmoothTime,
            Mathf.Infinity, Time.deltaTime);
        currentTrigger = Mathf.SmoothDamp(
            currentTrigger, ReadInput(triggerInput), ref triggerVelocity, inputSmoothTime,
            Mathf.Infinity, Time.deltaTime);

        if (hasUIPointPose && (uiPointRequested || uiPointWeight > 0f))
        {
            float target = uiPointRequested ? 1f : 0f;
            uiPointWeight = Mathf.SmoothDamp(uiPointWeight, target, ref uiPointVelocity,
                inputSmoothTime, Mathf.Infinity, Time.deltaTime);
            if (Mathf.Abs(uiPointWeight - target) < 0.001f)
            {
                uiPointWeight = target;
                uiPointVelocity = 0f;
            }
        }

        ApplyPose(currentGrip, currentTrigger, uiPointWeight);
    }

    public void SetUIPointMode(bool enabled)
    {
        if (handSide == HandSide.Right)
            uiPointRequested = enabled;
    }

    private void OnDisable()
    {
        uiPointWeight = 0f;
        uiPointVelocity = 0f;
        if (openPoseCaptured)
            ApplyPoseImmediate(0f, 0f);
    }

    public void CaptureOpenPose()
    {
        ForEachFinger(finger => finger.CaptureOpen());
        openPoseCaptured = true;
    }

    public void CaptureClosedPose()
    {
        ForEachFinger(finger => finger.CaptureClosed());
        generatedGripPose = null;
        generatedTriggerPose = null;
        closedPoseCaptured = true;
    }

    public void ApplyGeneratedInputPoses(HandPoseData gripPose, HandPoseData triggerPose)
    {
        if (gripPose == null || triggerPose == null ||
            gripPose.Side != handSide || triggerPose.Side != handSide)
            return;

        generatedGripPose = gripPose;
        generatedTriggerPose = triggerPose;
        thumb.SetClosedFromPose(transform, gripPose);
        middle.SetClosedFromPose(transform, gripPose);
        ring.SetClosedFromPose(transform, gripPose);
        little.SetClosedFromPose(transform, gripPose);
        index.SetClosedFromPose(transform, triggerPose);
        closedPoseCaptured = true;
    }

    public float GetGeneratedCurlSign(HandFinger finger, float detectedSign)
    {
        FingerCurlCalibration calibration = GetCurlCalibration(finger);
        return calibration.DirectionOverride switch
        {
            CurlDirectionOverride.Normal => detectedSign,
            CurlDirectionOverride.Inverted => -detectedSign,
            _ => invertCurlDirection ? -detectedSign : detectedSign
        };
    }

    public float GetGeneratedAngleMultiplier(HandFinger finger, int jointIndex)
    {
        return Mathf.Max(0f, GetCurlCalibration(finger).GetAngleMultiplier(jointIndex));
    }

    public JointCurlCalibration GetJointCurlCalibration(HandFinger finger, int jointIndex)
    {
        return GetCurlCalibration(finger).GetJoint(jointIndex);
    }

    public void SetGeneratedLocalCurlAxis(
        HandFinger finger, int jointIndex, Vector3 localAxis)
    {
        GetJointCurlCalibration(finger, jointIndex).SetAutoAxis(localAxis);
    }

    public int GetFingerJointCount(HandFinger finger)
    {
        return GetFinger(finger).GetJoints().Length;
    }

    public bool TryGetFingerJoint(
        HandFinger finger, int jointIndex, out Transform joint, out Quaternion openRotation)
    {
        return GetFinger(finger).TryGetJointAndOpenRotation(
            jointIndex, out joint, out openRotation);
    }

    public void PreviewSingleJoint(HandFinger finger, int jointIndex, float angleDegrees)
    {
        if (!TryGetFingerJoint(finger, jointIndex, out Transform joint, out Quaternion open))
            return;

        JointCurlCalibration calibration = GetJointCurlCalibration(finger, jointIndex);
        float direction = GetGeneratedCurlSign(finger, 1f);
        if (calibration.InvertDirection)
            direction = -direction;
        joint.localRotation = open * Quaternion.AngleAxis(
            angleDegrees * direction, calibration.LocalCurlAxis);
    }

    public void ResetSingleJoint(HandFinger finger, int jointIndex)
    {
        if (TryGetFingerJoint(finger, jointIndex, out Transform joint, out Quaternion open))
            joint.localRotation = open;
    }

    public void RestoreOpenPose()
    {
        if (openPoseCaptured)
            ApplyPoseImmediate(0f, 0f);
    }

    public void ApplyPoseImmediate(float gripAmount, float triggerAmount)
    {
        ApplyPose(gripAmount, triggerAmount);
    }

#if UNITY_EDITOR
    public void ApplyDataPoseImmediate(HandPoseData pose, float weight)
    {
        weight = Mathf.Clamp01(weight);
        ForEachFinger(finger => finger.PreviewAsset(transform, pose, weight, false));
    }

    public void PreviewInputPose(float gripAmount, float triggerAmount)
    {
        // Awake resolves asset rotations in Play Mode. In Edit Mode, read the
        // current assets without changing the captured or serialized rotations.
        if (generatedGripPose == null || generatedTriggerPose == null ||
            generatedGripPose.Side != handSide || generatedTriggerPose.Side != handSide)
        {
            ApplyPoseImmediate(gripAmount, triggerAmount);
            return;
        }

        thumb.PreviewAsset(transform, generatedGripPose, gripAmount, true);
        middle.PreviewAsset(transform, generatedGripPose, gripAmount, true);
        ring.PreviewAsset(transform, generatedGripPose, gripAmount, true);
        little.PreviewAsset(transform, generatedGripPose, gripAmount, true);
        index.PreviewAsset(transform, generatedTriggerPose, triggerAmount, true);
    }
#endif

    public Transform[] GetControlledJoints()
    {
        List<Transform> joints = new(20);
        ForEachFinger(finger => finger.AddJoints(joints));
        return joints.ToArray();
    }

    public Transform[] GetFingerJoints(HandFinger finger)
    {
        return GetFinger(finger).GetJoints();
    }

    private void ApplyPose(float gripAmount, float triggerAmount, float pointWeight = 0f)
    {
        if (clampInput)
        {
            gripAmount = Mathf.Clamp01(gripAmount);
            triggerAmount = Mathf.Clamp01(triggerAmount);
        }

        thumb.Apply(gripAmount, pointWeight);
        middle.Apply(gripAmount, pointWeight);
        ring.Apply(gripAmount, pointWeight);
        little.Apply(gripAmount, pointWeight);
        index.Apply(triggerAmount, pointWeight);
    }

    private float ReadInput(InputActionReference reference)
    {
        InputAction action = reference != null ? reference.action : null;
        if (action == null || !action.enabled)
        {
            if (!warnedMissingInput)
            {
                Debug.LogWarning(
                    "[Hand Pose] Grip/Trigger action is missing or disabled. " +
                    "Assign existing Select Value and Activate Value actions.", this);
                warnedMissingInput = true;
            }
            return 0f;
        }

        float value = action.ReadValue<float>();
        return clampInput ? Mathf.Clamp01(value) : value;
    }

    private FingerDefinition GetFinger(HandFinger finger)
    {
        return finger switch
        {
            HandFinger.Thumb => thumb,
            HandFinger.Index => index,
            HandFinger.Middle => middle,
            HandFinger.Ring => ring,
            HandFinger.Little => little,
            _ => index
        };
    }

    private FingerCurlCalibration GetCurlCalibration(HandFinger finger)
    {
        return finger switch
        {
            HandFinger.Thumb => thumbCurl,
            HandFinger.Index => indexCurl,
            HandFinger.Middle => middleCurl,
            HandFinger.Ring => ringCurl,
            HandFinger.Little => littleCurl,
            _ => indexCurl
        };
    }

    private void CopyOpenPoseToClosed()
    {
        ForEachFinger(finger => finger.CopyOpenToClosed());
    }

    private void ForEachFinger(Action<FingerDefinition> action)
    {
        action(thumb);
        action(index);
        action(middle);
        action(ring);
        action(little);
    }

    private void OnValidate()
    {
        inputSmoothTime = Mathf.Max(0.001f, inputSmoothTime);
        if (palmNormalLocal.sqrMagnitude < 0.0001f)
            palmNormalLocal = Vector3.forward;
    }
}
