using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;

[System.Serializable]
public class ChatMessageRequest
{
    public string receiver_username;
    public string message;
}

[System.Serializable]
public class ChatMessageData
{
    public int id;
    public string sender_username;
    public string receiver_username;
    public string message;
    public string created_at;
    public bool is_sent_by_me;
}

[System.Serializable]
public class ChatHistoryResponse
{
    public ChatMessageData[] messages;
}

[System.Serializable]
public class ChatMessageResponse
{
    public bool success;
    public string message;
}

public class ChatManager : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] string friendsSceneName = "FriendsScene";

    [Header("UI Elements")]
    [SerializeField] Button backButton;
    [SerializeField] TMP_InputField messageInput;
    [SerializeField] Button sendButton;
    [SerializeField] ScrollRect chatScrollRect;
    [SerializeField] Transform chatContent;

    [Header("Message UI")]
    [SerializeField] GameObject myMessagePrefab;
    [SerializeField] GameObject theirMessagePrefab;

    [Header("Profile UI")]
    [SerializeField] UnityEngine.UI.Image myProfileImage;
    [SerializeField] UnityEngine.UI.Image friendProfileImage;
    [SerializeField] Sprite defaultProfileSprite;


    [Header("Session Warning UI")]
    [SerializeField] GameObject warningPanel;
    [SerializeField] TextMeshProUGUI warningText;

    private string friendUsername;
    private List<GameObject> messageItems = new List<GameObject>();

    // 프로필 이미지 캐싱
    private Dictionary<string, Sprite> profileImageCache = new Dictionary<string, Sprite>();
    private bool isMyProfileLoaded = false;
    private bool isFriendProfileLoaded = false;

    public static string TargetFriendUsername { get; set; }

    void Start()
    {
        friendUsername = TargetFriendUsername;

        if (string.IsNullOrEmpty(friendUsername))
        {
            Debug.LogError("No target friend username set!");
            GoBackToFriendsScene();
            return;
        }

        SetupUI();
        LoadChatHistory();
        LoadProfileImages();

        // 주기적으로 새 메시지 확인 (5초마다)
        InvokeRepeating(nameof(LoadChatHistory), 5f, 5f);
    }

    void SetupUI()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClicked);

        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendButtonClicked);

        if (warningPanel != null)
            warningPanel.SetActive(false);
    }

    public void OnBackButtonClicked()
    {
        GoBackToFriendsScene();
    }

    public void OnSendButtonClicked()
    {
        string messageText = messageInput?.text?.Trim();
        if (!string.IsNullOrEmpty(messageText))
        {
            StartCoroutine(SendMessage(messageText));
        }
    }

    void GoBackToFriendsScene()
    {
        if (!string.IsNullOrEmpty(friendsSceneName))
            SceneManager.LoadScene(friendsSceneName);
    }

    void LoadChatHistory()
    {
        StartCoroutine(ValidateSessionAndProceed(() =>
        {
            StartCoroutine(LoadChatHistoryCoroutine());
        }));
    }

    void LoadProfileImages()
    {
        // 내 프로필 이미지 로드 (한 번만)
        if (!isMyProfileLoaded && !string.IsNullOrEmpty(SessionData.username))
        {
            StartCoroutine(LoadMyProfileImage());
        }

        // 친구 프로필 이미지 로드 (한 번만)
        if (!isFriendProfileLoaded && !string.IsNullOrEmpty(friendUsername))
        {
            StartCoroutine(LoadFriendProfileImage());
        }
    }

    IEnumerator LoadMyProfileImage()
    {
        // 캐시에서 확인
        if (profileImageCache.ContainsKey(SessionData.username))
        {
            if (myProfileImage != null)
            {
                myProfileImage.sprite = profileImageCache[SessionData.username];
            }
            isMyProfileLoaded = true;
            yield break;
        }

        string url = $"{ServerConfig.GetHttpUrl()}/user/{SessionData.username}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                MeResponse userResponse = null;
                try
                {
                    userResponse = JsonUtility.FromJson<MeResponse>(request.downloadHandler.text);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing my user info: {e.Message}");
                }

                if (userResponse != null)
                {
                    yield return StartCoroutine(LoadAndCacheProfileImage(userResponse.id, SessionData.username, myProfileImage));
                    isMyProfileLoaded = true;
                }
            }
        }
    }

    IEnumerator LoadFriendProfileImage()
    {
        // 캐시에서 확인
        if (profileImageCache.ContainsKey(friendUsername))
        {
            if (friendProfileImage != null)
            {
                friendProfileImage.sprite = profileImageCache[friendUsername];
            }
            isFriendProfileLoaded = true;
            yield break;
        }

        string url = $"{ServerConfig.GetHttpUrl()}/user/{friendUsername}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                MeResponse userResponse = null;
                try
                {
                    userResponse = JsonUtility.FromJson<MeResponse>(request.downloadHandler.text);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing friend user info: {e.Message}");
                }

                if (userResponse != null)
                {
                    yield return StartCoroutine(LoadAndCacheProfileImage(userResponse.id, friendUsername, friendProfileImage));
                    isFriendProfileLoaded = true;
                }
            }
        }
    }

    IEnumerator LoadAndCacheProfileImage(int userId, string username, UnityEngine.UI.Image targetImage)
    {
        string url = $"{ServerConfig.GetHttpUrl()}/profile-image/{userId}";

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);

                if (texture != null)
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

                    // 캐시에 저장
                    profileImageCache[username] = sprite;

                    // UI에 적용
                    if (targetImage != null)
                    {
                        targetImage.sprite = sprite;
                    }
                }
            }
            else
            {
                // 실패 시 기본 이미지 설정 및 캐시
                if (defaultProfileSprite != null)
                {
                    profileImageCache[username] = defaultProfileSprite;
                    if (targetImage != null)
                    {
                        targetImage.sprite = defaultProfileSprite;
                    }
                }
            }
        }
    }

    IEnumerator LoadProfileImageById(int userId, UnityEngine.UI.Image targetImage)
    {
        string url = $"{ServerConfig.GetHttpUrl()}/profile-image/{userId}";

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);

                if (texture != null && targetImage != null)
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    targetImage.sprite = sprite;
                }
            }
            else
            {
                // 실패 시 기본 이미지 설정
                if (targetImage != null && defaultProfileSprite != null)
                {
                    targetImage.sprite = defaultProfileSprite;
                }
            }
        }
    }

    IEnumerator SendMessage(string messageText)
    {
        string url = $"{ServerConfig.GetHttpUrl()}/chat/send";

        ChatMessageRequest requestData = new ChatMessageRequest
        {
            receiver_username = friendUsername,
            message = messageText
        };

        string jsonData = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
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
                    ChatMessageResponse response = JsonUtility.FromJson<ChatMessageResponse>(request.downloadHandler.text);
                    if (response.success)
                    {
                        // 입력 필드 초기화
                        if (messageInput != null)
                            messageInput.text = "";

                        // 채팅 기록 새로고침
                        LoadChatHistory();
                    }
                    else
                    {
                        Debug.LogError($"Failed to send message: {response.message}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing send message response: {e.Message}");
                }
            }
            else
            {
                long statusCode = request.responseCode;
                if (statusCode == 401)
                {
                    Debug.Log("Session expired while sending message");
                    SessionData.ClearSession();
                    StartCoroutine(ShowWarningAndQuit());
                }
                else
                {
                    Debug.LogError($"Failed to send message: {request.error}");
                }
            }
        }
    }

    IEnumerator LoadChatHistoryCoroutine()
    {
        string url = $"{ServerConfig.GetHttpUrl()}/chat/history/{friendUsername}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    ChatHistoryResponse chatHistory = JsonUtility.FromJson<ChatHistoryResponse>(request.downloadHandler.text);
                    UpdateChatDisplay(chatHistory.messages);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing chat history: {e.Message}");
                }
            }
            else
            {
                long statusCode = request.responseCode;
                if (statusCode == 401)
                {
                    Debug.Log("Session expired while loading chat history");
                    SessionData.ClearSession();
                    StartCoroutine(ShowWarningAndQuit());
                }
                else
                {
                    Debug.LogError($"Failed to load chat history: {request.error}");
                }
            }
        }
    }

    void UpdateChatDisplay(ChatMessageData[] messages)
    {
        // 기존 메시지들 제거
        foreach (GameObject item in messageItems)
        {
            if (item != null)
                Destroy(item);
        }
        messageItems.Clear();

        // 새 메시지들 생성
        foreach (ChatMessageData message in messages)
        {
            CreateMessageItem(message);
        }

        // 스크롤을 가장 아래로
        StartCoroutine(ScrollToBottom());
    }

    void CreateMessageItem(ChatMessageData message)
    {
        GameObject prefab = message.is_sent_by_me ? myMessagePrefab : theirMessagePrefab;
        if (prefab == null || chatContent == null) return;

        GameObject messageItem = Instantiate(prefab, chatContent);
        messageItems.Add(messageItem);

        // 메시지 텍스트 설정
        TextMeshProUGUI messageText = messageItem.GetComponentInChildren<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = message.message;
        }

        // 프로필 이미지 설정
        UnityEngine.UI.Image profileImg = messageItem.GetComponentInChildren<UnityEngine.UI.Image>();
        if (profileImg != null)
        {
            StartCoroutine(LoadMessageProfileImage(profileImg, message.sender_username));
        }
    }

    IEnumerator LoadMessageProfileImage(UnityEngine.UI.Image targetImage, string username)
    {
        // 캐시에서 확인
        if (profileImageCache.ContainsKey(username))
        {
            if (targetImage != null)
            {
                targetImage.sprite = profileImageCache[username];
            }
            yield break;
        }

        // 캐시에 없으면 로드
        string userUrl = $"{ServerConfig.GetHttpUrl()}/user/{username}";

        using (UnityWebRequest request = UnityWebRequest.Get(userUrl))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                MeResponse userResponse = null;
                try
                {
                    userResponse = JsonUtility.FromJson<MeResponse>(request.downloadHandler.text);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing user info for message: {e.Message}");
                }

                if (userResponse != null)
                {
                    yield return StartCoroutine(LoadAndCacheProfileImage(userResponse.id, username, targetImage));
                }
            }
            else
            {
                // 실패 시 기본 이미지 설정 및 캐시
                if (defaultProfileSprite != null)
                {
                    profileImageCache[username] = defaultProfileSprite;
                    if (targetImage != null)
                    {
                        targetImage.sprite = defaultProfileSprite;
                    }
                }
            }
        }
    }

    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        if (chatScrollRect != null)
        {
            chatScrollRect.verticalNormalizedPosition = 0f;
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
                    // 네트워크 에러 등의 경우 그냥 진행
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
}