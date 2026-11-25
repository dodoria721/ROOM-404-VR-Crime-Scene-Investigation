using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Manual scene setup for rendering the in-game World Space UI through
/// an XR-compatible URP Overlay Camera.
/// </summary>
public static class XRUIOverlaySetupEditor
{
    private const string ScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string LayerName = "XR_UI_Overlay";
    private const int PreferredLayer = 8;
    private const string HandLayerName = "XR_Hands";
    private const int PreferredHandLayer = 9;
    private const string OverlayCameraName = "XRUIOverlayCamera";
    private const string HandOverlayCameraName = "XRHandsOverlayCamera";
    private const string RightUIRayName = "RightUIRay";
    private const string MaterialFolder = "Assets/_Project/Materials/UI";
    private const string UIOverlayMaterialPath = MaterialFolder + "/M_UI_AlwaysOnTop.mat";
    private const string TMPOverlayMaterialPath = MaterialFolder + "/M_NotoSansKR_Overlay.mat";
    private const string UIOverlayShaderName = "ROOM404/UI/Always On Top";
    private const string TMPOverlayShaderName = "TextMeshPro/Mobile/Distance Field Overlay";

    // These two full-screen graphics intentionally swallow pointer input only
    // while their modal root is active. All other non-Selectable graphics are
    // visual decoration and must not compete with Button target graphics.
    private static readonly HashSet<string> IntentionalModalBlockers = new()
    {
        "DeductionUnlockPrompt",
        "Dimmer"
    };

    // Kept only for validating the five original canvases in older scene versions.
    // Current setup discovers every root World Space Canvas automatically.
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

    [MenuItem("ROOM 404/UI/Apply Depth-Aware UI And Hands")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode에서는 XR UI Overlay 설정을 변경할 수 없습니다.");

        ValidateRenderPipeline();
        Scene scene = GetOrOpenTargetScene();
        int overlayLayer = EnsureLayer(LayerName, PreferredLayer);
        int handLayer = EnsureLayer(HandLayerName, PreferredHandLayer);

        Camera mainCamera = FindMainCamera(scene);
        UniversalAdditionalCameraData baseData = mainCamera.GetUniversalAdditionalCameraData();
        if (baseData == null)
            throw new InvalidOperationException("Main Camera에 UniversalAdditionalCameraData가 없습니다.");

        // cameraStack returns null when the active renderer does not support stacking.
        List<Camera> cameraStack = baseData.cameraStack;
        if (cameraStack == null)
            throw new InvalidOperationException(
                "현재 URP Renderer가 Camera Stacking을 지원하지 않아 설정을 중단했습니다.");

        List<Canvas> canvases = FindAllWorldSpaceCanvases(scene);
        List<GameObject> handVisuals = FindHandVisuals(scene);
        GameObject rightUiRay = FindNamedGameObject(scene, RightUIRayName);
        Material uiOverlayMaterial = GetOrCreateUIOverlayMaterial();
        Material tmpOverlayMaterial = GetOrCreateTMPOverlayMaterial(canvases);
        Camera overlayCamera = GetOrCreateOverlayCamera(mainCamera, OverlayCameraName);
        Camera handOverlayCamera = GetOrCreateOverlayCamera(mainCamera, HandOverlayCameraName);
        ConfigureCameras(
            mainCamera,
            baseData,
            overlayCamera,
            handOverlayCamera,
            overlayLayer,
            handLayer,
            cameraStack);
        ConfigureCanvases(
            canvases,
            overlayCamera,
            overlayLayer,
            uiOverlayMaterial,
            tmpOverlayMaterial);
        ConfigureHandVisuals(handVisuals, handLayer);
        ConfigureRightUIRay(rightUiRay, handLayer);
        int handMaterialCount = RestoreHandMaterialQueues(handVisuals);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("VR_Test Scene 저장에 실패했습니다.");

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[ROOM 404] XR UI/Hands Overlay 적용 완료: UI={LayerName}({overlayLayer}), " +
            $"Hands={HandLayerName}({handLayer}), Canvas={canvases.Count}, " +
            $"HandVisuals={handVisuals.Count}, RightUIRay={rightUiRay.name}, " +
            $"RuntimeReticleLayerSync=True, " +
            $"HandMaterials={handMaterialCount}, " +
            $"Base={mainCamera.name}, UIOverlay={overlayCamera.name}, " +
            $"HandsOverlay={handOverlayCamera.name}, PreserveWorldDepth=True");
    }

    private static Scene GetOrOpenTargetScene()
    {
        Scene loadedTarget = SceneManager.GetSceneByPath(ScenePath);
        if (loadedTarget.IsValid() && loadedTarget.isLoaded)
        {
            if (loadedTarget.isDirty)
                throw new InvalidOperationException(
                    $"'{loadedTarget.path}'에 저장되지 않은 변경이 있어 XR UI Overlay 적용을 중단했습니다.");
            return loadedTarget;
        }

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

    private static List<Canvas> FindAllWorldSpaceCanvases(Scene scene)
    {
        List<Canvas> results = new();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode == RenderMode.WorldSpace && canvas.rootCanvas == canvas)
                    results.Add(canvas);
            }
        }

        results.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        if (results.Count == 0)
            throw new InvalidOperationException("VR_Test scene has no root World Space Canvas.");

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

    private static GameObject FindNamedGameObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == objectName)
                    return item.gameObject;
            }
        }

        throw new InvalidOperationException($"VR_Test scene object was not found: {objectName}");
    }

    private static Camera GetOrCreateOverlayCamera(Camera mainCamera, string cameraName)
    {
        Transform existing = mainCamera.transform.Find(cameraName);
        GameObject cameraObject;
        if (existing != null)
        {
            cameraObject = existing.gameObject;
        }
        else
        {
            cameraObject = new GameObject(cameraName);
            Undo.RegisterCreatedObjectUndo(cameraObject, $"Create {cameraName}");
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
        Camera handOverlayCamera,
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
        Undo.RecordObject(handOverlayCamera, "Configure XR Hands Overlay Camera");

        baseData.renderType = CameraRenderType.Base;
        mainCamera.cullingMask &= ~foregroundMask;

        overlayCamera.enabled = true;
        overlayCamera.clearFlags = CameraClearFlags.Depth;
        overlayCamera.backgroundColor = Color.clear;
        ConfigureOverlayCameraComponent(overlayCamera, mainCamera, overlayMask, mainCamera.depth + 1f);
        ConfigureOverlayCameraComponent(handOverlayCamera, mainCamera, handMask, mainCamera.depth + 2f);

        UniversalAdditionalCameraData overlayData = overlayCamera.GetUniversalAdditionalCameraData();
        Undo.RecordObject(overlayData, "Configure XR UI Overlay Camera Data");
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderShadows = false;
        overlayData.renderPostProcessing = false;
        overlayData.requiresDepthOption = CameraOverrideOption.Off;
        overlayData.requiresColorOption = CameraOverrideOption.Off;
        overlayData.allowXRRendering = true;
        CopyRendererAndSetClearDepth(baseData, overlayData, false);

        UniversalAdditionalCameraData handOverlayData = handOverlayCamera.GetUniversalAdditionalCameraData();
        Undo.RecordObject(handOverlayData, "Configure XR Hands Overlay Camera Data");
        handOverlayData.renderType = CameraRenderType.Overlay;
        handOverlayData.renderShadows = false;
        handOverlayData.renderPostProcessing = false;
        handOverlayData.requiresDepthOption = CameraOverrideOption.Off;
        handOverlayData.requiresColorOption = CameraOverrideOption.Off;
        handOverlayData.allowXRRendering = true;
        CopyRendererAndSetClearDepth(baseData, handOverlayData, false);

        for (int i = cameraStack.Count - 1; i >= 0; i--)
        {
            if (cameraStack[i] == null ||
                cameraStack[i] == overlayCamera ||
                cameraStack[i] == handOverlayCamera)
                cameraStack.RemoveAt(i);
        }
        // The camera order is intentional. UI ignores world depth but does not write
        // depth; hands are drawn last and therefore appear over UI while still being
        // rejected by the depth previously written by tables, walls and props.
        cameraStack.Add(overlayCamera);
        cameraStack.Add(handOverlayCamera);

        EditorUtility.SetDirty(mainCamera);
        EditorUtility.SetDirty(baseData);
        EditorUtility.SetDirty(overlayCamera);
        EditorUtility.SetDirty(overlayData);
        EditorUtility.SetDirty(handOverlayCamera);
        EditorUtility.SetDirty(handOverlayData);
    }

    private static void ConfigureOverlayCameraComponent(
        Camera overlayCamera,
        Camera mainCamera,
        int cullingMask,
        float depth)
    {
        overlayCamera.enabled = true;
        overlayCamera.clearFlags = CameraClearFlags.Depth;
        overlayCamera.backgroundColor = Color.clear;
        overlayCamera.cullingMask = cullingMask;
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
        overlayCamera.depth = depth;
    }

    private static void CopyRendererAndSetClearDepth(
        UniversalAdditionalCameraData baseData,
        UniversalAdditionalCameraData overlayData,
        bool shouldClearDepth)
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
        clearDepth.boolValue = shouldClearDepth;
        overlaySerialized.ApplyModifiedProperties();
    }

    private static void ConfigureCanvases(
        List<Canvas> canvases,
        Camera overlayCamera,
        int overlayLayer,
        Material uiOverlayMaterial,
        Material tmpOverlayMaterial)
    {
        foreach (Canvas canvas in canvases)
        {
            Undo.RecordObject(canvas, "Assign XR UI Overlay Camera");
            canvas.worldCamera = overlayCamera;
            EditorUtility.SetDirty(canvas);
            SetLayerRecursively(canvas.gameObject, overlayLayer);
            NormalizeCanvasRaycaster(canvas);

            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                Undo.RecordObject(graphic, "Apply Depth-Aware XR UI Material");

                if (graphic is TMP_Text tmpText)
                    tmpText.fontSharedMaterial = tmpOverlayMaterial;
                else
                    graphic.material = uiOverlayMaterial;

                EditorUtility.SetDirty(graphic);
            }
        }
    }

    private static void NormalizeCanvasRaycaster(Canvas canvas)
    {
        Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(true);
        bool isInteractive = selectables.Length > 0;

        // A standard GraphicRaycaster does not process tracked-device pointer
        // events. When both raycaster types are active it can also create
        // duplicate or inconsistent hits, so disable it for VR World Space UI.
        foreach (GraphicRaycaster legacyRaycaster in canvas.GetComponents<GraphicRaycaster>())
        {
            Undo.RecordObject(legacyRaycaster, "Disable Legacy UI Raycaster");
            legacyRaycaster.enabled = false;
            EditorUtility.SetDirty(legacyRaycaster);
        }

        TrackedDeviceGraphicRaycaster[] trackedRaycasters =
            canvas.GetComponents<TrackedDeviceGraphicRaycaster>();
        TrackedDeviceGraphicRaycaster activeRaycaster = null;

        if (isInteractive)
        {
            activeRaycaster = trackedRaycasters.Length > 0
                ? trackedRaycasters[0]
                : Undo.AddComponent<TrackedDeviceGraphicRaycaster>(canvas.gameObject);

            Undo.RecordObject(activeRaycaster, "Configure XR UI Raycaster");
            activeRaycaster.enabled = true;
            activeRaycaster.ignoreReversedGraphics = false;
            activeRaycaster.checkFor2DOcclusion = false;
            activeRaycaster.checkFor3DOcclusion = false;
            activeRaycaster.raycastTriggerInteraction = QueryTriggerInteraction.Ignore;
            EditorUtility.SetDirty(activeRaycaster);
        }

        foreach (TrackedDeviceGraphicRaycaster raycaster in trackedRaycasters)
        {
            if (raycaster == activeRaycaster)
                continue;

            Undo.RecordObject(raycaster, "Disable Extra XR UI Raycaster");
            raycaster.enabled = false;
            EditorUtility.SetDirty(raycaster);
        }

        HashSet<Graphic> selectableTargets = new();
        foreach (Selectable selectable in selectables)
        {
            if (selectable.targetGraphic == null)
                continue;

            selectableTargets.Add(selectable.targetGraphic);
            Undo.RecordObject(selectable.targetGraphic, "Enable Selectable Raycast Target");
            selectable.targetGraphic.raycastTarget = true;
            EditorUtility.SetDirty(selectable.targetGraphic);
        }

        // TMP labels, icons, panel backgrounds, and decorative images should
        // never win the tracked-device raycast over their Button. Retain only
        // Selectable target graphics and the explicit full-screen modal blockers.
        foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
        {
            bool shouldReceiveRaycasts = selectableTargets.Contains(graphic) ||
                                         IntentionalModalBlockers.Contains(graphic.gameObject.name);
            if (graphic.raycastTarget == shouldReceiveRaycasts)
                continue;

            Undo.RecordObject(graphic, "Normalize XR UI Raycast Target");
            graphic.raycastTarget = shouldReceiveRaycasts;
            EditorUtility.SetDirty(graphic);
        }
    }

    private static void ConfigureHandVisuals(List<GameObject> handVisuals, int handLayer)
    {
        foreach (GameObject handVisual in handVisuals)
            SetLayerRecursively(handVisual, handLayer);
    }

    private static void ConfigureRightUIRay(GameObject rightUiRay, int foregroundLayer)
    {
        SetLayerRecursively(rightUiRay, foregroundLayer);

        if (rightUiRay.GetComponent<XROverlayVisualLayer>() == null)
            Undo.AddComponent<XROverlayVisualLayer>(rightUiRay);

        EditorUtility.SetDirty(rightUiRay);
    }

    private static int RestoreHandMaterialQueues(List<GameObject> handVisuals)
    {
        HashSet<Material> materials = new();
        foreach (GameObject handVisual in handVisuals)
        {
            foreach (Renderer renderer in handVisual.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                        materials.Add(material);
                }
            }
        }

        foreach (Material material in materials)
        {
            // Camera stacking now defines the ordering. Keep the normal URP/Lit
            // opaque queue so the hand shader retains standard depth behaviour.
            if (material.renderQueue == 4000)
            {
                Undo.RecordObject(material, "Restore XR Hand Material Queue");
                material.renderQueue = -1;
                EditorUtility.SetDirty(material);
            }
        }

        return materials.Count;
    }

    private static Material GetOrCreateUIOverlayMaterial()
    {
        EnsureMaterialFolder();

        Shader shader = Shader.Find(UIOverlayShaderName);
        if (shader == null)
            throw new InvalidOperationException($"UI Overlay Shader를 찾을 수 없습니다: {UIOverlayShaderName}");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(UIOverlayMaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "M_UI_AlwaysOnTop"
            };
            AssetDatabase.CreateAsset(material, UIOverlayMaterialPath);
        }
        else if (material.shader != shader)
        {
            Undo.RecordObject(material, "Update XR UI Overlay Material Shader");
            material.shader = shader;
        }

        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateTMPOverlayMaterial(List<Canvas> canvases)
    {
        EnsureMaterialFolder();

        Shader shader = Shader.Find(TMPOverlayShaderName);
        if (shader == null)
            throw new InvalidOperationException($"TMP Overlay Shader를 찾을 수 없습니다: {TMPOverlayShaderName}");

        Material source = null;
        foreach (Canvas canvas in canvases)
        {
            TMP_Text text = canvas.GetComponentInChildren<TMP_Text>(true);
            if (text != null && text.fontSharedMaterial != null)
            {
                source = text.fontSharedMaterial;
                break;
            }
        }

        if (source == null)
            throw new InvalidOperationException("대상 Canvas에서 TMP Font Material을 찾을 수 없습니다.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TMPOverlayMaterialPath);
        if (material == null)
        {
            material = new Material(source)
            {
                name = "M_NotoSansKR_Overlay",
                shader = shader
            };
            AssetDatabase.CreateAsset(material, TMPOverlayMaterialPath);
        }
        else
        {
            Undo.RecordObject(material, "Update TMP Overlay Material");
            material.CopyPropertiesFromMaterial(source);
            material.shader = shader;
        }

        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureMaterialFolder()
    {
        const string parent = "Assets/_Project/Materials";
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder(parent, "UI");
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
