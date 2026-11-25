using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class LegacyGrabPoseCleanupEditor
{
    private const string InspectMenu = "ROOM 404/Maintenance/Inspect Legacy Grab Pose";
    private const string RemoveMenu = "ROOM 404/Maintenance/Remove Legacy Grab Pose";
    private const string TargetScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string UndoName = "Remove Legacy Grab Pose";

    [MenuItem(InspectMenu, false, 100)]
    private static void InspectActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!CanInspect(scene))
            return;

        List<GrabHandPose> components = FindInScene(scene);
        var report = new StringBuilder()
            .Append("[ROOM 404] ").Append(scene.path)
            .Append(" · GrabHandPose ").Append(components.Count).Append("개");

        foreach (GrabHandPose component in components)
        {
            report.AppendLine().Append(GetHierarchyPath(component.transform))
                .AppendLine().Append("  Left: ").Append(GetPosePath(component.LeftHandPose))
                .AppendLine().Append("  Right: ").Append(GetPosePath(component.RightHandPose));
        }

        Debug.Log(report.ToString());
    }

    [MenuItem(InspectMenu, true)]
    private static bool ValidateInspect() => CanInspect(SceneManager.GetActiveScene());

    [MenuItem(RemoveMenu, false, 101)]
    private static void RemoveFromActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!CanRemove(scene))
            return;

        List<GrabHandPose> components = FindInScene(scene);
        if (components.Count == 0)
        {
            Debug.Log("[ROOM 404] VR_Test에 제거할 GrabHandPose 컴포넌트가 없습니다.");
            return;
        }

        var message = new StringBuilder()
            .Append(scene.path).AppendLine()
            .Append("다음 GrabHandPose 컴포넌트 ").Append(components.Count).AppendLine("개를 제거합니다.")
            .AppendLine();
        foreach (GrabHandPose component in components)
            message.AppendLine(GetHierarchyPath(component.transform));
        message.AppendLine().Append("변경 사항은 씬을 수동으로 저장할 때 저장됩니다.");

        if (!EditorUtility.DisplayDialog(UndoName, message.ToString(), "제거", "취소"))
            return;

        int removed = RemoveComponents(scene, components);
        Debug.Log($"[ROOM 404] VR_Test의 GrabHandPose {removed}개를 제거했습니다.");
    }

    [MenuItem(RemoveMenu, true)]
    private static bool ValidateRemove() => CanRemove(SceneManager.GetActiveScene());

    private static bool CanInspect(Scene scene) =>
        !EditorApplication.isPlayingOrWillChangePlaymode &&
        PrefabStageUtility.GetCurrentPrefabStage() == null &&
        scene.IsValid() && scene.isLoaded && scene.path.StartsWith("Assets/", System.StringComparison.Ordinal);

    private static bool CanRemove(Scene scene) => CanInspect(scene) && scene.path == TargetScenePath;

    private static List<GrabHandPose> FindInScene(Scene scene)
    {
        var components = new List<GrabHandPose>();
        foreach (GameObject root in scene.GetRootGameObjects())
            components.AddRange(root.GetComponentsInChildren<GrabHandPose>(includeInactive: true));
        return components;
    }

    private static int RemoveComponents(Scene scene, List<GrabHandPose> components)
    {
        if (!CanRemove(scene) || scene != SceneManager.GetActiveScene())
            return 0;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        int removed = 0;
        try
        {
            foreach (GrabHandPose component in components)
            {
                if (component == null || component.gameObject.scene != scene)
                    continue;

                Undo.DestroyObjectImmediate(component);
                removed++;
            }
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
            if (removed > 0)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        return removed;
    }

    private static string GetHierarchyPath(Transform item)
    {
        var names = new Stack<string>();
        for (Transform ancestor = item; ancestor != null; ancestor = ancestor.parent)
            names.Push(ancestor.name);
        return string.Join("/", names);
    }

    private static string GetPosePath(HandPoseData pose) =>
        pose == null ? "(없음)" : AssetDatabase.GetAssetPath(pose);
}
