using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class FriendRequestListPanel : MonoBehaviour
{
    [Header("UI Elements - Deprecated (now uses UIController)")]
    [SerializeField] GameObject requestListPanel;
    [SerializeField] Transform requestItemParent;
    [SerializeField] GameObject requestItemPrefab;
    [SerializeField] Button closeButton;

    private static FriendRequestListPanel instance;
    private List<GameObject> requestItems = new List<GameObject>();
    private FriendSceneUIController currentUIController;

    public static FriendRequestListPanel GetInstance()
    {
        return instance;
    }

    public void RegisterUIController(FriendSceneUIController uiController)
    {
        currentUIController = uiController;
    }

    public void UnregisterUIController()
    {
        currentUIController = null;
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            // 씬 변경 감지를 위한 이벤트 등록
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnDestroy()
    {
        // 이벤트 해제
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 씬이 변경되면 UI 참조 다시 찾기
        if (scene.name == "FriendScene" || scene.name.Contains("Friend"))
        {
            StartCoroutine(RefreshUIReferencesDelayed());
        }
    }

    System.Collections.IEnumerator RefreshUIReferencesDelayed()
    {
        // 한 프레임 대기 (씬이 완전히 로드될 때까지)
        yield return null;
        RefreshUIReferences();
    }

    void Start()
    {
        SetupUI();
        HidePanel();
    }

    void SetupUI()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);
    }

    void RefreshUIReferences()
    {

        // 씬의 모든 GameObject 확인 (디버깅용)
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.ToLower().Contains("request") || obj.name.ToLower().Contains("friend") || obj.name.ToLower().Contains("panel"))
            {
            }
        }

        // UI 요소들을 더 광범위하게 찾기
        if (requestListPanel == null)
        {
            // 모든 가능한 이름으로 찾기
            string[] panelNames = {
                "FriendRequestListPanel", "FriendRequestPanel", "RequestListPanel", "RequestPanel",
                "FriendRequest", "Request", "Panel", "FriendPanel"
            };

            foreach (string panelName in panelNames)
            {
                GameObject found = GameObject.Find(panelName);
                if (found != null)
                {
                    requestListPanel = found;
                    break;
                }
            }

            // GameObject.Find가 실패한 경우 FindObjectsOfType으로 찾기
            if (requestListPanel == null)
            {
                GameObject[] panels = FindObjectsOfType<GameObject>();
                foreach (GameObject panel in panels)
                {
                    if (panel.name.ToLower().Contains("request") && panel.name.ToLower().Contains("panel"))
                    {
                        requestListPanel = panel;
                        break;
                    }
                }
            }

            if (requestListPanel == null)
            {
            }
        }

        if (requestItemParent == null && requestListPanel != null)
        {
            // 자식에서 Content 찾기
            string[] contentNames = {"Content", "ScrollContent", "ItemParent", "Items", "List"};

            foreach (string contentName in contentNames)
            {
                Transform found = requestListPanel.transform.Find(contentName);
                if (found == null)
                {
                    // 깊은 검색
                    Transform[] allChildren = requestListPanel.GetComponentsInChildren<Transform>(true);
                    foreach (Transform child in allChildren)
                    {
                        if (child.name.Equals(contentName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            found = child;
                            break;
                        }
                    }
                }

                if (found != null)
                {
                    requestItemParent = found;
                    break;
                }
            }

            if (requestItemParent == null)
            {
            }
        }

        if (closeButton == null && requestListPanel != null)
        {
            // 모든 Button 컴포넌트 찾기
            Button[] buttons = requestListPanel.GetComponentsInChildren<Button>(true);

            foreach (Button button in buttons)
            {
                string buttonName = button.name.ToLower();
                if (buttonName.Contains("close") || buttonName.Contains("exit") || buttonName.Contains("x") || buttonName.Contains("back"))
                {
                    closeButton = button;
                    break;
                }
            }

            // 첫 번째 버튼을 close 버튼으로 사용 (fallback)
            if (closeButton == null && buttons.Length > 0)
            {
                closeButton = buttons[0];
            }
        }

        // UI 설정 다시 적용
        SetupUI();

        // 최종 상태 로깅
    }

    public static void ShowRequestList()
    {
        if (instance != null)
        {
            if (instance.currentUIController != null && instance.currentUIController.IsUIReady())
            {
                instance.LoadAndShowRequests();
            }
            else
            {
            }
        }
        else
        {
        }
    }

    void LoadAndShowRequests()
    {
        StartCoroutine(LoadRequestsCoroutine());
    }

    IEnumerator LoadRequestsCoroutine()
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

                    UpdateRequestList(response.requests);
                    ShowPanel();
                }
                catch (System.Exception e)
                {
                }
            }
            else
            {
            }
        }
    }

    void UpdateRequestList(FriendRequestData[] requests)
    {
        // 기존 아이템들 제거
        foreach (GameObject item in requestItems)
        {
            if (item != null)
                Destroy(item);
        }
        requestItems.Clear();

        if (requests != null && requests.Length > 0)
        {
            // 요청 아이템들 생성
            foreach (FriendRequestData request in requests)
            {
                CreateRequestItem(request);
            }
        }
    }

    void CreateRequestItem(FriendRequestData request)
    {

        Transform parentTransform = null;
        GameObject prefab = null;

        // UI Controller를 통해 참조 가져오기
        if (currentUIController != null)
        {
            parentTransform = currentUIController.GetRequestItemParent();
            prefab = currentUIController.GetRequestItemPrefab();
        }
        else
        {
            // Fallback: 기존 방식
            parentTransform = requestItemParent;
            prefab = requestItemPrefab;
        }

        if (prefab == null || parentTransform == null)
        {
            return;
        }

        GameObject requestItem = Instantiate(prefab, parentTransform);
        requestItems.Add(requestItem);

        // 요청 아이템 UI 설정
        SetupBasicRequestItem(requestItem, request);
    }

    void SetupBasicRequestItem(GameObject item, FriendRequestData request)
    {

        // 기본적인 텍스트와 버튼 설정
        TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>();
        Button[] buttons = item.GetComponentsInChildren<Button>();

        // 첫 번째 텍스트: 유저네임
        if (texts.Length > 0)
            texts[0].text = request.sender_username;

        // 두 번째 텍스트: ELO 정보
        if (texts.Length >= 2)
            texts[1].text = $"R:{request.sender_rapid_elo} B:{request.sender_blitz_elo}";

        // 프로필 이미지 로딩
        Image[] images = item.GetComponentsInChildren<Image>(true);

        Image profileImage = null;
        foreach (Image img in images)
        {
            string imgName = img.name.ToLower();
            if (imgName.Contains("profile") || imgName.Contains("avatar") ||
                imgName.Contains("icon") || imgName.Contains("picture"))
            {
                profileImage = img;
                break;
            }
        }

        // 못 찾으면 첫 번째 Image 사용
        if (profileImage == null && images.Length > 0)
        {
            profileImage = images[0];
        }

        // 프로필 이미지 로드
        if (profileImage != null)
        {
            StartCoroutine(LoadProfileImageForBasicItem(profileImage, request.sender_username));
        }
        else
        {
        }

        if (buttons.Length >= 2)
        {
            // 첫 번째 버튼: 체크 버튼 (수락)
            buttons[0].onClick.RemoveAllListeners();
            buttons[0].onClick.AddListener(() => {
                OnAcceptRequest(request.id);
            });

            // 두 번째 버튼: X 버튼 (거절)
            buttons[1].onClick.RemoveAllListeners();
            buttons[1].onClick.AddListener(() => {
                OnRejectRequest(request.id);
            });
        }
    }

    IEnumerator LoadProfileImageForBasicItem(Image profileImage, string username)
    {

        // 1단계: 사용자 ID 가져오기
        string userUrl = $"{ServerConfig.GetHttpUrl()}/user/{username}";

        using (UnityWebRequest request = UnityWebRequest.Get(userUrl))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();


            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;

                MeResponse userResponse = null;
                try
                {
                    userResponse = JsonUtility.FromJson<MeResponse>(responseText);
                }
                catch (System.Exception e)
                {
                    yield break;
                }

                if (userResponse != null)
                {
                    // 2단계: 프로필 이미지 다운로드
                    yield return StartCoroutine(DownloadProfileImage(profileImage, userResponse.id, username));
                }
            }
            else
            {
            }
        }
    }

    IEnumerator DownloadProfileImage(Image profileImage, int userId, string username)
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
                else
                {
                }
            }
            else
            {
            }
        }
    }

    void OnAcceptRequest(int requestId)
    {
        StartCoroutine(AcceptRequestCoroutine(requestId));
    }

    void OnRejectRequest(int requestId)
    {
        StartCoroutine(RejectRequestCoroutine(requestId));
    }

    IEnumerator AcceptRequestCoroutine(int requestId)
    {
        string url = $"{ServerConfig.GetHttpUrl()}/friends/request/{requestId}/accept";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(new byte[0]);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {

                // 목록 새로고침
                LoadAndShowRequests();

                // 뱃지 업데이트
                FriendNotificationBadge.HideBadge();

                // 친구 목록 새로고침
                FriendsManager.RefreshFriendsListStatic();
            }
            else
            {
            }
        }
    }

    IEnumerator RejectRequestCoroutine(int requestId)
    {
        string url = $"{ServerConfig.GetHttpUrl()}/friends/request/{requestId}/reject";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(new byte[0]);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {

                // 목록 새로고침
                LoadAndShowRequests();

                // 뱃지 업데이트
                FriendNotificationBadge.HideBadge();

                // 친구 목록 새로고침 (혹시 모르니까)
                FriendsManager.RefreshFriendsListStatic();
            }
            else
            {
            }
        }
    }

    void ShowPanel()
    {
        if (currentUIController != null)
        {
            currentUIController.ShowRequestPanel();
        }
        else
        {
        }
    }

    void HidePanel()
    {
        if (currentUIController != null)
        {
            currentUIController.HideRequestPanel();
        }
        else if (requestListPanel != null)
        {
            requestListPanel.SetActive(false);
        }
    }
}