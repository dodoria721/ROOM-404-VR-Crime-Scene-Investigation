using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사건 브리핑 화면의 두 전환 버튼을 SceneFlowController와 연결한다.
/// </summary>
public sealed class BriefingUIController : MonoBehaviour
{
    [SerializeField] private SceneFlowController sceneFlowController;
    [SerializeField] private Button startInvestigationButton;
    [SerializeField] private Button returnToMainMenuButton;

    private void Awake()
    {
        if (sceneFlowController == null)
            sceneFlowController = GetComponent<SceneFlowController>();
    }

    private void OnEnable()
    {
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    private void RegisterListeners()
    {
        UnregisterListeners();

        if (startInvestigationButton != null && sceneFlowController != null)
            startInvestigationButton.onClick.AddListener(sceneFlowController.StartInvestigation);

        if (returnToMainMenuButton != null && sceneFlowController != null)
            returnToMainMenuButton.onClick.AddListener(sceneFlowController.ReturnToMainMenu);
    }

    private void UnregisterListeners()
    {
        if (startInvestigationButton != null && sceneFlowController != null)
            startInvestigationButton.onClick.RemoveListener(sceneFlowController.StartInvestigation);

        if (returnToMainMenuButton != null && sceneFlowController != null)
            returnToMainMenuButton.onClick.RemoveListener(sceneFlowController.ReturnToMainMenu);
    }
}
