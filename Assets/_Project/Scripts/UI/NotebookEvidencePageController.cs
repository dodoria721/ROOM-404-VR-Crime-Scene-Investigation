using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NotebookEvidencePageController : MonoBehaviour
{
    private static readonly string[] EvidenceIds =
    {
        "E01", "E02", "E03", "E04", "E05", "E06"
    };

    [SerializeField] private TMP_Text collectedCountText;
    [SerializeField] private TMP_Text requiredCountText;
    [SerializeField] private GameObject[] collectedVisuals;
    [SerializeField] private GameObject[] lockedVisuals;
    [SerializeField] private TMP_Text[] evidenceNameTexts;

    [Header("Evidence Detail")]
    [SerializeField] private GameObject evidenceGrid;
    [SerializeField] private GameObject evidenceDetailPanel;
    [SerializeField] private GameObject pageNavigationRoot;
    [SerializeField] private Button[] evidenceCardButtons;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text detailHeaderText;
    [SerializeField] private TMP_Text detailEvidenceIdText;
    [SerializeField] private TMP_Text detailMetadataIdText;
    [SerializeField] private TMP_Text detailEvidenceNameText;
    [SerializeField] private TMP_Text detailEvidenceTypeText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_Text detailStatusText;
    [SerializeField] private TMP_Text detailMetadataStatusText;
    [SerializeField] private RawImage detailEvidenceImage;
    [SerializeField] private GameObject detailFallbackIcon;
    [SerializeField] private Texture[] evidenceImages;

    private EvidenceManager subscribedManager;
    private bool buttonListenersRegistered;

    private void OnEnable()
    {
        RegisterButtonListeners();
        Subscribe();
        ShowEvidenceGrid();
        Refresh();
    }

    private void OnDisable()
    {
        ShowEvidenceGrid();
        UnregisterButtonListeners();
        Unsubscribe();
    }

    public void Refresh()
    {
        EvidenceManager manager = EvidenceManager.Instance;
        int collectedCount = manager != null ? manager.CollectedCount : 0;
        int requiredCount = manager != null ? manager.RequiredCount : 0;

        if (collectedCountText != null)
            collectedCountText.text = $"{collectedCount} / 6";

        if (requiredCountText != null)
            requiredCountText.text = $"{requiredCount} / 4";

        for (int i = 0; i < EvidenceIds.Length; i++)
        {
            EvidenceData data = FindCollectedEvidence(manager, EvidenceIds[i]);
            bool collected = data != null;

            SetActive(collectedVisuals, i, collected);
            SetActive(lockedVisuals, i, !collected);
            SetCardInteractable(i, collected);

            if (collected && evidenceNameTexts != null && i < evidenceNameTexts.Length &&
                evidenceNameTexts[i] != null)
            {
                evidenceNameTexts[i].text = data.EvidenceName;
            }
        }
    }

    private void Subscribe()
    {
        EvidenceManager manager = EvidenceManager.Instance;
        if (manager == subscribedManager)
            return;

        Unsubscribe();
        subscribedManager = manager;
        if (subscribedManager != null)
            subscribedManager.EvidenceRegistered += HandleEvidenceRegistered;
    }

    private void Unsubscribe()
    {
        if (subscribedManager != null)
            subscribedManager.EvidenceRegistered -= HandleEvidenceRegistered;

        subscribedManager = null;
    }

    private void HandleEvidenceRegistered(EvidenceData _)
    {
        Refresh();
    }

    public void ShowEvidenceGrid()
    {
        if (evidenceGrid != null)
            evidenceGrid.SetActive(true);

        if (evidenceDetailPanel != null)
            evidenceDetailPanel.SetActive(false);

        if (pageNavigationRoot != null)
            pageNavigationRoot.SetActive(true);
    }

    private void OpenDetail(int index)
    {
        if (index < 0 || index >= EvidenceIds.Length)
            return;

        EvidenceData data = FindCollectedEvidence(EvidenceManager.Instance, EvidenceIds[index]);
        if (data == null)
            return;

        if (detailHeaderText != null)
            detailHeaderText.text = $"CASE 404  /  EVIDENCE RECORD · {data.Id}";

        if (detailEvidenceIdText != null)
            detailEvidenceIdText.text = $"EVIDENCE {data.Id.Substring(1)}   /   RECORD";

        if (detailMetadataIdText != null)
            detailMetadataIdText.text = data.Id;

        if (detailEvidenceNameText != null)
            detailEvidenceNameText.text = data.EvidenceName;

        if (detailEvidenceTypeText != null)
            detailEvidenceTypeText.text = IsRequiredEvidence(data.Id) ? "필수 증거" : "선택 증거";

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = string.IsNullOrWhiteSpace(data.Description)
                ? "등록된 설명 정보가 없습니다."
                : data.Description;
        }

        if (detailStatusText != null)
            detailStatusText.text = $"✓ {data.Id}  확보 완료";

        if (detailMetadataStatusText != null)
            detailMetadataStatusText.text = "수집 완료";

        Texture image = evidenceImages != null && index < evidenceImages.Length
            ? evidenceImages[index]
            : null;
        if (detailEvidenceImage != null)
        {
            detailEvidenceImage.texture = image;
            detailEvidenceImage.gameObject.SetActive(image != null);
        }

        if (detailFallbackIcon != null)
            detailFallbackIcon.SetActive(image == null);

        if (evidenceGrid != null)
            evidenceGrid.SetActive(false);

        if (pageNavigationRoot != null)
            pageNavigationRoot.SetActive(false);

        if (evidenceDetailPanel != null)
            evidenceDetailPanel.SetActive(true);
    }

    private void RegisterButtonListeners()
    {
        if (buttonListenersRegistered)
            return;

        AddListener(0, ShowE01Detail);
        AddListener(1, ShowE02Detail);
        AddListener(2, ShowE03Detail);
        AddListener(3, ShowE04Detail);
        AddListener(4, ShowE05Detail);
        AddListener(5, ShowE06Detail);
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ShowEvidenceGrid);
            backButton.onClick.AddListener(ShowEvidenceGrid);
        }

        buttonListenersRegistered = true;
    }

    private void UnregisterButtonListeners()
    {
        if (!buttonListenersRegistered)
            return;

        RemoveListener(0, ShowE01Detail);
        RemoveListener(1, ShowE02Detail);
        RemoveListener(2, ShowE03Detail);
        RemoveListener(3, ShowE04Detail);
        RemoveListener(4, ShowE05Detail);
        RemoveListener(5, ShowE06Detail);
        if (backButton != null)
            backButton.onClick.RemoveListener(ShowEvidenceGrid);

        buttonListenersRegistered = false;
    }

    private void AddListener(int index, UnityEngine.Events.UnityAction action)
    {
        if (evidenceCardButtons == null || index >= evidenceCardButtons.Length ||
            evidenceCardButtons[index] == null)
        {
            return;
        }

        evidenceCardButtons[index].onClick.RemoveListener(action);
        evidenceCardButtons[index].onClick.AddListener(action);
    }

    private void RemoveListener(int index, UnityEngine.Events.UnityAction action)
    {
        if (evidenceCardButtons != null && index < evidenceCardButtons.Length &&
            evidenceCardButtons[index] != null)
        {
            evidenceCardButtons[index].onClick.RemoveListener(action);
        }
    }

    private void SetCardInteractable(int index, bool interactable)
    {
        if (evidenceCardButtons == null || index >= evidenceCardButtons.Length ||
            evidenceCardButtons[index] == null)
        {
            return;
        }

        Button button = evidenceCardButtons[index];
        button.interactable = interactable;
        if (button.targetGraphic != null)
            button.targetGraphic.raycastTarget = interactable;
    }

    private static bool IsRequiredEvidence(string id)
    {
        return id == "E01" || id == "E02" || id == "E03" || id == "E06";
    }

    private void ShowE01Detail() => OpenDetail(0);
    private void ShowE02Detail() => OpenDetail(1);
    private void ShowE03Detail() => OpenDetail(2);
    private void ShowE04Detail() => OpenDetail(3);
    private void ShowE05Detail() => OpenDetail(4);
    private void ShowE06Detail() => OpenDetail(5);

    private static EvidenceData FindCollectedEvidence(EvidenceManager manager, string id)
    {
        if (manager == null)
            return null;

        foreach (EvidenceData data in manager.CollectedEvidence)
        {
            if (data != null && data.Id == id)
                return data;
        }

        return null;
    }

    private static void SetActive(GameObject[] targets, int index, bool active)
    {
        if (targets != null && index < targets.Length && targets[index] != null &&
            targets[index].activeSelf != active)
        {
            targets[index].SetActive(active);
        }
    }
}
