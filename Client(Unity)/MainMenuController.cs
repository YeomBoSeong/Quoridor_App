using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using TMPro;
using Debug = UnityEngine.Debug;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] string timeControlSceneName = "TimeControlScene";
    [SerializeField] string friendsSceneName = "FriendsScene";
    [SerializeField] string profileSceneName = "ProfileScene";
    [SerializeField] string settingSceneName = "SettingScene";
    [SerializeField] string loginSceneName = "LoginScene";
    
    [Header("Session Warning UI")]
    [SerializeField] GameObject warningPanel;
    [SerializeField] TextMeshProUGUI warningText;

    public void OnClickPlay()
    {
        StartCoroutine(ValidateSessionAndProceed(() => 
        {
            if (!string.IsNullOrEmpty(timeControlSceneName))
                SceneManager.LoadScene(timeControlSceneName);
        }));
    }

    public void OnClickFriends()
    {
        StartCoroutine(ValidateSessionAndProceed(() =>
        {
            if (!string.IsNullOrEmpty(friendsSceneName))
                SceneManager.LoadScene(friendsSceneName);
        }));
    }

    public void OnClickProfile()
    {
        StartCoroutine(ValidateSessionAndProceed(() => 
        {
            if (!string.IsNullOrEmpty(profileSceneName))
                SceneManager.LoadScene(profileSceneName);
        }));
    }

    public void OnClickSettings()
    {
        StartCoroutine(ValidateSessionAndProceed(() =>
        {
            if (!string.IsNullOrEmpty(settingSceneName))
            {
                SceneManager.LoadScene(settingSceneName);
            }
            else
            {
            }
        }));
    }

    public void OnClickHistory()
    {
        StartCoroutine(ValidateSessionAndProceed(() => 
        {
        }));
    }

    public void OnClickAccount()
    {
        StartCoroutine(ValidateSessionAndProceed(() => 
        {
        }));
    }

    public void OnClickLogout()
    {
        PlayerPrefs.DeleteKey("jwt");
        PlayerPrefs.DeleteKey("username");
        PlayerPrefs.Save();
        SessionData.ClearSession();

        if (!string.IsNullOrEmpty(loginSceneName))
            SceneManager.LoadScene(loginSceneName);
    }

    public void OnClickExit()
    {

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    // 세션 검증을 위한 /me 엔드포인트 호출
    IEnumerator ValidateSessionAndProceed(System.Action onSuccess)
    {
        if (!SessionData.IsValidSession())
        {
            GoToLogin();
            yield break;
        }

        string url = $"{ServerConfig.GetHttpUrl()}/me";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke();
            }
            else
            {
                long statusCode = request.responseCode;
                
                if (statusCode == 401)
                {
                    SessionData.ClearSession();
                    
                    // 경고 패널 표시 후 3초 후 종료
                    StartCoroutine(ShowWarningAndQuit());
                }
                else
                {
                    // 네트워크 에러 등의 경우 그냥 진행 (선택사항)
                    onSuccess?.Invoke();
                }
            }
        }
    }
    
    void GoToLogin()
    {
        if (!string.IsNullOrEmpty(loginSceneName))
            SceneManager.LoadScene(loginSceneName);
    }
    
    IEnumerator ShowWarningAndQuit()
    {
        // 경고 패널 활성화
        if (warningPanel != null)
        {
            warningPanel.SetActive(true);
            
            if (warningText != null)
            {
                for (int i = 3; i > 0; i--)
                {
                    warningText.text = $"Your account logged in another device. Program terminates in {i} seconds....";
                    yield return new WaitForSeconds(1f);
                }
            }
        }
        else
        {
            // 패널이 없으면 3초 대기
            yield return new WaitForSeconds(3f);
        }
        
        // 프로그램 강제 종료
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}