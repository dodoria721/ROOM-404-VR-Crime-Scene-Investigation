using System;
using System.Collections.Generic;
using UnityEngine;

public enum HandSide
{
    Left,
    Right
}

public enum HandFinger
{
    Thumb,
    Index,
    Middle,
    Ring,
    Little
}

public enum HandPoseKind
{
    Open,
    Idle,
    Grip,
    Trigger,
    Point,
    FullGrip,
    ObjectGrab
}

[CreateAssetMenu(fileName = "HandPose", menuName = "ROOM 404/XR/Hand Pose Data")]
public sealed class HandPoseData : ScriptableObject
{
    [Serializable]
    public struct JointRotation
    {
        [SerializeField] private string transformPath;
        [SerializeField] private Quaternion localRotation;

        public string TransformPath => transformPath;
        public Quaternion LocalRotation => localRotation;

        public JointRotation(string path, Quaternion rotation)
        {
            transformPath = path;
            localRotation = rotation;
        }
    }

    [SerializeField] private HandSide handSide;
    [SerializeField] private HandPoseKind poseKind;
    [SerializeField] private string displayName;
    [TextArea(2, 4)]
    [SerializeField] private string generationNotes;
    [SerializeField] private Vector3 sourceObjectSize;
    [SerializeField] private bool generatedDraft;
    [SerializeField] private List<JointRotation> jointRotations = new();

    private Dictionary<string, Quaternion> rotationLookup;

    public HandSide Side => handSide;
    public HandPoseKind Kind => poseKind;
    public string DisplayName => displayName;
    public string GenerationNotes => generationNotes;
    public Vector3 SourceObjectSize => sourceObjectSize;
    public bool IsGeneratedDraft => generatedDraft;
    public IReadOnlyList<JointRotation> JointRotations => jointRotations;

    public bool TryGetLocalRotation(Transform handRoot, Transform joint, out Quaternion rotation)
    {
        rotation = Quaternion.identity;
        if (handRoot == null || joint == null)
            return false;

        EnsureLookup();
        return rotationLookup.TryGetValue(GetRelativePath(handRoot, joint), out rotation);
    }

    public void ReplaceData(
        HandSide side,
        HandPoseKind kind,
        string poseDisplayName,
        string notes,
        Vector3 objectSize,
        bool isDraft,
        IEnumerable<JointRotation> rotations)
    {
        handSide = side;
        poseKind = kind;
        displayName = poseDisplayName;
        generationNotes = notes;
        sourceObjectSize = objectSize;
        generatedDraft = isDraft;
        jointRotations.Clear();
        if (rotations != null)
            jointRotations.AddRange(rotations);
        rotationLookup = null;
    }

    public void CaptureCurrent(HandPoseController controller)
    {
        if (controller == null)
            return;

        List<JointRotation> rotations = new();
        foreach (Transform joint in controller.GetControlledJoints())
        {
            rotations.Add(new JointRotation(
                GetRelativePath(controller.transform, joint), joint.localRotation));
        }

        handSide = controller.Side;
        ReplaceJointRotations(rotations, true);
    }

    public void ReplaceJointRotations(
        IEnumerable<JointRotation> rotations, bool markAsUserEdited)
    {
        jointRotations.Clear();
        if (rotations != null)
            jointRotations.AddRange(rotations);
        if (markAsUserEdited)
            generatedDraft = false;
        rotationLookup = null;
    }

    public static string GetRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null)
            return string.Empty;
        if (root == target)
            return string.Empty;

        Stack<string> names = new();
        Transform current = target;
        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return current == root ? string.Join("/", names) : target.name;
    }

    private void EnsureLookup()
    {
        if (rotationLookup != null)
            return;

        rotationLookup = new Dictionary<string, Quaternion>(jointRotations.Count);
        foreach (JointRotation entry in jointRotations)
        {
            if (!string.IsNullOrEmpty(entry.TransformPath))
                rotationLookup[entry.TransformPath] = entry.LocalRotation;
        }
    }

    private void OnValidate()
    {
        rotationLookup = null;
    }
}
