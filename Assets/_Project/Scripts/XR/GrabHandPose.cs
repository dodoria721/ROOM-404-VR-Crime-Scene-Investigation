using UnityEngine;

// Keep the original script GUID and serialized references for existing scenes.
// Legacy object poses are archival data; this component has no runtime behavior.
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class GrabHandPose : MonoBehaviour
{
    [SerializeField, HideInInspector] private HandPoseData leftHandPose = null;
    [SerializeField, HideInInspector] private HandPoseData rightHandPose = null;

    public HandPoseData LeftHandPose => leftHandPose;
    public HandPoseData RightHandPose => rightHandPose;
}
