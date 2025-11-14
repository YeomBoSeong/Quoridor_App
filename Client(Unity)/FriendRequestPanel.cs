using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Text;

[System.Serializable]
public class FriendRequestData
{
    public int id;
    public string sender_username;
    public int sender_rapid_elo;
    public int sender_blitz_elo;
    public string created_at;
}

[System.Serializable]
public class FriendRequestResponse
{
    public string message;
    public int request_id;
}

public class FriendRequestPanel : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] GameObject friendRequestPanel;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] Button outButton;

    [Header("Notification Settings")]
    [SerializeField] float autoHideDelay = 3f;

    private bool isNotificationPanel = false;

    private static FriendRequestPanel instance;

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
        HidePanel();
    }

    void SetupUI()
    {
        if (outButton != null)
            outButton.onClick.AddListener(OnOutClicked);
    }

    public static void ShowNotification(string message)
    {
        if (instance != null)
        {
            instance.ShowNotificationPanel(message);
        }
    }


    void ShowNotificationPanel(string message)
    {
        isNotificationPanel = true;

        if (messageText != null)
            messageText.text = message;

        if (outButton != null)
            outButton.gameObject.SetActive(true);

        if (friendRequestPanel != null)
            friendRequestPanel.SetActive(true);

        // 자동으로 패널 숨기기
        StartCoroutine(AutoHidePanel());
    }


    void HidePanel()
    {
        if (friendRequestPanel != null)
            friendRequestPanel.SetActive(false);
    }

    IEnumerator AutoHidePanel()
    {
        yield return new WaitForSeconds(autoHideDelay);

        if (isNotificationPanel)
        {
            HidePanel();
        }
    }

    void OnOutClicked()
    {
        HidePanel();
    }

}

[System.Serializable]
public class FriendRequestsResponse
{
    public FriendRequestData[] requests;
}