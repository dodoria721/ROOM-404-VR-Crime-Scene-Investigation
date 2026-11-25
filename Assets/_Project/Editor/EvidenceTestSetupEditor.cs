using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EvidenceTestSetupEditor
{
    private const string MenuPath = "ROOM 404/Test Tools/Create E02-E06 Evidence Sources";
    private const string VrTestScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string RootObjectName = "TestEvidence_Sources";

    private readonly struct TestEvidenceDefinition
    {
        public string Id { get; }
        public string ObjectName { get; }
        public string EvidenceName { get; }
        public string Description { get; }

        public TestEvidenceDefinition(
            string id,
            string objectName,
            string evidenceName,
            string description)
        {
            Id = id;
            ObjectName = objectName;
            EvidenceName = evidenceName;
            Description = description;
        }
    }

    private static readonly TestEvidenceDefinition[] TestEvidenceDefinitions =
    {
        new(
            "E02",
            "TestEvidence_E02",
            "책상 모서리 충돌 흔적",
            "책상 모서리에서 발견된 충돌 흔적. 실제 사망 원인을 추리하는 핵심 증거."),
        new(
            "E03",
            "TestEvidence_E03",
            "정하린 개인 하드디스크",
            "과거 임상시험 CCTV와 장비 로그가 저장된 정하린의 개인 하드디스크."),
        new(
            "E04",
            "TestEvidence_E04",
            "박진호 연구비 문서",
            "박진호의 연구비 횡령 정황과 정하린의 요구가 기록된 문서."),
        new(
            "E05",
            "TestEvidence_E05",
            "김민수 협박 메시지",
            "연구 성과 도용에 분노한 김민수가 정하린에게 보낸 협박 메시지."),
        new(
            "E06",
            "TestEvidence_E06",
            "정하린 휴대폰 약속 기록",
            "사건 당일 이서연과 정하린의 마지막 만남 약속이 남은 휴대폰 기록.")
    };

    [MenuItem(MenuPath)]
    private static void CreateEvidenceSources()
    {
        if (!CanRunInCurrentScene(showMessage: true))
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject rootObject = EditorUIUtility.FindSceneObject(activeScene, RootObjectName);

        if (rootObject != null && rootObject.transform.parent != null)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 Test Tools",
                $"'{RootObjectName}' 오브젝트가 Scene 루트가 아닌 위치에 이미 존재합니다. " +
                "기존 계층을 변경하지 않기 위해 생성을 중단합니다.",
                "확인");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create E02-E06 Evidence Sources");

        bool sceneChanged = false;
        int createdCount = 0;
        int skippedCount = 0;

        if (rootObject == null)
        {
            rootObject = new GameObject(RootObjectName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create evidence test root");
            SceneManager.MoveGameObjectToScene(rootObject, activeScene);
            sceneChanged = true;
        }

        foreach (TestEvidenceDefinition definition in TestEvidenceDefinitions)
        {
            if (EditorUIUtility.FindSceneObject(activeScene, definition.ObjectName) != null)
            {
                skippedCount++;
                Debug.Log(
                    $"[ROOM 404] '{definition.ObjectName}' 오브젝트가 이미 존재하여 변경하지 않고 건너뜁니다.");
                continue;
            }

            CreateEvidenceSourceObject(rootObject.transform, definition);
            createdCount++;
            sceneChanged = true;
        }

        Undo.CollapseUndoOperations(undoGroup);

        if (sceneChanged)
            EditorSceneManager.MarkSceneDirty(activeScene);

        Selection.activeGameObject = rootObject;

        Debug.Log(
            $"[ROOM 404] E02-E06 테스트 증거 생성 완료. 생성: {createdCount}, 기존 오브젝트 건너뜀: {skippedCount}. " +
            "Scene은 자동 저장되지 않았습니다.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateCreateEvidenceSources()
    {
        return CanRunInCurrentScene(showMessage: false);
    }

    private static bool CanRunInCurrentScene(bool showMessage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (showMessage)
            {
                EditorUtility.DisplayDialog(
                    "ROOM 404 Test Tools",
                    "Play Mode에서는 테스트 증거 오브젝트를 생성할 수 없습니다.",
                    "확인");
            }

            return false;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        bool isVrTestScene = activeScene.IsValid()
            && activeScene.isLoaded
            && string.Equals(activeScene.path, VrTestScenePath, StringComparison.Ordinal);

        if (!isVrTestScene && showMessage)
        {
            EditorUtility.DisplayDialog(
                "ROOM 404 Test Tools",
                $"이 메뉴는 '{VrTestScenePath}' Scene에서만 실행할 수 있습니다.",
                "확인");
        }

        return isVrTestScene;
    }

    private static void CreateEvidenceSourceObject(
        Transform parent,
        TestEvidenceDefinition definition)
    {
        var evidenceObject = new GameObject(definition.ObjectName);
        Undo.RegisterCreatedObjectUndo(evidenceObject, $"Create {definition.ObjectName}");
        Undo.SetTransformParent(evidenceObject.transform, parent, $"Parent {definition.ObjectName}");

        evidenceObject.transform.localPosition = Vector3.zero;
        evidenceObject.transform.localRotation = Quaternion.identity;
        evidenceObject.transform.localScale = Vector3.one;

        EvidenceSource evidenceSource = Undo.AddComponent<EvidenceSource>(evidenceObject);
        var serializedSource = new SerializedObject(evidenceSource);

        SerializedProperty idProperty = serializedSource.FindProperty("evidenceId");
        SerializedProperty nameProperty = serializedSource.FindProperty("evidenceName");
        SerializedProperty descriptionProperty = serializedSource.FindProperty("description");

        if (idProperty == null || nameProperty == null || descriptionProperty == null)
        {
            Debug.LogError(
                $"[ROOM 404] EvidenceSource의 SerializedField를 찾을 수 없습니다: {definition.ObjectName}",
                evidenceObject);
            return;
        }

        idProperty.stringValue = definition.Id;
        nameProperty.stringValue = definition.EvidenceName;
        descriptionProperty.stringValue = definition.Description;
        serializedSource.ApplyModifiedProperties();
        EditorUtility.SetDirty(evidenceSource);
    }

}
