using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class FriendNotificationBadge : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] GameObject redBadge;
    [SerializeField] Button friendButton;

    [Header("Check Settings")]
    [SerializeField] float checkInterval = 5f;

    private static FriendNotificationBadge instance;
    private int currentRequestCount = 0;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        SetupUI();
        HideBadge();

        // 정기적으로 친구 요청 확인
        InvokeRepeating(nameof(CheckForFriendRequests), 2f, checkInterval);
    }

    void SetupUI()
    {
        if (friendButton != null)
            friendButton.onClick.AddListener(OnFriendButtonClicked);
    }

    public static void ShowBadge()
    {
        if (instance != null)
        {
            instance.ShowRedBadge();
        }
    }

    public static void HideBadge()
    {
        if (instance != null)
        {
            instance.HideRedBadge();
        }
    }

    public static void SetBadgeVisibility(bool hasRequests)
    {
        if (instance != null)
        {
            if (hasRequests)
                instance.ShowRedBadge();
            else
                instance.HideRedBadge();
        }
    }

    void ShowRedBadge()
    {
        if (redBadge != null)
            redBadge.SetActive(true);
    }

    void HideRedBadge()
    {
        if (redBadge != null)
            redBadge.SetActive(false);
    }

    void OnFriendButtonClicked()
    {
        // 친구 요청 목록 패널 표시
        FriendRequestListPanel.ShowRequestList();
    }

    void CheckForFriendRequests()
    {
        if (!SessionData.IsValidSession())
            return;

        StartCoroutine(CheckForFriendRequestsCoroutine());
    }

    IEnumerator CheckForFriendRequestsCoroutine()
    {
        string url = $"{ServerConfig.GetHttpUrl()}/friends/requests";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    var response = JsonUtility.FromJson<FriendRequestsResponse>(jsonResponse);

                    int requestCount = response.requests != null ? response.requests.Length : 0;

                    // 요청 개수가 변경되었을 때만 UI 업데이트
                    if (requestCount != currentRequestCount)
                    {
                        currentRequestCount = requestCount;
                        SetBadgeVisibility(requestCount > 0);
                    }
                }
                catch (System.Exception e)
                {
                }
            }
            else if (request.responseCode != 401) // 401은 세션 만료이므로 로그하지 않음
            {
            }
        }
    }
}