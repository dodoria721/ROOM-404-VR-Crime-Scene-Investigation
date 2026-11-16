using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴의 버튼 연결과 조작 안내 패널 표시만 담당한다.
/// Scene 전환은 SceneFlowController에 위임한다.
/// </summary>
public sealed class MainMenuUIController : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private SceneFlowController sceneFlowController;

    [Header("Main Menu")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button quitButton;

    [Header("Controls Panel")]
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private Button closeControlsButton;

    private void Awake()
    {
        if (sceneFlowController == null)
            sceneFlowController = GetComponent<SceneFlowController>();

        SetControlsVisible(false);
    }

    private void OnEnable()
    {
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    public void ShowControls()
    {
        SetControlsVisible(true);
    }

    public void HideControls()
    {
        SetControlsVisible(false);
    }

    private void RegisterListeners()
    {
        UnregisterListeners();

        if (startButton != null && sceneFlowController != null)
            startButton.onClick.AddListener(sceneFlowController.StartBriefing);

        if (controlsButton != null)
            controlsButton.onClick.AddListener(ShowControls);

        if (quitButton != null && sceneFlowController != null)
            quitButton.onClick.AddListener(sceneFlowController.QuitGame);

        if (closeControlsButton != null)
            closeControlsButton.onClick.AddListener(HideControls);
    }

    private void UnregisterListeners()
    {
        if (startButton != null && sceneFlowController != null)
            startButton.onClick.RemoveListener(sceneFlowController.StartBriefing);

        if (controlsButton != null)
            controlsButton.onClick.RemoveListener(ShowControls);

        if (quitButton != null && sceneFlowController != null)
            quitButton.onClick.RemoveListener(sceneFlowController.QuitGame);

        if (closeControlsButton != null)
            closeControlsButton.onClick.RemoveListener(HideControls);
    }

    private void SetControlsVisible(bool visible)
    {
        if (controlsPanel != null && controlsPanel.activeSelf != visible)
            controlsPanel.SetActive(visible);
    }
}
