using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class UnreadMessageData
{
    public string friend_username;
    public int unread_count;
}

[System.Serializable]
public class UnreadMessagesResponse
{
    public UnreadMessageData[] unread_messages;
}

public class MessageNotificationManager : MonoBehaviour
{
    [Header("Check Settings")]
    [SerializeField] float checkInterval = 10f;

    private static MessageNotificationManager instance;
    private Dictionary<string, int> unreadCounts = new Dictionary<string, int>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[MessageNotificationManager] Instance created and set to DontDestroyOnLoad");
        }
        else
        {
            Debug.Log("[MessageNotificationManager] Duplicate instance found, destroying");
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        Debug.Log($"[MessageNotificationManager] Starting unread message checking every {checkInterval} seconds");
        // 정기적으로 읽지 않은 메시지 확인
        InvokeRepeating(nameof(CheckForUnreadMessages), 5f, checkInterval);
    }

    public static void RegisterFriendItem(string friendUsername, FriendItemUI friendItem)
    {
        if (instance != null)
        {
            instance.UpdateFriendNotification(friendUsername);
        }
    }

    public static void MarkMessagesAsRead(string friendUsername)
    {
        if (instance != null)
        {
            instance.unreadCounts[friendUsername] = 0;
            instance.UpdateFriendNotification(friendUsername);

            // 서버에도 읽음 상태 전송
            instance.StartCoroutine(instance.MarkMessagesAsReadOnServer(friendUsername));
        }
    }

    IEnumerator MarkMessagesAsReadOnServer(string friendUsername)
    {
        string url = $"{ServerConfig.GetHttpUrl()}/messages/mark-read/{friendUsername}";

        using (UnityWebRequest request = UnityWebRequest.Post(url, ""))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[MessageNotificationManager] Marked messages from {friendUsername} as read on server");
            }
            else
            {
                Debug.LogError($"[MessageNotificationManager] Failed to mark messages as read on server: {request.error}");
            }
        }
    }

    void CheckForUnreadMessages()
    {
        if (!SessionData.IsValidSession())
        {
            Debug.Log("[MessageNotificationManager] Invalid session, skipping unread message check");
            return;
        }

        Debug.Log("[MessageNotificationManager] Checking for unread messages...");
        StartCoroutine(CheckForUnreadMessagesCoroutine());
    }

    IEnumerator CheckForUnreadMessagesCoroutine()
    {
        string url = $"{ServerConfig.GetHttpUrl()}/messages/unread-counts";

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
                    Debug.Log($"[MessageNotificationManager] Server response: {jsonResponse}");

                    var response = JsonUtility.FromJson<UnreadMessagesResponse>(jsonResponse);

                    // 기존 읽지 않은 개수 초기화
                    Dictionary<string, int> newUnreadCounts = new Dictionary<string, int>();

                    if (response.unread_messages != null)
                    {
                        Debug.Log($"[MessageNotificationManager] Found {response.unread_messages.Length} friends with unread messages");
                        foreach (var unreadData in response.unread_messages)
                        {
                            newUnreadCounts[unreadData.friend_username] = unreadData.unread_count;
                            Debug.Log($"[MessageNotificationManager] {unreadData.friend_username}: {unreadData.unread_count} unread messages");
                        }
                    }
                    else
                    {
                        Debug.Log("[MessageNotificationManager] No unread messages found");
                    }

                    // 변경된 경우에만 UI 업데이트
                    bool hasChanges = false;
                    foreach (var kvp in newUnreadCounts)
                    {
                        if (!unreadCounts.ContainsKey(kvp.Key) || unreadCounts[kvp.Key] != kvp.Value)
                        {
                            hasChanges = true;
                            break;
                        }
                    }

                    // 사라진 알림도 확인
                    if (!hasChanges)
                    {
                        foreach (var kvp in unreadCounts)
                        {
                            if (!newUnreadCounts.ContainsKey(kvp.Key) || newUnreadCounts[kvp.Key] == 0)
                            {
                                hasChanges = true;
                                break;
                            }
                        }
                    }

                    if (hasChanges)
                    {
                        Debug.Log("[MessageNotificationManager] Unread counts changed, updating UI");
                        unreadCounts = newUnreadCounts;
                        UpdateAllFriendNotifications();
                    }
                    else
                    {
                        Debug.Log("[MessageNotificationManager] No changes in unread counts");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing unread messages: {e.Message}");
                }
            }
            else if (request.responseCode != 401) // 401은 세션 만료이므로 로그하지 않음
            {
                Debug.LogError($"Failed to check unread messages: {request.error}");
            }
        }
    }

    void UpdateAllFriendNotifications()
    {
        Debug.Log($"[MessageNotificationManager] Updating notifications for {unreadCounts.Count} friends with unread messages");

        foreach (var kvp in unreadCounts)
        {
            UpdateFriendNotification(kvp.Key);
        }

        // 읽지 않은 메시지가 없는 친구들도 업데이트
        var allFriendItems = FriendItemUI.GetAllFriendItems();
        Debug.Log($"[MessageNotificationManager] Total friend items found: {allFriendItems.Count}");

        foreach (var friendItem in allFriendItems)
        {
            string username = friendItem.Key;
            if (!unreadCounts.ContainsKey(username) || unreadCounts[username] == 0)
            {
                UpdateFriendNotification(username);
            }
        }
    }

    void UpdateFriendNotification(string friendUsername)
    {
        var friendItem = FriendItemUI.GetFriendItemByUsername(friendUsername);
        if (friendItem != null)
        {
            int unreadCount = unreadCounts.ContainsKey(friendUsername) ? unreadCounts[friendUsername] : 0;
            bool shouldShow = unreadCount > 0;
            Debug.Log($"[MessageNotificationManager] Updating {friendUsername}: {unreadCount} unread messages, notification visible: {shouldShow}");
            friendItem.SetMessageNotificationVisible(shouldShow);
        }
        else
        {
            Debug.LogWarning($"[MessageNotificationManager] FriendItemUI not found for {friendUsername}");
        }
    }

    public static int GetUnreadCount(string friendUsername)
    {
        if (instance != null && instance.unreadCounts.ContainsKey(friendUsername))
        {
            return instance.unreadCounts[friendUsername];
        }
        return 0;
    }
}