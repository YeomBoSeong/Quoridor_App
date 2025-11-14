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
                Debug.Log($"Loading {settingSceneName}...");
                SceneManager.LoadScene(settingSceneName);
            }
            else
            {
                Debug.LogError("Setting scene name is not set!");
            }
        }));
    }

    public void OnClickHistory()
    {
        StartCoroutine(ValidateSessionAndProceed(() => 
        {
            Debug.Log("History feature not implemented yet");
        }));
    }

    public void OnClickAccount()
    {
        StartCoroutine(ValidateSessionAndProceed(() => 
        {
            Debug.Log("Account feature not implemented yet");
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
        Debug.Log("Exit button clicked. Quitting application...");

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
            Debug.Log("No valid session found. Redirecting to login.");
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
                Debug.Log("Session validation successful");
                onSuccess?.Invoke();
            }
            else
            {
                long statusCode = request.responseCode;
                Debug.Log($"Session validation failed with status: {statusCode}");
                
                if (statusCode == 401)
                {
                    Debug.Log("Session expired due to login from another device. Showing warning and closing application.");
                    SessionData.ClearSession();
                    
                    // 경고 패널 표시 후 3초 후 종료
                    StartCoroutine(ShowWarningAndQuit());
                }
                else
                {
                    Debug.Log($"Session validation error: {request.error}");
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