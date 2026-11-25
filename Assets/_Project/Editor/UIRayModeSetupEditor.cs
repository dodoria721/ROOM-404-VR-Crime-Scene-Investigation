using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

internal static class UIRayModeSetupEditor
{
    private const string MenuPath = "ROOM 404/XR/Configure UI Ray Mode";
    private const string InvestigationScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string BriefingScenePath = "Assets/_Project/Scenes/Briefing.unity";
    private const string PointPosePath = "Assets/_Project/HandPoses/Generated/Defaults/Right_Point.asset";

    [MenuItem(MenuPath, validate = true)]
    private static bool CanConfigure() =>
        !EditorApplication.isPlayingOrWillChangePlaymode &&
        PrefabStageUtility.GetCurrentPrefabStage() == null &&
        SceneManager.GetActiveScene().path is InvestigationScenePath or MainMenuScenePath or BriefingScenePath;

    [MenuItem(MenuPath)]
    private static void Configure()
    {
        if (!CanConfigure())
            return;

        try
        {
            var scene = SceneManager.GetActiveScene();
            var ray = FindOne<XRRayInteractor>(scene, item => item.name == "RightUIRay");
            var direct = ray.GetComponentsInParent<XRDirectInteractor>(true)
                .Single(item => item.handedness == InteractorHandedness.Right);
            if (direct.gameObject == ray.gameObject || ray.handedness != InteractorHandedness.Right ||
                !ray.enabled || !ray.enableUIInteraction || ray.GetComponent<XRInteractorLineVisual>() == null ||
                ray.uiPressInput.inputSourceMode != XRInputButtonReader.InputSourceMode.InputActionReference ||
                ray.uiPressInput.inputActionReferencePerformed == null)
                throw new InvalidOperationException("RightUIRay must be a separate enabled right-hand XR UI Ray using the shared UI Press Action Reference.");

            var hand = direct.GetComponentsInChildren<HandPoseController>(true)
                .Single(item => item.Side == HandSide.Right);
            if (hand.transform.IsChildOf(ray.transform))
                throw new InvalidOperationException("The right hand must stay active independently of RightUIRay.");

            var pointPose = AssetDatabase.LoadAssetAtPath<HandPoseData>(PointPosePath);
            if (pointPose == null || pointPose.Side != HandSide.Right || pointPose.Kind != HandPoseKind.Point)
                throw new InvalidOperationException("Could not find the right-hand Point pose at " + PointPosePath);

            var panels = GetInteractivePanels(scene);
            if (panels.Any(panel => panel == null || panel.GetComponentInChildren<Button>(true) == null) ||
                panels.Distinct().Count() != panels.Length)
                throw new InvalidOperationException($"Expected distinct interactive panels connected to the UI in {scene.name}.");

            if (scene.path != InvestigationScenePath)
                ValidateMenuScene(scene, ray, panels);

            var controller = FindInScene<UIRayModeController>(scene).SingleOrDefault();
            if (controller != null && controller.gameObject != direct.gameObject)
                throw new InvalidOperationException("An existing UI Ray Mode Controller is outside the right Direct Interactor.");

            var handState = new SerializedObject(hand);
            var pointSlot = handState.FindProperty("uiPointPose");
            if (pointSlot.objectReferenceValue is HandPoseData existingPoint &&
                (existingPoint.Side != HandSide.Right || existingPoint.Kind != HandPoseKind.Point))
                throw new InvalidOperationException("The assigned UI Point Pose must be a right-hand Point asset.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure UI Ray Mode");
            bool addedController = controller == null;
            if (addedController)
                controller = Undo.AddComponent<UIRayModeController>(direct.gameObject);

            var state = new SerializedObject(controller);
            state.FindProperty("rightHand").objectReferenceValue = hand;
            state.FindProperty("rightDirectInteractor").objectReferenceValue = direct;
            state.FindProperty("rightUIRay").objectReferenceValue = ray;
            var slots = state.FindProperty("interactivePanels");
            var configuredPanels = Enumerable.Range(0, slots.arraySize)
                .Select(i => slots.GetArrayElementAtIndex(i).objectReferenceValue as GameObject)
                .Where(panel => panel != null)
                .ToArray();
            // The shared right controller prefab can contain seven empty VR_Test
            // slots. Populate the instance using this scene's available panels.
            var desiredPanels = configuredPanels.Length == 0
                ? panels
                : scene.path == InvestigationScenePath
                    ? configuredPanels
                    : configuredPanels.Concat(panels).Distinct().ToArray();
            if (scene.path != InvestigationScenePath || configuredPanels.Length == 0)
            {
                slots.arraySize = desiredPanels.Length;
                for (int i = 0; i < desiredPanels.Length; i++)
                    slots.GetArrayElementAtIndex(i).objectReferenceValue = desiredPanels[i];
            }

            if (pointSlot.objectReferenceValue == null)
                pointSlot.objectReferenceValue = pointPose;

            bool controllerChanged = state.ApplyModifiedProperties();
            bool handChanged = handState.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
            if (addedController || controllerChanged || handChanged)
                EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = controller.gameObject;
            Debug.Log($"[UI Ray Mode] Review the interactive panel references and save {scene.name} manually.", controller);
        }
        catch (InvalidOperationException exception)
        {
            EditorUtility.DisplayDialog("UI Ray Mode", exception.Message, "OK");
        }
    }

    private static GameObject[] GetInteractivePanels(Scene scene)
    {
        if (scene.path == MainMenuScenePath)
        {
            var menu = FindOne<MainMenuUIController>(scene);
            var start = ReadReference<Button>(menu, "startButton");
            var controls = ReadReference<Button>(menu, "controlsButton");
            var quit = ReadReference<Button>(menu, "quitButton");
            var mainPanel = start != null ? start.transform.parent : null;
            if (mainPanel == null || mainPanel.name != "MainMenuPanel" || quit == null ||
                !quit.transform.IsChildOf(mainPanel) || (controls != null && !controls.transform.IsChildOf(mainPanel)))
                throw new InvalidOperationException("Main menu buttons do not match MainMenuPanel.");

            var controlsPanel = ReadPanel(menu, "controlsPanel");
            if (controlsPanel == null)
            {
                Debug.LogWarning("[UI Ray Mode] Controls panel is unavailable. Only MainMenuPanel will be linked.", menu);
                return new[] { mainPanel.gameObject };
            }

            var close = ReadReference<Button>(menu, "closeControlsButton");
            if (controls == null || close == null || !close.transform.IsChildOf(controlsPanel.transform))
                throw new InvalidOperationException("The controls panel must have its open and close button references.");
            return new[] { mainPanel.gameObject, controlsPanel };
        }

        if (scene.path == BriefingScenePath)
        {
            var briefing = FindOne<BriefingUIController>(scene);
            var start = ReadReference<Button>(briefing, "startInvestigationButton");
            var back = ReadReference<Button>(briefing, "returnToMainMenuButton");
            var canvas = start != null ? start.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null || canvas.name != "BriefingCanvas" || back == null ||
                back.GetComponentInParent<Canvas>(true) != canvas)
                throw new InvalidOperationException("The two scene transition buttons must belong to BriefingCanvas.");
            return new[] { canvas.gameObject };
        }

        var unlockRoot = ReadPanel(FindOne<DeductionStartUIController>(scene), "startAreaRoot");
        var unlockModal = unlockRoot != null ? unlockRoot.transform.Find("UnlockModal") : null;
        var deduction = FindOne<DeductionUIController>(scene);
        var ending = FindOne<EndingUIController>(scene);
        return new[]
        {
            ReadPanel(FindOne<NotebookToggleController>(scene), "notebookRoot"),
            unlockModal != null ? unlockModal.gameObject : null,
            ReadPanel(FindOne<CaseTypeSelectionController>(scene), "caseTypeRoot"),
            ReadPanel(deduction, "deductionPanel"),
            ReadPanel(deduction, "confirmationPanel"),
            ReadPanel(ending, "successPanel"),
            ReadPanel(ending, "failurePanel"),
        };
    }

    private static void ValidateMenuScene(Scene scene, XRRayInteractor ray, GameObject[] panels)
    {
        var events = FindOne<EventSystem>(scene);
        var input = FindOne<XRUIInputModule>(scene);
        if (events.gameObject != input.gameObject || !events.isActiveAndEnabled || !input.isActiveAndEnabled)
            throw new InvalidOperationException("Expected an active EventSystem with the existing XR UI Input Module.");

        foreach (var canvas in panels.Select(panel => panel.GetComponentInParent<Canvas>(true)).Distinct())
        {
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace || canvas.worldCamera == null ||
                canvas.GetComponents<TrackedDeviceGraphicRaycaster>().Count(item => item.enabled) != 1)
                throw new InvalidOperationException("Menu UI requires a World Space Canvas, event camera, and one enabled Tracked Device Graphic Raycaster.");
            int handsLayer = LayerMask.NameToLayer("XR_Hands");
            if (handsLayer < 0 || (canvas.worldCamera.cullingMask & (1 << handsLayer)) == 0 ||
                (canvas.worldCamera.cullingMask & (1 << ray.gameObject.layer)) == 0)
                throw new InvalidOperationException("The existing menu Event Camera must render the hand and Ray layers.");
        }
    }

    private static GameObject ReadPanel(Component controller, string field) =>
        ReadReference<GameObject>(controller, field);

    private static T ReadReference<T>(Component controller, string field) where T : UnityEngine.Object =>
        new SerializedObject(controller).FindProperty(field)?.objectReferenceValue as T;

    private static T FindOne<T>(Scene scene, Func<T, bool> predicate = null) where T : Component
    {
        var matches = FindInScene<T>(scene).Where(item => predicate == null || predicate(item)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"Expected one {typeof(T).Name} in {scene.name}, found {matches.Length}.");
        return matches[0];
    }

    private static System.Collections.Generic.IEnumerable<T> FindInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
}
