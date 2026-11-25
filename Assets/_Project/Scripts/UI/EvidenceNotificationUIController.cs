using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EvidenceNotificationUIController : MonoBehaviour
{
    private const string NotificationTitle = "증거 확보 완료";
    private const string NotificationMessage = "수집한 증거가 수사 기록에 기록되었습니다.";
    private const int RequiredEvidenceTarget = 4;

    [Header("UI References")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TMP_Text notificationTitleText;
    [SerializeField] private TMP_Text evidenceIdText;
    [SerializeField] private TMP_Text evidenceNameText;
    [SerializeField] private TMP_Text notificationMessageText;
    [SerializeField] private TMP_Text requiredProgressText;
    [SerializeField] private Image requiredProgressFill;

    [Header("Notification Settings")]
    [Min(0.1f)]
    [SerializeField] private float displayDuration = 2.5f;

    [Header("World Space Safety")]
    [SerializeField] private bool applySafeWorldSpaceDefaults = true;
    [SerializeField] private Vector3 safeLocalPosition = new(0f, 0.25f, 1.1f);
    [SerializeField] private Vector3 safeLocalScale = new(0.0012f, 0.0012f, 0.0012f);

    private readonly Queue<EvidenceData> pendingNotifications = new();

    private EvidenceManager subscribedManager;
    private Coroutine subscriptionCoroutine;
    private Coroutine notificationCoroutine;

    private void Awake()
    {
        ApplyWorldSpaceSafety();
        SetStaticText();
        SetPanelVisible(false);
    }

    private void OnEnable()
    {
        SetPanelVisible(false);

        if (!TrySubscribeToManager())
            subscriptionCoroutine = StartCoroutine(WaitForEvidenceManager());
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private bool TrySubscribeToManager()
    {
        EvidenceManager manager = EvidenceManager.Instance;
        if (manager == null)
            return false;

        if (subscribedManager == manager)
            return true;

        UnsubscribeFromManager();
        subscribedManager = manager;
        subscribedManager.EvidenceRegistered += OnEvidenceRegistered;
        return true;
    }

    private IEnumerator WaitForEvidenceManager()
    {
        while (isActiveAndEnabled && EvidenceManager.Instance == null)
            yield return null;

        subscriptionCoroutine = null;

        if (isActiveAndEnabled)
            TrySubscribeToManager();
    }

    private void OnEvidenceRegistered(EvidenceData evidence)
    {
        if (evidence == null)
            return;

        pendingNotifications.Enqueue(evidence);

        if (notificationCoroutine == null)
            notificationCoroutine = StartCoroutine(ProcessNotificationQueue());
    }

    private IEnumerator ProcessNotificationQueue()
    {
        while (pendingNotifications.Count > 0)
        {
            EvidenceData evidence = pendingNotifications.Dequeue();
            ShowNotification(evidence);

            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, displayDuration));

            SetPanelVisible(false);

            if (pendingNotifications.Count > 0)
                yield return null;
        }

        notificationCoroutine = null;
    }

    private void ShowNotification(EvidenceData evidence)
    {
        if (evidenceIdText != null)
            evidenceIdText.text = $"{evidence.Id}  ·";

        if (evidenceNameText != null)
            evidenceNameText.text = evidence.EvidenceName;

        EvidenceManager manager = EvidenceManager.Instance;
        int requiredCount = manager != null ? manager.RequiredCount : 0;

        if (requiredProgressText != null)
            requiredProgressText.SetText("{0} / {1}", requiredCount, RequiredEvidenceTarget);

        if (requiredProgressFill != null)
            requiredProgressFill.fillAmount = Mathf.Clamp01(
                requiredCount / (float)RequiredEvidenceTarget);

        SetPanelVisible(true);
    }

    private void SetStaticText()
    {
        if (notificationTitleText != null)
            notificationTitleText.text = NotificationTitle;

        if (notificationMessageText != null)
            notificationMessageText.text = NotificationMessage;
    }

    private void SetPanelVisible(bool visible)
    {
        if (notificationPanel != null && notificationPanel.activeSelf != visible)
            notificationPanel.SetActive(visible);
    }

    private void ApplyWorldSpaceSafety()
    {
        if (!applySafeWorldSpaceDefaults)
            return;

        Canvas notificationCanvas = GetComponent<Canvas>();
        Camera parentCamera = GetComponentInParent<Camera>();

        if (notificationCanvas == null)
        {
            Debug.LogWarning("[Evidence Notification] Canvas 컴포넌트를 찾을 수 없습니다.", this);
            return;
        }

        notificationCanvas.renderMode = RenderMode.WorldSpace;

        if (parentCamera != null)
        {
            notificationCanvas.worldCamera = parentCamera;
            transform.localPosition = safeLocalPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = safeLocalScale;
        }
        else
        {
            Debug.LogWarning(
                "[Evidence Notification] 부모 Camera를 찾을 수 없습니다. Canvas를 Main Camera의 자식으로 배치하세요.",
                this);
        }

        if (notificationPanel != null)
        {
            if (notificationPanel.transform is RectTransform panelRect)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                panelRect.anchoredPosition3D = Vector3.zero;
                panelRect.localRotation = Quaternion.identity;
                panelRect.localScale = Vector3.one;
            }

            Image background = notificationPanel.GetComponent<Image>();
            if (background != null)
            {
                Color color = background.color;
                color.a = Mathf.Max(color.a, 0.9f);
                background.color = color;
                background.raycastTarget = false;
            }
        }

        SetTextVisible(notificationTitleText);
        SetTextVisible(evidenceIdText);
        SetTextVisible(evidenceNameText);
        SetTextVisible(notificationMessageText);
        SetTextVisible(requiredProgressText);
    }

    private static void SetTextVisible(TMP_Text text)
    {
        if (text == null)
            return;

        Color color = text.color;
        color.a = 1f;
        text.color = color;
        text.raycastTarget = false;

        if (text.rectTransform != null)
        {
            Vector3 anchoredPosition = text.rectTransform.anchoredPosition3D;
            anchoredPosition.z = 0f;
            text.rectTransform.anchoredPosition3D = anchoredPosition;
            text.rectTransform.localRotation = Quaternion.identity;
            text.rectTransform.localScale = Vector3.one;
        }
    }

    private void Cleanup()
    {
        UnsubscribeFromManager();

        if (subscriptionCoroutine != null)
        {
            StopCoroutine(subscriptionCoroutine);
            subscriptionCoroutine = null;
        }

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
            notificationCoroutine = null;
        }

        pendingNotifications.Clear();
        SetPanelVisible(false);
    }

    private void UnsubscribeFromManager()
    {
        if (subscribedManager != null)
            subscribedManager.EvidenceRegistered -= OnEvidenceRegistered;

        subscribedManager = null;
    }
}
