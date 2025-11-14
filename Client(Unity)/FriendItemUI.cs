using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class FriendItemUI : MonoBehaviour
{
    // 정적 딕셔너리로 username -> FriendItemUI 매핑 관리
    private static Dictionary<string, FriendItemUI> friendItemsMap = new Dictionary<string, FriendItemUI>();
    [Header("UI Elements")]
    [SerializeField] TextMeshProUGUI usernameText;
    [SerializeField] TextMeshProUGUI eloText;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] Image statusIndicator;
    [SerializeField] Button inviteButton;
    [SerializeField] Button removeButton;
    [SerializeField] Button messageButton;
    [SerializeField] GameObject messageNotificationBadge; // 메시지 알림 빨간 원

    [Header("Battle Request Panel")]
    [SerializeField] GameObject battleRequestPanel;
    [SerializeField] Button rapidBattleButton;
    [SerializeField] Button blitzBattleButton;
    [SerializeField] Button cancelBattleButton;

    [Header("Status Message Panel")]
    [SerializeField] GameObject statusMessagePanel;
    [SerializeField] TextMeshProUGUI statusMessageText;
    [SerializeField] Button statusOkButton;

    [Header("Profile Image")]
    [SerializeField] Image profileImage;
    [SerializeField] Sprite defaultProfileSprite;

    private FriendData friendData;
    private static string currentSelectedFriend; // 현재 선택된 친구

    // FriendsScene의 공유 Unfriend 패널 (정적 참조)
    private static GameObject sharedRemoveConfirmationPanel;
    private static TextMeshProUGUI sharedRemoveConfirmationText;
    private static Button sharedConfirmRemoveButton;
    private static Button sharedCancelRemoveButton;
    private static string friendToRemove; // 삭제할 친구 이름 (정적 변수로 변경)

    public void SetFriendData(FriendData friend)
    {
        friendData = friend;

        // UI 컴포넌트들을 런타임에 자동으로 찾기 (Inspector 참조 문제 해결)
        TextMeshProUGUI[] textComponents = GetComponentsInChildren<TextMeshProUGUI>();
        if (textComponents.Length > 0)
        {
            usernameText = textComponents[0]; // 첫 번째 텍스트 컴포넌트를 username으로 사용
        }

        // 딕셔너리에 등록
        if (!string.IsNullOrEmpty(friend.username))
        {
            friendItemsMap[friend.username] = this;
        }

        UpdateUI();

        // 메시지 알림 매니저에 등록
        MessageNotificationManager.RegisterFriendItem(friend.username, this);
    }

    void UpdateUI()
    {
        if (friendData == null) return;

        if (usernameText != null)
        {
            usernameText.text = friendData.username;
        }

        if (eloText != null)
            eloText.text = $"R:{friendData.rapid_elo} B:{friendData.blitz_elo}";

        if (statusText != null)
            statusText.text = GetStatusDisplayText(friendData.status);

        if (statusIndicator != null)
            statusIndicator.color = GetStatusColor(friendData.status);

        // 프로필 이미지 설정
        SetDefaultProfileImage();

        // 메시지 알림 뱃지 초기에는 숨김
        SetMessageNotificationVisible(false);
        LoadProfileImage();

        // 버튼 설정
        if (inviteButton != null)
        {
            inviteButton.onClick.RemoveAllListeners();
            inviteButton.onClick.AddListener(() => OnInviteButtonClicked(friendData.username));
        }

        if (removeButton != null)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(() => OnRemoveButtonClicked(friendData.username));
        }

        if (messageButton != null)
        {
            messageButton.onClick.RemoveAllListeners();
            string messageTarget = friendData.username;
            messageButton.onClick.AddListener(() => OnMessageButtonClicked(messageTarget));
        }

        // 대전 버튼도 메시지 버튼과 완전히 동일한 방식으로 설정
        if (rapidBattleButton != null)
        {
            rapidBattleButton.onClick.RemoveAllListeners();
            string rapidTarget = friendData.username;
            rapidBattleButton.onClick.AddListener(() => OnRapidBattleButtonClicked(rapidTarget));
        }

        if (blitzBattleButton != null)
        {
            blitzBattleButton.onClick.RemoveAllListeners();
            string blitzTarget = friendData.username;
            blitzBattleButton.onClick.AddListener(() => OnBlitzBattleButtonClicked(blitzTarget));
        }

        // 대전 버튼 설정은 위에서 이미 처리됨

        // 나머지 배틀 패널 설정 (기본 상태, 취소 버튼 등)
        SetupBattleRequestPanel();
    }

    void SetupBattleButtonsForCurrentFriend()
    {
        // Rapid 버튼 재설정
        if (rapidBattleButton != null)
        {
            rapidBattleButton.onClick.RemoveAllListeners();
            rapidBattleButton.onClick.AddListener(() => OnRapidBattleButtonClicked(currentSelectedFriend));
        }

        // Blitz 버튼 재설정
        if (blitzBattleButton != null)
        {
            blitzBattleButton.onClick.RemoveAllListeners();
            blitzBattleButton.onClick.AddListener(() => OnBlitzBattleButtonClicked(currentSelectedFriend));
        }
    }

    string GetStatusDisplayText(string status)
    {
        switch (status)
        {
            case "online": return "Online";
            case "in_game": return "In Game";
            case "offline": return "Offline";
            default: return "Unknown";
        }
    }

    Color GetStatusColor(string status)
    {
        switch (status)
        {
            case "online": return Color.green;
            case "in_game": return Color.yellow;
            case "offline": return Color.gray;
            default: return Color.white;
        }
    }

    void OnInviteButtonClicked(string friendUsername)
    {
        // 현재 선택된 친구 정보를 저장
        currentSelectedFriend = friendUsername;

        // 대전 요청 패널 표시 후 버튼 재설정
        if (battleRequestPanel != null)
        {
            battleRequestPanel.SetActive(true);
            SetupBattleButtonsForCurrentFriend();
        }
    }

    void OnRemoveButtonClicked(string friendUsername)
    {
        Debug.Log($"[FriendItemUI] OnRemoveButtonClicked called for: {friendUsername}");

        // 삭제할 친구 이름 저장
        friendToRemove = friendUsername;

        // 확인 패널 표시
        ShowRemoveConfirmation(friendUsername);
    }

    void OnMessageButtonClicked(string friendUsername)
    {
        // 메시지를 읽음으로 표시 (ChatScene 이동 전에)
        MessageNotificationManager.MarkMessagesAsRead(friendUsername);

        // ChatManager에 친구 사용자명 설정
        ChatManager.TargetFriendUsername = friendUsername;

        // ChatScene으로 이동
        SceneManager.LoadScene("ChatScene");
    }

    void SetDefaultProfileImage()
    {
        if (profileImage != null && defaultProfileSprite != null)
        {
            profileImage.sprite = defaultProfileSprite;
        }
    }

    void LoadProfileImage()
    {
        if (friendData == null || string.IsNullOrEmpty(friendData.username))
            return;

        StartCoroutine(LoadProfileImageCoroutine());
    }

    IEnumerator LoadProfileImageCoroutine()
    {
        string url = $"{ServerConfig.GetHttpUrl()}/profile-image/{GetUserIdByUsername(friendData.username)}";

        // 먼저 친구의 사용자 ID를 가져와야 함
        yield return StartCoroutine(GetUserIdAndLoadImage());
    }

    IEnumerator GetUserIdAndLoadImage()
    {
        // 친구의 사용자 ID를 가져오기 위해 /user/{username} 호출
        string userUrl = $"{ServerConfig.GetHttpUrl()}/user/{friendData.username}";

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
                }

                if (userResponse != null)
                {
                    yield return StartCoroutine(LoadProfileImageById(userResponse.id));
                }
            }
            else
            {
            }
        }
    }

    IEnumerator LoadProfileImageById(int userId)
    {
        string imageUrl = $"{ServerConfig.GetHttpUrl()}/profile-image/{userId}";

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);

                if (texture != null && profileImage != null)
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    profileImage.sprite = sprite;
                }
            }
            else
            {
                // 실패 시 기본 이미지 유지
            }
        }
    }

    // 사용자명으로 사용자 ID를 가져오는 임시 메서드 (실제로는 위의 코루틴에서 처리됨)
    int GetUserIdByUsername(string username)
    {
        // 이 메서드는 실제로 사용되지 않지만, 컴파일 에러를 피하기 위해 남겨둠
        return 0;
    }

    void SetupBattleRequestPanel()
    {
        // 대전 요청 패널 초기에는 비활성화
        if (battleRequestPanel != null)
        {
            battleRequestPanel.SetActive(false);
        }

        // 상태 메시지 패널 초기에는 비활성화
        if (statusMessagePanel != null)
        {
            statusMessagePanel.SetActive(false);
        }

        // 대전 버튼은 이제 UpdateUI()에서 설정되므로 여기서는 설정하지 않음

        // Cancel 버튼 설정
        if (cancelBattleButton != null)
        {
            cancelBattleButton.onClick.RemoveAllListeners();
            cancelBattleButton.onClick.AddListener(() => OnCancelBattleButtonClicked());
        }

        // 상태 메시지 OK 버튼 설정
        if (statusOkButton != null)
        {
            statusOkButton.onClick.RemoveAllListeners();
            statusOkButton.onClick.AddListener(() => OnStatusOkButtonClicked());
        }
    }

    void OnRapidBattleButtonClicked(string targetUsername)
    {
        if (string.IsNullOrEmpty(targetUsername))
        {
            return;
        }

        // FightSocketManager를 통해 Rapid 대전 요청 전송
        if (FightSocketManager.Instance != null)
        {
            FightSocketManager.Instance.SendBattleRequest(targetUsername, "rapid");
        }

        // 패널 닫기
        if (battleRequestPanel != null)
        {
            battleRequestPanel.SetActive(false);
        }
    }

    void OnBlitzBattleButtonClicked(string targetUsername)
    {
        if (string.IsNullOrEmpty(targetUsername))
        {
            return;
        }

        // FightSocketManager를 통해 Blitz 대전 요청 전송
        if (FightSocketManager.Instance != null)
        {
            FightSocketManager.Instance.SendBattleRequest(targetUsername, "blitz");
        }

        // 패널 닫기
        if (battleRequestPanel != null)
        {
            battleRequestPanel.SetActive(false);
        }
    }

    void OnCancelBattleButtonClicked()
    {
        // 패널 닫기
        if (battleRequestPanel != null)
        {
            battleRequestPanel.SetActive(false);
        }
    }

    void OnStatusOkButtonClicked()
    {
        // 상태 메시지 패널 닫기
        if (statusMessagePanel != null)
        {
            statusMessagePanel.SetActive(false);
        }
    }

    public void ShowStatusMessage(string status)
    {
        string message = "";
        switch (status)
        {
            case "offline":
                message = "Your friend is offline.";
                break;
            case "in_game":
                message = "Your friend is in battle.";
                break;
            case "online":
                message = "Sent request!";
                break;
            default:
                message = $"Friend status: {status}";
                break;
        }

        // 상태 메시지 패널 표시
        if (statusMessagePanel != null)
        {
            statusMessagePanel.SetActive(true);
        }

        if (statusMessageText != null)
        {
            statusMessageText.text = message;
        }

    }

    // 특정 username에 해당하는 FriendItemUI 찾기
    public static FriendItemUI GetFriendItemByUsername(string username)
    {
        if (friendItemsMap.ContainsKey(username))
        {
            return friendItemsMap[username];
        }
        return null;
    }

    // 모든 FriendItemUI 가져오기 (MessageNotificationManager용)
    public static Dictionary<string, FriendItemUI> GetAllFriendItems()
    {
        return new Dictionary<string, FriendItemUI>(friendItemsMap);
    }

    // 메시지 알림 뱃지 표시/숨김
    public void SetMessageNotificationVisible(bool visible)
    {
        if (messageNotificationBadge != null)
        {
            messageNotificationBadge.SetActive(visible);
        }
    }

    void OnDestroy()
    {
        // 오브젝트 파괴 시 딕셔너리에서 제거
        if (friendData != null && !string.IsNullOrEmpty(friendData.username))
        {
            if (friendItemsMap.ContainsKey(friendData.username))
            {
                friendItemsMap.Remove(friendData.username);
            }
        }
    }

    // ========== 친구 삭제 확인 패널 관련 메서드 (정적) ==========

    /// <summary>
    /// FriendsScene의 공유 Unfriend 패널 등록
    /// FriendsManager에서 호출되어야 함
    /// </summary>
    public static void RegisterRemoveConfirmationPanel(
        GameObject panel,
        TextMeshProUGUI text,
        Button confirmButton,
        Button cancelButton)
    {
        sharedRemoveConfirmationPanel = panel;
        sharedRemoveConfirmationText = text;
        sharedConfirmRemoveButton = confirmButton;
        sharedCancelRemoveButton = cancelButton;

        // 패널 초기에는 비활성화
        if (sharedRemoveConfirmationPanel != null)
        {
            sharedRemoveConfirmationPanel.SetActive(false);
        }

        // Confirm 버튼 설정
        if (sharedConfirmRemoveButton != null)
        {
            sharedConfirmRemoveButton.onClick.RemoveAllListeners();
            sharedConfirmRemoveButton.onClick.AddListener(() => OnConfirmRemoveButtonClickedStatic());
        }

        // Cancel 버튼 설정
        if (sharedCancelRemoveButton != null)
        {
            sharedCancelRemoveButton.onClick.RemoveAllListeners();
            sharedCancelRemoveButton.onClick.AddListener(() => OnCancelRemoveButtonClickedStatic());
        }

        Debug.Log("[FriendItemUI] Shared remove confirmation panel registered");
    }

    void ShowRemoveConfirmation(string friendUsername)
    {
        Debug.Log($"[FriendItemUI] ShowRemoveConfirmation called for: {friendUsername}");
        Debug.Log($"[FriendItemUI] sharedRemoveConfirmationPanel is null: {sharedRemoveConfirmationPanel == null}");

        if (sharedRemoveConfirmationPanel == null)
        {
            Debug.LogError("[FriendItemUI] Shared remove confirmation panel not registered! Make sure FriendsManager registered the panel.");
            return;
        }

        Debug.Log($"[FriendItemUI] Panel active before SetActive: {sharedRemoveConfirmationPanel.activeSelf}");

        // 패널 표시
        sharedRemoveConfirmationPanel.SetActive(true);

        Debug.Log($"[FriendItemUI] Panel active after SetActive: {sharedRemoveConfirmationPanel.activeSelf}");

        // 텍스트 설정
        if (sharedRemoveConfirmationText != null)
        {
            sharedRemoveConfirmationText.text = $"Do you want to unfriend {friendUsername}?";
            Debug.Log($"[FriendItemUI] Text set to: {sharedRemoveConfirmationText.text}");
        }
        else
        {
            Debug.LogError("[FriendItemUI] sharedRemoveConfirmationText is null!");
        }

        Debug.Log($"[FriendItemUI] Successfully showing remove confirmation for: {friendUsername}");
    }

    static void OnConfirmRemoveButtonClickedStatic()
    {
        if (string.IsNullOrEmpty(friendToRemove))
        {
            Debug.LogError("[FriendItemUI] Friend to remove is null or empty!");
            return;
        }

        Debug.Log($"[FriendItemUI] Confirmed removal of friend: {friendToRemove}");

        // FriendsManager에서 삭제 처리 (타이밍 문제 방지)
        FriendsManager.RemoveFriendStatic(friendToRemove);

        // 패널 닫기
        if (sharedRemoveConfirmationPanel != null)
        {
            sharedRemoveConfirmationPanel.SetActive(false);
        }

        // 삭제할 친구 이름 초기화
        friendToRemove = null;
    }

    static void OnCancelRemoveButtonClickedStatic()
    {
        Debug.Log("[FriendItemUI] Cancelled friend removal");

        // 패널 닫기
        if (sharedRemoveConfirmationPanel != null)
        {
            sharedRemoveConfirmationPanel.SetActive(false);
        }

        // 삭제할 친구 이름 초기화
        friendToRemove = null;
    }
}

