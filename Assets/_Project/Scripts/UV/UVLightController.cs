using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public sealed class UVLightController : MonoBehaviour
{
    [Header("XR Interaction")]
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Header("UV Emitter")]
    [Tooltip("Raycast origin. Its forward (blue Z axis) must point out of the flashlight lens.")]
    [SerializeField] private Transform emitterTransform;
    [SerializeField] private Light uvSpotLight;

    [Header("Detection")]
    [Min(0.1f)]
    [SerializeField] private float detectionDistance = 5f;
    [SerializeField] private LayerMask detectionMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("UV Light Appearance")]
    [SerializeField] private Color uvColor = new(0.45f, 0.1f, 1f, 1f);
    [Min(0f)]
    [SerializeField] private float lightIntensity = 4f;
    [Range(1f, 179f)]
    [SerializeField] private float spotAngle = 35f;

    private UVRevealable currentTarget;
    private Collider cachedHitCollider;
    private UVRevealable cachedRevealable;
    private bool isGrabbed;
    private bool isUVOn;

    public bool IsGrabbed => isGrabbed;
    public bool IsUVOn => isUVOn;
    public UVRevealable CurrentTarget => currentTarget;

    private void Awake()
    {
        ResolveReferences();
        ConfigureSpotLight();
        ApplyUVState(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        UnsubscribeFromGrabEvents();
        SubscribeToGrabEvents();
        ApplyUVState(false);
    }

    private void OnDisable()
    {
        UnsubscribeFromGrabEvents();
        isGrabbed = false;
        ApplyUVState(false);
    }

    private void Update()
    {
        if (isUVOn)
            PerformDetection();
    }

    public void SetUVOnForTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UV Light] UV 테스트는 Play Mode에서만 실행할 수 있습니다.", this);
            return;
        }

        ApplyUVState(true);
    }

    public void SetUVOffForTest()
    {
        ApplyUVState(false);
    }

    [ContextMenu("Debug / Toggle UV")]
    public void ToggleUVForTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UV Light] UV 테스트는 Play Mode에서만 실행할 수 있습니다.", this);
            return;
        }

        ApplyUVState(!isUVOn);
    }

    [ContextMenu("Debug / Detect Once")]
    public void PerformDetectionNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UV Light] 탐지 테스트는 Play Mode에서만 실행할 수 있습니다.", this);
            return;
        }

        if (isUVOn)
            PerformDetection();
    }

    private void HandleSelectEntered(SelectEnterEventArgs args)
    {
        isGrabbed = true;
    }

    private void HandleSelectExited(SelectExitEventArgs args)
    {
        isGrabbed = false;
        ApplyUVState(false);
    }

    private void HandleActivated(ActivateEventArgs args)
    {
        if (!isGrabbed)
            return;

        ApplyUVState(!isUVOn);
    }

    private void ApplyUVState(bool active)
    {
        isUVOn = active;

        if (uvSpotLight != null)
            uvSpotLight.enabled = active;

        if (!active)
        {
            ClearHitCache();
            ClearCurrentTarget();
        }
    }

    private void PerformDetection()
    {
        Transform emitter = emitterTransform != null ? emitterTransform : transform;

        bool hitSomething = Physics.Raycast(
            emitter.position,
            emitter.forward,
            out RaycastHit hit,
            detectionDistance,
            detectionMask,
            triggerInteraction);

        SetCurrentTarget(ResolveHitTarget(hitSomething ? hit.collider : null));
    }

    private UVRevealable ResolveHitTarget(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            ClearHitCache();
            return null;
        }

        // A real null is a cached miss; a destroyed Unity object needs a new lookup.
        if (cachedHitCollider != hitCollider ||
            (!ReferenceEquals(cachedRevealable, null) && cachedRevealable == null))
        {
            cachedHitCollider = hitCollider;
            cachedRevealable = hitCollider.GetComponentInParent<UVRevealable>();
        }

        return cachedRevealable != null && cachedRevealable.isActiveAndEnabled
            ? cachedRevealable
            : null;
    }

    private void ClearHitCache()
    {
        cachedHitCollider = null;
        cachedRevealable = null;
    }

    private void SetCurrentTarget(UVRevealable nextTarget)
    {
        if (currentTarget == nextTarget)
        {
            // OnDisable clears illumination even if the target is re-enabled between raycasts.
            currentTarget = nextTarget;
            if (currentTarget != null && !currentTarget.IsIlluminated)
                currentTarget.SetIlluminated(true);
            return;
        }

        if (currentTarget != null)
            currentTarget.SetIlluminated(false);

        currentTarget = nextTarget;

        if (currentTarget != null)
            currentTarget.SetIlluminated(true);
    }

    private void ClearCurrentTarget()
    {
        if (currentTarget != null)
            currentTarget.SetIlluminated(false);

        currentTarget = null;
    }

    private void ResolveReferences()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (emitterTransform == null)
            emitterTransform = transform;
    }

    private void ConfigureSpotLight()
    {
        if (uvSpotLight == null)
            return;

        uvSpotLight.type = LightType.Spot;
        uvSpotLight.color = uvColor;
        uvSpotLight.intensity = lightIntensity;
        uvSpotLight.range = detectionDistance;
        uvSpotLight.spotAngle = spotAngle;
    }

    private void SubscribeToGrabEvents()
    {
        if (grabInteractable == null)
        {
            Debug.LogError("[UV Light] XRGrabInteractable을 찾을 수 없습니다.", this);
            return;
        }

        grabInteractable.selectEntered.AddListener(HandleSelectEntered);
        grabInteractable.selectExited.AddListener(HandleSelectExited);
        grabInteractable.activated.AddListener(HandleActivated);
    }

    private void UnsubscribeFromGrabEvents()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
        grabInteractable.selectExited.RemoveListener(HandleSelectExited);
        grabInteractable.activated.RemoveListener(HandleActivated);
    }

    private void OnDrawGizmosSelected()
    {
        Transform emitter = emitterTransform != null ? emitterTransform : transform;
        Gizmos.color = isUVOn ? new Color(0.65f, 0.25f, 1f, 1f) : new Color(0.65f, 0.25f, 1f, 0.4f);
        Gizmos.DrawLine(emitter.position, emitter.position + emitter.forward * detectionDistance);
    }
}
