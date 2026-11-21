using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot scene setup for rendering the in-game World Space UI through
/// an XR-compatible URP Overlay Camera. Run with -executeMethod.
/// </summary>
public static class XRUIOverlaySetupEditor
{
    private const string ScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string LayerName = "XR_UI_Overlay";
    private const int PreferredLayer = 8;
    private const string HandLayerName = "XR_Hands";
    private const int PreferredHandLayer = 9;
    private const string OverlayCameraName = "XRUIOverlayCamera";

    private static readonly string[] TargetCanvasNames =
    {
        "EvidenceInspectionCanvas",
        "EvidenceNotificationCanvas",
        "DeductionCanvas",
        "EndingCanvas",
        "DeductionUnlockCanvas"
    };

    private static readonly string[] HandVisualNames =
    {
        "LeftHandVisual",
        "RightHandVisual"
    };

    [InitializeOnLoadMethod]
    private static void ApplyOnceAfterScriptReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (LayerMask.NameToLayer(LayerName) >= 0 && LayerMask.NameToLayer(HandLayerName) >= 0)
                return;

            try
            {
                Apply();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ROOM 404] XR UI Overlay 자동 적용 중단: {exception.Message}\n{exception}");
            }
        };
    }

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode에서는 XR UI Overlay 설정을 변경할 수 없습니다.");

        ValidateRenderPipeline();
        int overlayLayer = EnsureLayer(LayerName, PreferredLayer);
        int handLayer = EnsureLayer(HandLayerName, PreferredHandLayer);

        Scene scene = GetOrOpenTargetScene();
        Camera mainCamera = FindMainCamera(scene);
        UniversalAdditionalCameraData baseData = mainCamera.GetUniversalAdditionalCameraData();
        if (baseData == null)
            throw new InvalidOperationException("Main Camera에 UniversalAdditionalCameraData가 없습니다.");

        // cameraStack returns null when the active renderer does not support stacking.
        List<Camera> cameraStack = baseData.cameraStack;
        if (cameraStack == null)
            throw new InvalidOperationException(
                "현재 URP Renderer가 Camera Stacking을 지원하지 않아 설정을 중단했습니다.");

        List<Canvas> canvases = FindTargetCanvases(scene);
        List<GameObject> handVisuals = FindHandVisuals(scene);
        Camera overlayCamera = GetOrCreateOverlayCamera(mainCamera);
        ConfigureCameras(mainCamera, baseData, overlayCamera, overlayLayer, handLayer, cameraStack);
        ConfigureCanvases(canvases, overlayCamera, overlayLayer);
        ConfigureHandVisuals(handVisuals, handLayer);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("VR_Test Scene 저장에 실패했습니다.");

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[ROOM 404] XR UI/Hands Overlay 적용 완료: UI={LayerName}({overlayLayer}), " +
            $"Hands={HandLayerName}({handLayer}), Canvas={canvases.Count}, " +
            $"HandVisuals={handVisuals.Count}, Base={mainCamera.name}, Overlay={overlayCamera.name}");
    }

    private static Scene GetOrOpenTargetScene()
    {
        Scene loadedTarget = SceneManager.GetSceneByPath(ScenePath);
        if (loadedTarget.IsValid() && loadedTarget.isLoaded)
            return loadedTarget;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isDirty)
            {
                throw new InvalidOperationException(
                    $"'{loaded.path}'에 저장되지 않은 변경이 있어 VR_Test를 자동으로 열지 않았습니다.");
            }
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void ValidateRenderPipeline()
    {
        RenderPipelineAsset active = QualitySettings.renderPipeline != null
            ? QualitySettings.renderPipeline
            : GraphicsSettings.defaultRenderPipeline;

        if (active is not UniversalRenderPipelineAsset)
            throw new InvalidOperationException(
                "현재 활성 Render Pipeline이 URP가 아니므로 Camera Stacking을 적용하지 않았습니다.");
    }

    private static int EnsureLayer(string layerName, int preferredLayer)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0)
            return existing;

        UnityEngine.Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        SerializedObject serialized = new(tagManager);
        SerializedProperty layers = serialized.FindProperty("layers");
        if (layers == null || layers.arraySize <= preferredLayer)
            throw new InvalidOperationException("TagManager의 Layer 배열을 읽을 수 없습니다.");

        SerializedProperty preferred = layers.GetArrayElementAtIndex(preferredLayer);
        if (!string.IsNullOrEmpty(preferred.stringValue))
            throw new InvalidOperationException(
                $"Layer {preferredLayer}이 이미 '{preferred.stringValue}'로 사용 중입니다.");

        preferred.stringValue = layerName;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tagManager);
        AssetDatabase.SaveAssets();

        int created = LayerMask.NameToLayer(layerName);
        if (created != preferredLayer)
            throw new InvalidOperationException($"{layerName} Layer 생성 결과가 예상과 다릅니다: {created}");
        return created;
    }

    private static Camera FindMainCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (camera.CompareTag("MainCamera") || camera.name == "Main Camera")
                    return camera;
            }
        }
        throw new InvalidOperationException("VR_Test에서 Main Camera를 찾을 수 없습니다.");
    }

    private static List<Canvas> FindTargetCanvases(Scene scene)
    {
        List<Canvas> results = new();
        foreach (string canvasName in TargetCanvasNames)
        {
            Canvas found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.name == canvasName)
                    {
                        found = canvas;
                        break;
                    }
                }
                if (found != null)
                    break;
            }

            if (found == null)
                throw new InvalidOperationException($"대상 Canvas를 찾을 수 없습니다: {canvasName}");
            if (found.renderMode != RenderMode.WorldSpace)
                throw new InvalidOperationException($"{canvasName}이 World Space Canvas가 아닙니다.");
            results.Add(found);
        }
        return results;
    }

    private static List<GameObject> FindHandVisuals(Scene scene)
    {
        List<GameObject> results = new();
        foreach (string visualName in HandVisualNames)
        {
            GameObject found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (item.name == visualName)
                    {
                        found = item.gameObject;
                        break;
                    }
                }
                if (found != null)
                    break;
            }

            if (found == null)
                throw new InvalidOperationException($"손 Visual을 찾을 수 없습니다: {visualName}");
            results.Add(found);
        }
        return results;
    }

    private static Camera GetOrCreateOverlayCamera(Camera mainCamera)
    {
        Transform existing = mainCamera.transform.Find(OverlayCameraName);
        GameObject cameraObject;
        if (existing != null)
        {
            cameraObject = existing.gameObject;
        }
        else
        {
            cameraObject = new GameObject(OverlayCameraName);
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create XR UI Overlay Camera");
            cameraObject.transform.SetParent(mainCamera.transform, false);
        }

        cameraObject.layer = 0;
        cameraObject.tag = "Untagged";
        cameraObject.transform.localPosition = Vector3.zero;
        cameraObject.transform.localRotation = Quaternion.identity;
        cameraObject.transform.localScale = Vector3.one;

        Camera camera = cameraObject.GetComponent<Camera>();
        if (camera == null)
            camera = Undo.AddComponent<Camera>(cameraObject);
        if (cameraObject.GetComponent<UniversalAdditionalCameraData>() == null)
            Undo.AddComponent<UniversalAdditionalCameraData>(cameraObject);
        return camera;
    }

    private static void ConfigureCameras(
        Camera mainCamera,
        UniversalAdditionalCameraData baseData,
        Camera overlayCamera,
        int overlayLayer,
        int handLayer,
        List<Camera> cameraStack)
    {
        int overlayMask = 1 << overlayLayer;
        int handMask = 1 << handLayer;
        int foregroundMask = overlayMask | handMask;

        Undo.RecordObject(mainCamera, "Exclude XR UI Overlay Layer");
        Undo.RecordObject(baseData, "Configure XR Base Camera");
        Undo.RecordObject(overlayCamera, "Configure XR UI Overlay Camera");

        baseData.renderType = CameraRenderType.Base;
        mainCamera.cullingMask &= ~foregroundMask;

        overlayCamera.enabled = true;
        overlayCamera.clearFlags = CameraClearFlags.Depth;
        overlayCamera.backgroundColor = Color.clear;
        overlayCamera.cullingMask = foregroundMask;
        overlayCamera.nearClipPlane = mainCamera.nearClipPlane;
        overlayCamera.farClipPlane = mainCamera.farClipPlane;
        overlayCamera.fieldOfView = mainCamera.fieldOfView;
        overlayCamera.orthographic = mainCamera.orthographic;
        overlayCamera.orthographicSize = mainCamera.orthographicSize;
        overlayCamera.allowHDR = false;
        overlayCamera.allowMSAA = mainCamera.allowMSAA;
        overlayCamera.allowDynamicResolution = mainCamera.allowDynamicResolution;
        overlayCamera.useOcclusionCulling = false;
        overlayCamera.stereoTargetEye = StereoTargetEyeMask.Both;
        overlayCamera.depth = mainCamera.depth + 1f;

        UniversalAdditionalCameraData overlayData = overlayCamera.GetUniversalAdditionalCameraData();
        Undo.RecordObject(overlayData, "Configure XR UI Overlay Camera Data");
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderShadows = false;
        overlayData.renderPostProcessing = false;
        overlayData.requiresDepthOption = CameraOverrideOption.Off;
        overlayData.requiresColorOption = CameraOverrideOption.Off;
        overlayData.allowXRRendering = true;
        CopyRendererAndSetClearDepth(baseData, overlayData);

        for (int i = cameraStack.Count - 1; i >= 0; i--)
        {
            if (cameraStack[i] == null || cameraStack[i] == overlayCamera)
                cameraStack.RemoveAt(i);
        }
        cameraStack.Add(overlayCamera);

        EditorUtility.SetDirty(mainCamera);
        EditorUtility.SetDirty(baseData);
        EditorUtility.SetDirty(overlayCamera);
        EditorUtility.SetDirty(overlayData);
    }

    private static void CopyRendererAndSetClearDepth(
        UniversalAdditionalCameraData baseData,
        UniversalAdditionalCameraData overlayData)
    {
        // URP 17 exposes clearDepth as read-only and does not expose the renderer index.
        // These are serialized Inspector settings, so configure them through Unity's
        // SerializedObject API instead of relying on unavailable runtime setters.
        SerializedObject baseSerialized = new(baseData);
        SerializedObject overlaySerialized = new(overlayData);

        SerializedProperty baseRendererIndex = baseSerialized.FindProperty("m_RendererIndex");
        SerializedProperty overlayRendererIndex = overlaySerialized.FindProperty("m_RendererIndex");
        SerializedProperty clearDepth = overlaySerialized.FindProperty("m_ClearDepth");

        if (baseRendererIndex == null || overlayRendererIndex == null || clearDepth == null)
            throw new InvalidOperationException(
                "URP Camera Data의 Renderer 또는 Clear Depth 직렬화 필드를 찾을 수 없습니다.");

        overlayRendererIndex.intValue = baseRendererIndex.intValue;
        clearDepth.boolValue = true;
        overlaySerialized.ApplyModifiedProperties();
    }

    private static void ConfigureCanvases(List<Canvas> canvases, Camera overlayCamera, int overlayLayer)
    {
        foreach (Canvas canvas in canvases)
        {
            Undo.RecordObject(canvas, "Assign XR UI Overlay Camera");
            canvas.worldCamera = overlayCamera;
            EditorUtility.SetDirty(canvas);
            SetLayerRecursively(canvas.gameObject, overlayLayer);
        }
    }

    private static void ConfigureHandVisuals(List<GameObject> handVisuals, int handLayer)
    {
        foreach (GameObject handVisual in handVisuals)
            SetLayerRecursively(handVisual, handLayer);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            Undo.RecordObject(item.gameObject, "Apply XR UI Overlay Layer");
            item.gameObject.layer = layer;
            EditorUtility.SetDirty(item.gameObject);
        }
    }
}
