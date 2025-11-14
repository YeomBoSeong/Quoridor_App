using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;

[System.Serializable]
public class FriendData
{
    public string username;
    public int rapid_elo;
    public int blitz_elo;
    public bool is_online;
    public string status;
}

[System.Serializable]
public class MeResponse
{
    public int id;
    public string username;
    public string email;
    public string created_at;
}

[System.Serializable]
public class FriendsListResponse
{
    public FriendData[] friends;
}

[System.Serializable]
public class AddFriendRequestData
{
    public string username;
}

[System.Serializable]
public class SendFriendRequestResponse
{
    public string message;
    public int request_id;
}

public class FriendsManager : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] string startSceneName = "StartScene";

    [Header("UI Elements")]
    [SerializeField] Button backButton;
    [SerializeField] Button sendButton;
    [SerializeField] TMP_InputField friendUsernameInput;
    [SerializeField] GameObject friendItemPrefab;
    [SerializeField] Transform friendsList;
    [SerializeField] ScrollRect friendsScrollRect;

    [Header("Friend Request UI")]
    [SerializeField] Button friendRequestButton;
    [SerializeField] GameObject friendRequestBadge;

    [Header("Session Warning UI")]
    [SerializeField] GameObject warningPanel;
    [SerializeField] TextMeshProUGUI warningText;

    [Header("Unfriend Confirmation Panel")]
    [SerializeField] GameObject unfriendConfirmationPanel;
    [SerializeField] TextMeshProUGUI unfriendConfirmationText;
    [SerializeField] Button confirmUnfriendButton;
    [SerializeField] Button cancelUnfriendButton;

    private List<GameObject> friendItems = new List<GameObject>();
    private int currentRequestCount = 0;
    private int currentFriendCount = 0;

    private static FriendsManager instance;

    void Start()
    {
        instance = this;

        SetupUI();
        LoadFriendsList();

        // MessageNotificationManager 확인 및 생성
        EnsureMessageNotificationManager();

        // Unfriend 확인 패널을 FriendItemUI에 등록
        RegisterUnfriendPanel();

        // 정기적으로 친구 요청 확인
        InvokeRepeating(nameof(CheckForFriendRequests), 2f, 5f);

        // 정기적으로 친구 목록 변화 확인
        InvokeRepeating(nameof(CheckForFriendListChanges), 3f, 5f);
    }

    void EnsureMessageNotificationManager()
    {
        // MessageNotificationManager가 없으면 생성
        if (FindObjectOfType<MessageNotificationManager>() == null)
        {
            GameObject managerGO = new GameObject("MessageNotificationManager");
            managerGO.AddComponent<MessageNotificationManager>();
        }
    }

    void RegisterUnfriendPanel()
    {
        // FriendsScene의 Unfriend 패널을 FriendItemUI에 등록
        if (unfriendConfirmationPanel != null &&
            unfriendConfirmationText != null &&
            confirmUnfriendButton != null &&
            cancelUnfriendButton != null)
        {
            FriendItemUI.RegisterRemoveConfirmationPanel(
                unfriendConfirmationPanel,
                unfriendConfirmationText,
                confirmUnfriendButton,
                cancelUnfriendButton
            );
            Debug.Log("[FriendsManager] Unfriend panel registered to FriendItemUI");
        }
        else
        {
            Debug.LogWarning("[FriendsManager] Unfriend panel components are not assigned in Inspector!");
        }
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void SetupUI()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClicked);

        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendButtonClicked);

        if (friendRequestButton != null)
            friendRequestButton.onClick.AddListener(OnFriendRequestButtonClicked);

        if (warningPanel != null)
            warningPanel.SetActive(false);

        if (friendRequestBadge != null)
            friendRequestBadge.SetActive(false);
    }

    public void OnBackButtonClicked()
    {
        if (!string.IsNullOrEmpty(startSceneName))
            SceneManager.LoadScene(startSceneName);
    }

    public void OnSendButtonClicked()
    {
        string username = friendUsernameInput?.text?.Trim();
        if (!string.IsNullOrEmpty(username))
        {
            StartCoroutine(SendFriendRequest(username));
        }
    }

    public void OnFriendRequestButtonClicked()
    {
        // 친구 요청 목록 패널 표시
        FriendRequestListPanel.ShowRequestList();
    }

    void LoadFriendsList()
    {
        StartCoroutine(ValidateSessionAndProceed(() =>
        {
            StartCoroutine(LoadFriendsListCoroutine());
        }));
    }

    public void RefreshFriendsList()
    {
        StartCoroutine(LoadFriendsListCoroutine());
    }

    public static void RefreshFriendsListStatic()
    {
        if (instance != null)
        {
            instance.RefreshFriendsList();
        }
    }

    IEnumerator SendFriendRequest(string username)
    {
        string url = $"{ServerConfig.GetHttpUrl()}/friends/request";

        AddFriendRequestData requestData = new AddFriendRequestData();
        requestData.username = username;
        string jsonData = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    SendFriendRequestResponse response = JsonUtility.FromJson<SendFriendRequestResponse>(jsonResponse);

                    // 응답에 따라 적절한 메시지 표시
                    FriendRequestPanel.ShowNotification(response.message);

                    // 입력 필드 초기화
                    if (friendUsernameInput != null)
                        friendUsernameInput.text = "";
                }
                catch (System.Exception e)
                {
                    FriendRequestPanel.ShowNotification("Error sending request!");
                }
            }
            else
            {
                long statusCode = request.responseCode;
                if (statusCode == 401)
                {
                    SessionData.ClearSession();
                    StartCoroutine(ShowWarningAndQuit());
                }
                else
                {
                    FriendRequestPanel.ShowNotification("Failed to send request!");
                }
            }
        }
    }

    IEnumerator LoadFriendsListCoroutine()
    {
        string url = $"{ServerConfig.GetHttpUrl()}/friends";

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
                    FriendsListResponse friendsResponse = JsonUtility.FromJson<FriendsListResponse>(jsonResponse);

                    UpdateFriendsList(friendsResponse.friends);

                    // 현재 친구 수 업데이트
                    currentFriendCount = friendsResponse.friends.Length;
                }
                catch (System.Exception e)
                {
                }
            }
            else
            {
                long statusCode = request.responseCode;
                if (statusCode == 401)
                {
                    SessionData.ClearSession();
                    StartCoroutine(ShowWarningAndQuit());
                }
            }
        }
    }

    void UpdateFriendsList(FriendData[] friends)
    {
        // 기존 친구 아이템들 제거
        foreach (GameObject item in friendItems)
        {
            if (item != null)
                Destroy(item);
        }
        friendItems.Clear();

        // 새 친구 아이템들 생성
        foreach (FriendData friend in friends)
        {
            CreateFriendItem(friend);
        }
    }

    void CreateFriendItem(FriendData friend)
    {
        if (friendItemPrefab == null || friendsList == null) return;

        GameObject friendItem = Instantiate(friendItemPrefab, friendsList);
        friendItems.Add(friendItem);

        // 친구 정보 설정
        FriendItemUI friendUI = friendItem.GetComponent<FriendItemUI>();
        if (friendUI != null)
        {
            friendUI.SetFriendData(friend);
        }
        else
        {
            // FriendItemUI 컴포넌트가 없으면 기본 텍스트로 설정
            TextMeshProUGUI[] texts = friendItem.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                // 첫 번째 텍스트: 유저네임만
                texts[0].text = friend.username;
                // 두 번째 텍스트: ELO 정보
                texts[1].text = $"R:{friend.rapid_elo} B:{friend.blitz_elo}";
            }
            else if (texts.Length == 1)
            {
                // 텍스트가 하나만 있으면 유저네임만
                texts[0].text = friend.username;
            }
        }
    }

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
                    StartCoroutine(ShowWarningAndQuit());
                }
                else
                {
                    onSuccess?.Invoke();
                }
            }
        }
    }

    void GoToLogin()
    {
        SceneManager.LoadScene("LoginScene");
    }

    IEnumerator ShowWarningAndQuit()
    {
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
            yield return new WaitForSeconds(3f);
        }

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
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

                        if (friendRequestBadge != null)
                        {
                            friendRequestBadge.SetActive(requestCount > 0);
                        }
                    }
                }
                catch (System.Exception e)
                {
                }
            }
        }
    }

    void CheckForFriendListChanges()
    {
        if (!SessionData.IsValidSession())
            return;

        StartCoroutine(CheckForFriendListChangesCoroutine());
    }

    IEnumerator CheckForFriendListChangesCoroutine()
    {
        string url = $"{ServerConfig.GetHttpUrl()}/friends";

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
                    FriendsListResponse friendsResponse = JsonUtility.FromJson<FriendsListResponse>(jsonResponse);

                    int newFriendCount = friendsResponse.friends.Length;

                    // 친구 수가 변경되었을 때만 UI 업데이트
                    if (newFriendCount != currentFriendCount)
                    {
                        currentFriendCount = newFriendCount;
                        UpdateFriendsList(friendsResponse.friends);
                    }
                }
                catch (System.Exception e)
                {
                }
            }
        }
    }

    /// <summary>
    /// 친구 삭제 처리 (FriendItemUI에서 호출)
    /// </summary>
    public static void RemoveFriendStatic(string friendUsername)
    {
        if (instance != null)
        {
            instance.StartCoroutine(instance.RemoveFriendCoroutine(friendUsername));
        }
    }

    IEnumerator RemoveFriendCoroutine(string friendUsername)
    {
        Debug.Log($"[FriendsManager] Starting RemoveFriendCoroutine for: {friendUsername}");

        string url = $"{ServerConfig.GetHttpUrl()}/friends/remove";
        Debug.Log($"[FriendsManager] URL: {url}");

        // 요청 데이터 생성
        AddFriendRequestData requestData = new AddFriendRequestData();
        requestData.username = friendUsername;
        string jsonData = JsonUtility.ToJson(requestData);
        Debug.Log($"[FriendsManager] Request JSON: {jsonData}");

        using (UnityWebRequest request = new UnityWebRequest(url, "DELETE"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            Debug.Log($"[FriendsManager] Sending DELETE request to server...");
            yield return request.SendWebRequest();

            Debug.Log($"[FriendsManager] Request completed. Result: {request.result}");
            Debug.Log($"[FriendsManager] Response Code: {request.responseCode}");

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"[FriendsManager] Server Response: {responseText}");
                Debug.Log($"[FriendsManager] Successfully removed friend: {friendUsername}");

                // 친구 목록 새로고침
                Debug.Log($"[FriendsManager] Refreshing friend list...");
                RefreshFriendsList();
                Debug.Log($"[FriendsManager] Friend list refresh requested");
            }
            else
            {
                string errorText = request.downloadHandler?.text ?? "No error text";
                Debug.LogError($"[FriendsManager] Failed to remove friend: {friendUsername}");
                Debug.LogError($"[FriendsManager] Error: {request.error}");
                Debug.LogError($"[FriendsManager] Response Code: {request.responseCode}");
                Debug.LogError($"[FriendsManager] Response Text: {errorText}");
            }
        }

        Debug.Log($"[FriendsManager] RemoveFriendCoroutine completed for: {friendUsername}");
    }
}

