using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot editor task that transfers environmental lighting from lab_001
/// to VR_Test without touching gameplay lights such as the UV flashlight.
/// </summary>
public static class LabLightingToVRTestEditor
{
    private const string SourceScenePath = "Assets/_Project/Scenes/lab_001.unity";
    private const string TargetScenePath = "Assets/_Project/Scenes/VR_Test.unity";
    private const string SessionKey = "ROOM404.LabLightingToVRTest.Applied";

    [InitializeOnLoadMethod]
    private static void ApplyOnceAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(SessionKey, false))
                return;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (loadedScene.isDirty)
                {
                    Debug.LogWarning(
                        $"[ROOM 404] 저장되지 않은 Scene 변경이 있어 조명 자동 적용을 보류했습니다: {loadedScene.path}");
                    return;
                }
            }

            try
            {
                Apply();
                SessionState.SetBool(SessionKey, true);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ROOM 404] lab_001 조명 적용 실패: {exception.Message}\n{exception}");
            }
        };
    }

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode에서는 조명 설정을 복사할 수 없습니다.");

        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        Scene targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(sourceScene);
        RenderSettingsSnapshot renderSettings = RenderSettingsSnapshot.Capture();
        List<Light> sourceLights = FindComponentsInScene<Light>(sourceScene);
        Volume sourceVolume = FindNamedComponent<Volume>(sourceScene, "Global Volume");

        SceneManager.SetActiveScene(targetScene);
        renderSettings.Apply();
        TransferLights(sourceLights, targetScene);
        TransferGlobalVolume(sourceVolume, targetScene);

        EditorSceneManager.MarkSceneDirty(targetScene);
        if (!EditorSceneManager.SaveScene(targetScene))
            throw new InvalidOperationException("VR_Test Scene 저장에 실패했습니다.");

        EditorSceneManager.CloseScene(sourceScene, true);
        SceneManager.SetActiveScene(targetScene);

        Debug.Log(
            $"[ROOM 404] lab_001 조명 적용 완료: " +
            $"Lights={sourceLights.Count}, GlobalVolume={(sourceVolume != null ? "Copied" : "Not Found")}");
    }

    private static void TransferLights(List<Light> sourceLights, Scene targetScene)
    {
        foreach (Light sourceLight in sourceLights)
        {
            string hierarchyPath = GetHierarchyPath(sourceLight.transform);
            Light targetLight = FindComponentByPath<Light>(targetScene, hierarchyPath);

            if (targetLight == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(sourceLight.gameObject);
                clone.name = sourceLight.gameObject.name;
                SceneManager.MoveGameObjectToScene(clone, targetScene);
                targetLight = clone.GetComponent<Light>();
            }
            else
            {
                EditorUtility.CopySerialized(sourceLight, targetLight);
                CopyAdditionalLightData(sourceLight, targetLight);
                CopyTransform(sourceLight.transform, targetLight.transform);
                targetLight.gameObject.SetActive(sourceLight.gameObject.activeSelf);
            }

            EditorUtility.SetDirty(targetLight.gameObject);
            EditorUtility.SetDirty(targetLight);
        }
    }

    private static void CopyAdditionalLightData(Light sourceLight, Light targetLight)
    {
        UniversalAdditionalLightData sourceData = sourceLight.GetComponent<UniversalAdditionalLightData>();
        UniversalAdditionalLightData targetData = targetLight.GetComponent<UniversalAdditionalLightData>();

        if (sourceData == null)
            return;

        if (targetData == null)
            targetData = targetLight.gameObject.AddComponent<UniversalAdditionalLightData>();

        EditorUtility.CopySerialized(sourceData, targetData);
        EditorUtility.SetDirty(targetData);
    }

    private static void TransferGlobalVolume(Volume sourceVolume, Scene targetScene)
    {
        if (sourceVolume == null)
            return;

        Volume targetVolume = FindNamedComponent<Volume>(targetScene, sourceVolume.gameObject.name);
        if (targetVolume == null)
        {
            GameObject clone = UnityEngine.Object.Instantiate(sourceVolume.gameObject);
            clone.name = sourceVolume.gameObject.name;
            SceneManager.MoveGameObjectToScene(clone, targetScene);
            return;
        }

        EditorUtility.CopySerialized(sourceVolume, targetVolume);
        CopyTransform(sourceVolume.transform, targetVolume.transform);
        targetVolume.gameObject.SetActive(sourceVolume.gameObject.activeSelf);
        EditorUtility.SetDirty(targetVolume);
        EditorUtility.SetDirty(targetVolume.gameObject);
    }

    private static void CopyTransform(Transform source, Transform target)
    {
        target.position = source.position;
        target.rotation = source.rotation;
        target.localScale = source.lossyScale;
    }

    private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
    {
        List<T> results = new();
        foreach (GameObject root in scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<T>(true));
        return results;
    }

    private static T FindNamedComponent<T>(Scene scene, string objectName) where T : Component
    {
        foreach (T component in FindComponentsInScene<T>(scene))
        {
            if (component.gameObject.name == objectName)
                return component;
        }
        return null;
    }

    private static T FindComponentByPath<T>(Scene scene, string path) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != path.Split('/')[0])
                continue;

            Transform current = root.transform;
            string[] parts = path.Split('/');
            for (int i = 1; i < parts.Length && current != null; i++)
                current = current.Find(parts[i]);

            if (current != null)
                return current.GetComponent<T>();
        }
        return null;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    private readonly struct RenderSettingsSnapshot
    {
        private readonly bool fog;
        private readonly Color fogColor;
        private readonly FogMode fogMode;
        private readonly float fogDensity;
        private readonly float fogStart;
        private readonly float fogEnd;
        private readonly AmbientMode ambientMode;
        private readonly Color ambientSky;
        private readonly Color ambientEquator;
        private readonly Color ambientGround;
        private readonly float ambientIntensity;
        private readonly Color subtractiveShadowColor;
        private readonly Material skybox;
        private readonly float haloStrength;
        private readonly float flareStrength;
        private readonly float flareFadeSpeed;
        private readonly DefaultReflectionMode reflectionMode;
        private readonly int reflectionResolution;
        private readonly int reflectionBounces;
        private readonly float reflectionIntensity;
        private readonly Cubemap customReflection;

        private RenderSettingsSnapshot(bool capture)
        {
            fog = RenderSettings.fog;
            fogColor = RenderSettings.fogColor;
            fogMode = RenderSettings.fogMode;
            fogDensity = RenderSettings.fogDensity;
            fogStart = RenderSettings.fogStartDistance;
            fogEnd = RenderSettings.fogEndDistance;
            ambientMode = RenderSettings.ambientMode;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;
            ambientIntensity = RenderSettings.ambientIntensity;
            subtractiveShadowColor = RenderSettings.subtractiveShadowColor;
            skybox = RenderSettings.skybox;
            haloStrength = RenderSettings.haloStrength;
            flareStrength = RenderSettings.flareStrength;
            flareFadeSpeed = RenderSettings.flareFadeSpeed;
            reflectionMode = RenderSettings.defaultReflectionMode;
            reflectionResolution = RenderSettings.defaultReflectionResolution;
            reflectionBounces = RenderSettings.reflectionBounces;
            reflectionIntensity = RenderSettings.reflectionIntensity;
            customReflection = RenderSettings.customReflectionTexture as Cubemap;
        }

        public static RenderSettingsSnapshot Capture() => new(true);

        public void Apply()
        {
            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.subtractiveShadowColor = subtractiveShadowColor;
            RenderSettings.skybox = skybox;
            RenderSettings.haloStrength = haloStrength;
            RenderSettings.flareStrength = flareStrength;
            RenderSettings.flareFadeSpeed = flareFadeSpeed;
            RenderSettings.defaultReflectionMode = reflectionMode;
            RenderSettings.defaultReflectionResolution = reflectionResolution;
            RenderSettings.reflectionBounces = reflectionBounces;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.customReflectionTexture = customReflection;
        }
    }
}
