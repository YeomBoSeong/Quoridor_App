using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// SettingScene을 관리하는 컨트롤러
/// </summary>
public class SettingSceneController : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] private string startSceneName = "StartScene";

    private void Start()
    {
    }

    /// <summary>
    /// Back 버튼 클릭 시 호출
    /// StartScene으로 돌아가기
    /// </summary>
    public void OnBackButtonClick()
    {
        if (!string.IsNullOrEmpty(startSceneName))
        {
            SceneManager.LoadScene(startSceneName);
        }
        else
        {
        }
    }

    /// <summary>
    /// 특정 씬으로 이동 (범용)
    /// </summary>
    /// <param name="sceneName">이동할 씬 이름</param>
    public void LoadScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
        }
    }
}
