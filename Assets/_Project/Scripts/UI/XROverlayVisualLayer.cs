using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Keeps any child visuals created under an XR ray on the foreground rendering
/// layer. The XRI line reticle is configured separately because XRI instantiates
/// that prefab as a scene root rather than as a child of the ray.
/// </summary>
[DisallowMultipleComponent]
public sealed class XROverlayVisualLayer : MonoBehaviour
{
    [SerializeField] private string overlayLayerName = "XR_Hands";

    private int overlayLayer = -1;
    private XRInteractorLineVisual lineVisual;
    private GameObject trackedReticle;

#if UNITY_EDITOR
    [SerializeField]
    [Tooltip("Play Mode에서 최상위 UI Hit 또는 UI Press 상태가 바뀔 때만 진단 로그를 출력합니다.")]
    private bool monitorUIInteraction;

    private XRRayInteractor rayInteractor;
    private GameObject previousUIHit;
    private bool previousUIPress;
    private bool diagnosticInitialized;
#endif

    private void Awake()
    {
        lineVisual = GetComponent<XRInteractorLineVisual>();
#if UNITY_EDITOR
        rayInteractor = GetComponent<XRRayInteractor>();
#endif
    }

    private void OnEnable()
    {
        trackedReticle = null;
        ResolveLayerAndApply();
    }

    private void OnTransformChildrenChanged()
    {
        ApplyRecursively();
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR
        MonitorUIInteraction();
#endif

        if (overlayLayer < 0 || lineVisual == null)
            return;

        GameObject reticle = lineVisual.reticle;
        if (reticle == null)
        {
            trackedReticle = null;
            return;
        }

        if (reticle != trackedReticle || reticle.layer != overlayLayer)
        {
            trackedReticle = reticle;
            ApplyLayerToHierarchy(reticle.transform);
        }
    }

    private void ResolveLayerAndApply()
    {
        overlayLayer = LayerMask.NameToLayer(overlayLayerName);
        if (overlayLayer < 0)
        {
            Debug.LogError($"[XR UI Ray] Rendering layer '{overlayLayerName}' does not exist.", this);
            return;
        }

        ApplyRecursively();
    }

    private void ApplyRecursively()
    {
        if (overlayLayer < 0)
            return;

        ApplyLayerToHierarchy(transform);
    }

    private void ApplyLayerToHierarchy(Transform root)
    {
        GameObject visual = root.gameObject;
        if (visual.layer != overlayLayer)
            visual.layer = overlayLayer;

        for (int i = 0; i < root.childCount; i++)
            ApplyLayerToHierarchy(root.GetChild(i));
    }

#if UNITY_EDITOR
    private void MonitorUIInteraction()
    {
        if (!monitorUIInteraction || rayInteractor == null)
            return;

        GameObject currentHit = rayInteractor.TryGetCurrentUIRaycastResult(out var result)
            ? result.gameObject
            : null;
        bool currentPress = rayInteractor.uiPressInput.ReadIsPerformed();

        if (!diagnosticInitialized || currentHit != previousUIHit || currentPress != previousUIPress)
        {
            diagnosticInitialized = true;
            previousUIHit = currentHit;
            previousUIPress = currentPress;
            LogUISnapshot(currentHit, currentPress);
        }
    }

    [ContextMenu("Debug / Log Current UI Raycast")]
    private void LogCurrentUIRaycast()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[XR UI Diagnostic] Play Mode에서 실행해야 현재 UI Raycast를 확인할 수 있습니다.", this);
            return;
        }

        if (rayInteractor == null)
            rayInteractor = GetComponent<XRRayInteractor>();

        GameObject currentHit = rayInteractor.TryGetCurrentUIRaycastResult(out var result)
            ? result.gameObject
            : null;
        LogUISnapshot(currentHit, rayInteractor.uiPressInput.ReadIsPerformed());
    }

    private void LogUISnapshot(GameObject hit, bool press)
    {
        if (hit == null)
        {
            Debug.Log($"[XR UI Diagnostic] TopHit=<none>, UI Press={press}", this);
            return;
        }

        Canvas canvas = hit.GetComponentInParent<Canvas>();
        Button button = hit.GetComponentInParent<Button>();
        string layerName = LayerMask.LayerToName(hit.layer);
        string canvasName = canvas != null ? canvas.name : "<none>";
        string buttonName = button != null ? button.name : "<none>";

        Debug.Log(
            $"[XR UI Diagnostic] TopHit={GetHierarchyPath(hit.transform)}, " +
            $"Canvas={canvasName}, Layer={layerName}({hit.layer}), " +
            $"Button={buttonName}, UI Press={press}",
            hit);
    }

    private static string GetHierarchyPath(Transform item)
    {
        string path = item.name;
        while (item.parent != null)
        {
            item = item.parent;
            path = item.name + "/" + path;
        }

        return path;
    }
#endif
}
