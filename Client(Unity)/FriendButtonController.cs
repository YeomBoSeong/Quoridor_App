using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;

public class FriendButtonController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] Button friendButton;
    [SerializeField] string friendsSceneName = "FriendsScene";

    void Start()
    {
        SetupUI();
    }

    void SetupUI()
    {
        if (friendButton != null)
            friendButton.onClick.AddListener(OnFriendButtonClicked);
    }

    void OnFriendButtonClicked()
    {
        StartCoroutine(ValidateSessionAndNavigate());
    }

    IEnumerator ValidateSessionAndNavigate()
    {
        if (!SessionData.IsValidSession())
        {
            SceneManager.LoadScene("LoginScene");
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
                if (!string.IsNullOrEmpty(friendsSceneName))
                    SceneManager.LoadScene(friendsSceneName);
            }
            else
            {
                long statusCode = request.responseCode;

                if (statusCode == 401)
                {
                    SessionData.ClearSession();
                    SceneManager.LoadScene("LoginScene");
                }
                else
                {
                    // 네트워크 에러 등의 경우 그냥 진행
                    if (!string.IsNullOrEmpty(friendsSceneName))
                        SceneManager.LoadScene(friendsSceneName);
                }
            }
        }
    }

}