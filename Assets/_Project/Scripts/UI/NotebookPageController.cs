using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NotebookPageController : MonoBehaviour
{
    private const int PageCount = 5;

    [Header("Notebook Pages")]
    [SerializeField] private GameObject caseOverviewPage;
    [SerializeField] private GameObject victimPage;
    [SerializeField] private GameObject suspectPage;
    [SerializeField] private GameObject evidencePage;
    [SerializeField] private GameObject memoPage;

    [Header("Tab Buttons")]
    [SerializeField] private Button caseTabButton;
    [SerializeField] private Button victimTabButton;
    [SerializeField] private Button suspectTabButton;
    [SerializeField] private Button evidenceTabButton;
    [SerializeField] private Button memoTabButton;

    [Header("Tab Visuals")]
    [SerializeField] private Image[] tabSelectionGraphics;
    [SerializeField] private Color[] tabDefaultColors;
    [SerializeField] private Color selectedTabColor = new Color32(205, 182, 111, 255);

    [Header("Page Navigation")]
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private TMP_Text pageNumberText;

    private GameObject[] pages;
    private bool listenersRegistered;

    public int CurrentPageIndex { get; private set; }

    private void Awake()
    {
        CachePages();
        ValidateReferences();
        CurrentPageIndex = 0;
        RefreshPage();
    }

    private void OnEnable()
    {
        RegisterListeners();
        RefreshPage();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    public void ShowPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= PageCount)
        {
            Debug.LogWarning($"[Notebook] Invalid page index ignored: {pageIndex}", this);
            return;
        }

        if (CurrentPageIndex == pageIndex)
        {
            RefreshPage();
            return;
        }

        CurrentPageIndex = pageIndex;
        RefreshPage();
    }

    public void NextPage()
    {
        if (CurrentPageIndex < PageCount - 1)
            ShowPage(CurrentPageIndex + 1);
    }

    public void PreviousPage()
    {
        if (CurrentPageIndex > 0)
            ShowPage(CurrentPageIndex - 1);
    }

    public void RefreshPage()
    {
        if (pages == null || pages.Length != PageCount)
            CachePages();

        for (int i = 0; i < pages.Length; i++)
        {
            GameObject page = pages[i];
            if (page != null && page.activeSelf != (i == CurrentPageIndex))
                page.SetActive(i == CurrentPageIndex);
        }

        if (pageNumberText != null)
            pageNumberText.text = $"{CurrentPageIndex + 1:00} / {PageCount:00}";

        if (previousPageButton != null)
            previousPageButton.interactable = CurrentPageIndex > 0;

        if (nextPageButton != null)
            nextPageButton.interactable = CurrentPageIndex < PageCount - 1;

        RefreshTabVisuals();
    }

    private void CachePages()
    {
        pages = new[]
        {
            caseOverviewPage,
            victimPage,
            suspectPage,
            evidencePage,
            memoPage
        };
    }

    private void RefreshTabVisuals()
    {
        if (tabSelectionGraphics == null)
            return;

        for (int i = 0; i < tabSelectionGraphics.Length; i++)
        {
            Image graphic = tabSelectionGraphics[i];
            if (graphic == null)
                continue;

            Color defaultColor = Color.white;
            if (tabDefaultColors != null && i < tabDefaultColors.Length)
                defaultColor = tabDefaultColors[i];

            bool selected = i == CurrentPageIndex;
            graphic.color = selected ? selectedTabColor : defaultColor;

            // Figma에서는 선택된 인덱스만 책 바깥쪽으로 28 px 더 돌출된다.
            float width = selected ? 220f : 192f;
            RectTransform graphicRect = graphic.rectTransform;
            graphicRect.sizeDelta = new Vector2(width, graphicRect.sizeDelta.y);

            RectTransform tabRoot = graphicRect.parent as RectTransform;
            if (tabRoot != null)
            {
                tabRoot.anchoredPosition = new Vector2(selected ? 0f : 28f, tabRoot.anchoredPosition.y);
                tabRoot.sizeDelta = new Vector2(width, tabRoot.sizeDelta.y);

                RectTransform label = tabRoot.Find("Label") as RectTransform;
                if (label != null)
                    label.sizeDelta = new Vector2(width - 50f, label.sizeDelta.y);
            }
        }
    }

    private void RegisterListeners()
    {
        if (listenersRegistered)
            return;

        AddListener(caseTabButton, ShowCaseOverview);
        AddListener(victimTabButton, ShowVictim);
        AddListener(suspectTabButton, ShowSuspect);
        AddListener(evidenceTabButton, ShowEvidence);
        AddListener(memoTabButton, ShowMemo);
        AddListener(previousPageButton, PreviousPage);
        AddListener(nextPageButton, NextPage);
        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered)
            return;

        RemoveListener(caseTabButton, ShowCaseOverview);
        RemoveListener(victimTabButton, ShowVictim);
        RemoveListener(suspectTabButton, ShowSuspect);
        RemoveListener(evidenceTabButton, ShowEvidence);
        RemoveListener(memoTabButton, ShowMemo);
        RemoveListener(previousPageButton, PreviousPage);
        RemoveListener(nextPageButton, NextPage);
        listenersRegistered = false;
    }

    private bool ValidateReferences()
    {
        bool valid =
            caseOverviewPage != null &&
            victimPage != null &&
            suspectPage != null &&
            evidencePage != null &&
            memoPage != null &&
            caseTabButton != null &&
            victimTabButton != null &&
            suspectTabButton != null &&
            evidenceTabButton != null &&
            memoTabButton != null &&
            previousPageButton != null &&
            nextPageButton != null &&
            pageNumberText != null;

        if (!valid)
            Debug.LogError("[Notebook] NotebookPageController has missing Inspector references.", this);

        return valid;
    }

    private void ShowCaseOverview() => ShowPage(0);
    private void ShowVictim() => ShowPage(1);
    private void ShowSuspect() => ShowPage(2);
    private void ShowEvidence() => ShowPage(3);
    private void ShowMemo() => ShowPage(4);

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }

    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }
}
