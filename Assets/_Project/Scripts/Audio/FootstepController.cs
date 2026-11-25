
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FootstepController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public AudioClip[] footstepClips;

    [Header("Footstep Settings")]
    public float stepDistance = 0.55f;
    public float minMoveSpeed = 0.1f;
    public float teleportThreshold = 0.5f;

    [Header("Pitch Settings")]
    public float minPitch = 0.95f;
    public float maxPitch = 1.05f;

    private AudioSource audioSource;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float distanceTraveled;
    private int lastClipIndex = -1;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (player == null)
            player = transform.parent;
    }

    private void OnEnable()
    {
        ResetTracking();
    }

    public void ResetTracking()
    {
        if (player == null)
            return;

        lastPosition = player.position;
        lastRotation = player.rotation;
        distanceTraveled = 0f;
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        if (player == null || deltaTime <= 0f)
            return;

        player.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
        Vector3 movement = position - lastPosition;
        Quaternion previousRotation = lastRotation;

        lastPosition = position;
        lastRotation = rotation;

        // 순간이동 및 Snap Turn 감지
        float distance = movement.magnitude;
        if (distance > teleportThreshold ||
            Quaternion.Angle(previousRotation, rotation) > 10f)
        {
            distanceTraveled = 0f;
            return;
        }

        // 수직 이동이 없으면 순간이동 검사에 사용한 거리를 재사용한다.
        if (movement.y != 0f)
        {
            movement.y = 0f;
            distance = movement.magnitude;
        }

        float speed = distance / deltaTime;

        // 정지 상태에서는 발소리 재생하지 않음
        if (speed < minMoveSpeed)
        {
            distanceTraveled = 0f;
            return;
        }

        distanceTraveled += distance;

        if (distanceTraveled >= stepDistance)
        {
            PlayFootstep();
            distanceTraveled -= stepDistance;
        }
    }

    private void PlayFootstep()
    {
        if (footstepClips == null ||
            footstepClips.Length == 0)
            return;

        int index = Random.Range(0, footstepClips.Length);

        // 동일한 발소리 연속 재생 방지
        if (footstepClips.Length > 1)
        {
            while (index == lastClipIndex)
            {
                index = Random.Range(
                    0, footstepClips.Length);
            }
        }

        if (footstepClips[index] == null)
            return;

        audioSource.pitch = Random.Range(
            minPitch, maxPitch);

        audioSource.PlayOneShot(footstepClips[index]);

        lastClipIndex = index;
    }
}
