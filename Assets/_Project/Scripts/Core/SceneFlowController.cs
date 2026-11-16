using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MainMenu, Briefing, 현장 조사 사이의 단일 Scene 전환을 담당한다.
/// 이 컴포넌트는 Scene을 넘어 유지되지 않으므로 새 게임은 항상 새 상태로 시작한다.
/// </summary>
public sealed class SceneFlowController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string BriefingSceneName = "Briefing";
    private const string InvestigationSceneName = "VR_Test";

    private bool isLoading;

    public bool IsLoading => isLoading;

    public void StartBriefing()
    {
        LoadSingleScene(BriefingSceneName);
    }

    public void StartInvestigation()
    {
        LoadSingleScene(InvestigationSceneName);
    }

    public void RestartInvestigation()
    {
        LoadSingleScene(InvestigationSceneName);
    }

    public void ReturnToMainMenu()
    {
        LoadSingleScene(MainMenuSceneName);
    }

    public void QuitGame()
    {
        if (isLoading)
            return;

#if UNITY_EDITOR
        Debug.Log("[SceneFlow] 게임 종료는 빌드에서 Application.Quit()로 처리됩니다.", this);
#else
        Application.Quit();
#endif
    }

    private void LoadSingleScene(string sceneName)
    {
        if (isLoading)
            return;

        isLoading = true;

        try
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
                sceneName,
                LoadSceneMode.Single);

            if (loadOperation == null)
            {
                isLoading = false;
                Debug.LogError($"[SceneFlow] Scene 로드를 시작하지 못했습니다: {sceneName}", this);
            }
        }
        catch (Exception exception)
        {
            isLoading = false;
            Debug.LogError(
                $"[SceneFlow] Scene '{sceneName}' 로드 중 오류가 발생했습니다. " +
                "Build Settings/Build Profile 등록 상태를 확인하세요.\n" + exception,
                this);
        }
    }
}
