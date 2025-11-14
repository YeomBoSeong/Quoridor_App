using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendSceneUIController : MonoBehaviour
{
    [Header("Friend Request UI")]
    [SerializeField] GameObject friendRequestListPanel;
    [SerializeField] Transform requestItemParent;
    [SerializeField] GameObject requestItemPrefab;
    [SerializeField] Button closeButton;

    void Start()
    {
        // DontDestroyOnLoad 객체에 자신을 등록
        if (FriendRequestListPanel.GetInstance() != null)
        {
            FriendRequestListPanel.GetInstance().RegisterUIController(this);
            Debug.Log("[FriendSceneUIController] Registered with FriendRequestListPanel");
        }
        else
        {
            Debug.LogError("[FriendSceneUIController] FriendRequestListPanel instance not found!");
        }

        SetupUI();
    }

    void OnDestroy()
    {
        // 등록 해제
        if (FriendRequestListPanel.GetInstance() != null)
        {
            FriendRequestListPanel.GetInstance().UnregisterUIController();
        }
    }

    void SetupUI()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(HideRequestPanel);

        // 패널 초기에는 비활성화
        if (friendRequestListPanel != null)
            friendRequestListPanel.SetActive(false);
    }

    public void ShowRequestPanel()
    {
        if (friendRequestListPanel != null)
        {
            friendRequestListPanel.SetActive(true);
            Debug.Log("[FriendSceneUIController] Request panel shown");
        }
        else
        {
            Debug.LogError("[FriendSceneUIController] friendRequestListPanel is null!");
        }
    }

    public void HideRequestPanel()
    {
        if (friendRequestListPanel != null)
        {
            friendRequestListPanel.SetActive(false);
            Debug.Log("[FriendSceneUIController] Request panel hidden");
        }
    }

    public Transform GetRequestItemParent()
    {
        return requestItemParent;
    }

    public GameObject GetRequestItemPrefab()
    {
        return requestItemPrefab;
    }

    public bool IsUIReady()
    {
        return friendRequestListPanel != null && requestItemParent != null;
    }
}