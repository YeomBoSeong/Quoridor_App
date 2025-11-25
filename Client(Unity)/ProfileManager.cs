using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;

[System.Serializable]
public class ProfileData
{
    public string username;
    public int rapid_elo;
    public int blitz_elo;
    public float rapid_percentile;
    public float blitz_percentile;
    public string profile_image_url;
}

public class ProfileManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI rapidText;
    [SerializeField] private TextMeshProUGUI blitzText;
    [SerializeField] private Button backButton;

    [Header("Game History Elements")]
    [SerializeField] private GameObject gameHistoryItemPrefab;
    [SerializeField] private Transform gameHistoryContent;
    [SerializeField] private ScrollRect gameHistoryScrollRect;
    
    [Header("Profile Image Elements")]
    [SerializeField] private Button profileImageButton;
    [SerializeField] private Sprite defaultProfileSprite;
    
    private UnityEngine.UI.Image profileImage;
    
    [Header("Session Warning UI")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private TextMeshProUGUI warningText;

    private List<GameObject> gameHistoryItems = new List<GameObject>();
    
    void Start()
    {
        // 백 버튼 이벤트 설정
        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClick);
            
        // 프로필 이미지 버튼의 Image 컴포넌트 가져오기
        if (profileImageButton != null)
        {
            profileImage = profileImageButton.GetComponent<UnityEngine.UI.Image>();
            profileImageButton.onClick.AddListener(OnProfileImageClick);
        }
        
        // 기본 프로필 이미지 설정
        SetDefaultProfileImage();
        
        // 프로필 데이터 로드
        StartCoroutine(LoadProfileData());

        // GameHistoryManager 초기화 및 이벤트 구독
        InitializeGameHistoryManager();

        // 게임 히스토리 로드
        LoadGameHistory();
    }
    
    IEnumerator LoadProfileData()
    {
        // 세션 검증
        yield return StartCoroutine(ValidateSessionAndProceed(() => 
        {
            StartCoroutine(FetchProfileFromServer());
        }));
    }
    
    IEnumerator FetchProfileFromServer()
    {
        string serverUrl = ServerConfig.GetHttpUrl();
        string endpoint = "/profile";
        string fullUrl = serverUrl + endpoint;
        
        string token = SessionData.GetToken();
        if (string.IsNullOrEmpty(token))
        {
            ShowError("No authentication token found");
            yield break;
        }
        
        using (UnityWebRequest request = UnityWebRequest.Get(fullUrl))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 10;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    ProfileData profileData = JsonUtility.FromJson<ProfileData>(jsonResponse);
                    
                    DisplayProfileData(profileData);
                }
                catch (Exception e)
                {
                    ShowError($"Failed to parse profile data: {e.Message}");
                }
            }
            else
            {
                ShowError($"Failed to load profile: {request.error}");
            }
        }
    }
    
    void DisplayProfileData(ProfileData data)
    {
        if (nameText != null)
            nameText.text = $"Name : {data.username}";
            
        if (rapidText != null)
            rapidText.text = $"Rapid : {data.rapid_elo} ({data.rapid_percentile:F1}%)";
            
        if (blitzText != null)
            blitzText.text = $"Blitz : {data.blitz_elo} ({data.blitz_percentile:F1}%)";
            
        // 프로필 이미지 로드
        if (!string.IsNullOrEmpty(data.profile_image_url))
        {
            StartCoroutine(LoadProfileImage(data.profile_image_url));
        }
    }
    
    void ShowError(string message)
    {
        if (nameText != null)
            nameText.text = "Error loading profile";
        if (rapidText != null)
            rapidText.text = message;
        if (blitzText != null)
            blitzText.text = "";
    }
    
    public void OnBackButtonClick()
    {
        SceneManager.LoadScene("StartScene");
    }
    
    // 세션 검증을 위한 /me 엔드포인트 호출
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
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.GetToken()}");
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
    
    void SetDefaultProfileImage()
    {
        if (profileImage != null && defaultProfileSprite != null)
        {
            profileImage.sprite = defaultProfileSprite;
        }
    }
    
    public void OnProfileImageClick()
    {
        // 이미지 선택 옵션 표시
        ShowImagePickerOptions();
    }
    
    void ShowImagePickerOptions()
    {
        // 간단한 선택 다이얼로그 (실제로는 UI Panel로 구현하는 것이 좋음)
        if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            // 모바일에서는 갤러리와 카메라 선택 옵션 제공
            // 여기서는 갤러리만 구현
            PickImageFromGallery();
        }
        else
        {
            // PC/에디터에서는 파일 선택
            PickImageFromGallery();
        }
    }
    
    void PickImageFromGallery()
    {
        if (NativeImagePicker.Instance != null)
        {
            NativeImagePicker.Instance.PickImageFromGallery(
                onSuccess: OnImageSelected,
                onError: OnImageSelectionFailed
            );
        }
        else
        {
        }
    }
    
    void OnImageSelected(Texture2D selectedImage)
    {
        if (selectedImage != null)
        {
            // 이미지 크기 조절 (프로필 이미지에 적합한 크기로)
            Texture2D resizedImage = NativeImagePicker.ResizeTexture(selectedImage, 512, 512);
            
            // UI에 즉시 반영
            if (profileImage != null)
            {
                Sprite newSprite = Sprite.Create(resizedImage, new Rect(0, 0, resizedImage.width, resizedImage.height), new Vector2(0.5f, 0.5f));
                profileImage.sprite = newSprite;
            }
            
            // 서버에 업로드
            StartCoroutine(UploadProfileImage(resizedImage));
        }
    }
    
    void OnImageSelectionFailed(string error)
    {
        
        // "No file selected"는 사용자가 취소한 것이므로 오류가 아님
        if (error != "No file selected")
        {
            ShowError($"Failed to select image: {error}");
        }
        // 취소한 경우에는 아무것도 표시하지 않음
    }
    
    IEnumerator UploadProfileImage(Texture2D imageTexture)
    {
        string serverUrl = ServerConfig.GetHttpUrl();
        string endpoint = "/upload-profile-image";
        string fullUrl = serverUrl + endpoint;
        
        string token = SessionData.GetToken();
        if (string.IsNullOrEmpty(token))
        {
            ShowError("No authentication token found");
            yield break;
        }
        
        // 이미지를 PNG 바이트 배열로 변환
        byte[] imageData = imageTexture.EncodeToPNG();
        
        // multipart/form-data 형식으로 업로드
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageData, "profile.png", "image/png");
        
        using (UnityWebRequest request = UnityWebRequest.Post(fullUrl, form))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 30; // 업로드는 시간이 더 걸릴 수 있음
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
            }
            else
            {
                ShowError("Failed to upload image to server");
                // 실패 시 기본 이미지로 되돌리기
                SetDefaultProfileImage();
            }
        }
    }
    
    IEnumerator LoadProfileImage(string imageUrl)
    {
        string fullUrl = ServerConfig.GetHttpUrl() + imageUrl;
        
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
        {
            request.timeout = 10;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D downloadedTexture = DownloadHandlerTexture.GetContent(request);
                
                if (downloadedTexture != null && profileImage != null)
                {
                    Sprite newSprite = Sprite.Create(downloadedTexture, 
                        new Rect(0, 0, downloadedTexture.width, downloadedTexture.height), 
                        new Vector2(0.5f, 0.5f));
                    profileImage.sprite = newSprite;
                }
            }
            else
            {
                // 로드 실패 시 기본 이미지 유지
            }
        }
    }

    // ========== 게임 히스토리 관련 메서드들 ==========

    void InitializeGameHistoryManager()
    {

        // GameHistoryManager가 없으면 생성
        if (GameHistoryManager.Instance == null)
        {
            GameObject managerGO = new GameObject("GameHistoryManager");
            managerGO.AddComponent<GameHistoryManager>();
        }
        else
        {
        }

        // 이벤트 구독
        if (GameHistoryManager.Instance != null)
        {
            GameHistoryManager.Instance.OnGameHistoryLoaded += OnGameHistoryLoaded;
            GameHistoryManager.Instance.OnError += OnGameHistoryError;
        }
        else
        {
        }
    }

    void LoadGameHistory()
    {

        if (GameHistoryManager.Instance != null)
        {
            GameHistoryManager.Instance.LoadGameHistory();
        }
        else
        {
        }
    }

    void OnGameHistoryLoaded(GameHistoryData[] gameHistories)
    {
        DisplayGameHistory(gameHistories);
    }

    void OnGameHistoryError(string errorMessage)
    {
        // TODO: UI에 에러 메시지 표시
    }

    void DisplayGameHistory(GameHistoryData[] gameHistories)
    {

        // 기존 게임 히스토리 아이템들 제거
        ClearGameHistoryItems();

        if (gameHistoryContent == null)
        {
            return;
        }

        if (gameHistoryItemPrefab == null)
        {
            return;
        }


        // 각 게임 기록에 대해 UI 아이템 생성
        foreach (GameHistoryData gameData in gameHistories)
        {
            CreateGameHistoryItem(gameData);
        }

    }

    void CreateGameHistoryItem(GameHistoryData gameData)
    {

        GameObject historyItem = Instantiate(gameHistoryItemPrefab, gameHistoryContent);
        gameHistoryItems.Add(historyItem);


        // 게임 히스토리 아이템 컴포넌트 설정
        GameHistoryItem itemComponent = historyItem.GetComponent<GameHistoryItem>();
        if (itemComponent != null)
        {
            itemComponent.SetGameData(gameData);
        }
        else
        {
        }
    }

    void ClearGameHistoryItems()
    {
        foreach (GameObject item in gameHistoryItems)
        {
            if (item != null)
                Destroy(item);
        }
        gameHistoryItems.Clear();
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (GameHistoryManager.Instance != null)
        {
            GameHistoryManager.Instance.OnGameHistoryLoaded -= OnGameHistoryLoaded;
            GameHistoryManager.Instance.OnError -= OnGameHistoryError;
        }
    }
}