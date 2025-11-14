using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;
using System;

public class TimeControlManager : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] string gameSceneName = "PlayScene";
    [SerializeField] string aiGameSceneName = "AIScene";
    [SerializeField] string startSceneName = "StartScene";

    [Header("UI")]
    [SerializeField] GameObject rapidButton;
    [SerializeField] GameObject blitzButton;
    [SerializeField] GameObject statusPanel;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] GameObject cancelButton;
    [SerializeField] Button backButton;

    [Header("Game Credits UI")]
    [SerializeField] TextMeshProUGUI availableGamesText;
    [SerializeField] Button watchVideoButton;

    [Header("AI Difficulty Panel")]
    [SerializeField] GameObject aiDifficultyPanel;
    [SerializeField] Button easyButton;
    [SerializeField] Button mediumButton;
    [SerializeField] Button difficultButton;
    [SerializeField] Button difficultyPanelCancelButton;
    
    [Header("Session Warning UI")]
    [SerializeField] GameObject warningPanel;
    [SerializeField] TextMeshProUGUI warningText;
    
    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellationTokenSource;
    private bool isConnecting = false;
    private bool isMatching = false;
    
    // Unity 메인 스레드 동기화용
    private System.Collections.Generic.Queue<System.Action> mainThreadActions = new System.Collections.Generic.Queue<System.Action>();
    
    // 매치 결과 저장용
    public static string playerColor;
    public static string opponentName;
    public static string gameToken;
    public static string opponentELO;
    public static string myCurrentELO;
    public static ClientWebSocket gameWebSocket;
    public static string currentTimeControl;

    // AI 게임 관련
    public static string aiDifficulty; // "easy", "medium", "hard"
    public static bool isAIGame = false;

    // ComputerButton 클릭 카운트 (앱 실행 후 3번 클릭 시 전면 광고)
    private static int computerButtonClickCount = 0;
    private const int CLICKS_FOR_AD = 3;
    
    void Start()
    {
        // 이미 존재하는 TimeControlManager가 있는지 확인
        TimeControlManager[] existing = FindObjectsOfType<TimeControlManager>();
        if (existing.Length > 1)
        {
            // 중복이면 가장 최신 것 제외하고 파괴
            for (int i = 0; i < existing.Length - 1; i++)
            {
                Destroy(existing[i].gameObject);
            }
        }

        // 씬 전환 시 오브젝트가 파괴되지 않도록 설정
        DontDestroyOnLoad(gameObject);
        InitializeState();

        // 게임 횟수 이벤트 구독
        if (GameCreditManager.Instance != null)
        {
            GameCreditManager.Instance.OnGamesChanged += UpdateAvailableGamesUI;
        }

        // 광고 이벤트 구독
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.OnAdWatchedSuccessfully += OnAdWatchedSuccess;
            AdsManager.Instance.OnAdFailed += OnAdWatchFailed;
        }

        // 초기 UI 업데이트 (서버에서 실제 값 확인)
        StartCoroutine(FetchAvailableGamesFromServer());
    }

    void OnEnable()
    {
        // TimeControlScene으로 돌아올 때마다 상태 초기화
        if (SceneManager.GetActiveScene().name == "TimeControlScene")
        {
            InitializeState();
            // 씬으로 돌아올 때도 서버에서 게임 횟수 확인
            StartCoroutine(FetchAvailableGamesFromServer());
        }
    }

    void InitializeState()
    {
        // 이전 연결 정리
        CleanupPreviousConnections();

        // 상태 초기화
        isConnecting = false;
        isMatching = false;

        // UI 설정
        SetupUI();
}
    
    void CleanupPreviousConnections()
    {
        try
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
            
            if (webSocket != null)
            {
                webSocket.Dispose();
                webSocket = null;
            }
            
            // 정적 변수 초기화
            gameWebSocket = null;
        }
        catch (Exception ex)
        {
        }
    }
    
    void Update()
    {
        // 메인 스레드에서 실행할 액션들 처리
        while (mainThreadActions.Count > 0)
        {
            var action = mainThreadActions.Dequeue();
            action?.Invoke();
        }
    }
    
    void SetupUI()
    {
        if (statusPanel) statusPanel.SetActive(false);
        if (cancelButton) cancelButton.SetActive(false);

        // AI 난이도 패널 초기화
        if (aiDifficultyPanel) aiDifficultyPanel.SetActive(false);

        // 백 버튼 이벤트 설정
        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClick);

        // AI 난이도 버튼 이벤트 설정
        if (easyButton != null)
            easyButton.onClick.AddListener(() => OnDifficultySelected("easy"));
        if (mediumButton != null)
            mediumButton.onClick.AddListener(() => OnDifficultySelected("medium"));
        if (difficultButton != null)
            difficultButton.onClick.AddListener(() => OnDifficultySelected("hard"));
        if (difficultyPanelCancelButton != null)
            difficultyPanelCancelButton.onClick.AddListener(OnDifficultyPanelCancel);

        // Watch Video 버튼 이벤트 설정
        if (watchVideoButton != null)
            watchVideoButton.onClick.AddListener(OnWatchVideoButtonClick);
    }
    
    public void OnRapidButtonClick()
    {
        if (!isConnecting && !isMatching)
        {
            StartCoroutine(CheckGameCreditAndStartMatchmaking("Rapid"));
        }
    }

    public void OnBlitzButtonClick()
    {
        if (!isConnecting && !isMatching)
        {
            StartCoroutine(CheckGameCreditAndStartMatchmaking("Blitz"));
        }
    }

    IEnumerator CheckGameCreditAndStartMatchmaking(string timeControl)
    {
        // 게임 횟수 확인
        if (GameCreditManager.Instance == null)
        {
            Debug.LogWarning("[TimeControlManager] GameCreditManager not found");
            // 매니저 없으면 그냥 진행
            StatusManager.SetUserInGame();
            StartCoroutine(ValidateSessionAndProceed(() => StartMatchmaking(timeControl)));
            yield break;
        }

        bool canPlay = false;
        string message = "";
        bool checkComplete = false;

        GameCreditManager.Instance.CheckCanPlayGameFromServer((result, msg) =>
        {
            canPlay = result;
            message = msg;
            checkComplete = true;
        });

        // 서버 응답 대기
        yield return new WaitUntil(() => checkComplete);

        if (!canPlay)
        {
            Debug.Log($"[TimeControlManager] Cannot start game: {message}");
            ShowStatus("No games available! Watch a video to earn more.");

            // 2초 후 상태 패널 자동으로 숨기기
            StartCoroutine(HideStatusAfterDelay(2f));
            yield break;
        }

        // 게임 시작 가능 - 매치메이킹 시작
        StatusManager.SetUserInGame();
        StartCoroutine(ValidateSessionAndProceed(() => StartMatchmaking(timeControl)));
    }
    
    public void OnCancelButtonClick()
    {
        // 매치메이킹 취소 시 온라인 상태로 복원
        StatusManager.SetUserOnline();
        CancelMatchmaking();
    }
    
    public void OnBackButtonClick()
    {
        // 매치메이킹 중이면 취소하고 뒤로가기
        if (isMatching || isConnecting)
        {
            StatusManager.SetUserOnline(); // 뒤로가기 시 온라인 상태로 복원
            CancelMatchmaking();
        }

        // StartScene으로 이동
        if (!string.IsNullOrEmpty(startSceneName))
        {
            SceneManager.LoadScene(startSceneName);
        }
    }
    
    void StartMatchmaking(string timeControl)
    {
        TimeControlManager.currentTimeControl = timeControl;
        isConnecting = true;
        
        // UI 업데이트
        ShowStatus($"Connecting to {timeControl} matchmaking...");
        SetButtonsActive(false);
        
        // JWT 토큰 가져오기 (SessionData 사용)
        if (!SessionData.IsValidSession())
        {
            ShowStatus("Authentication required. Please login first.");
            SetButtonsActive(true);
            return;
        }
        string token = SessionData.token;
        
        _ = ConnectToMatchmakingAsync(token);
    }
    
    async Task ConnectToMatchmakingAsync(string token)
    {
        try
        {
            webSocket = new ClientWebSocket();
            cancellationTokenSource = new CancellationTokenSource();
            
            string wsUrl = $"{ServerConfig.GetWebSocketUrl()}/matchmaking";
            var uri = new Uri(wsUrl);
            
            await webSocket.ConnectAsync(uri, cancellationTokenSource.Token);
            
            // 연결 성공 처리 (메인 스레드에서)
            mainThreadActions.Enqueue(() =>
            {
                isConnecting = false;
                isMatching = true;
                ShowStatus($"Searching for {TimeControlManager.currentTimeControl} match...");
                if (cancelButton) cancelButton.SetActive(true);
            });
            
            // 매치메이킹 요청 전송
            string message = $"{TimeControlManager.currentTimeControl} {token}";
            await SendMessageAsync(message);
            
            // 메시지 수신 시작
            _ = ReceiveMessagesAsync();
        }
        catch (Exception ex)
        {
            mainThreadActions.Enqueue(() =>
            {
                ShowStatus("Failed to connect. Please try again.");
                ResetUI();
            });
        }
    }
    
    async Task SendMessageAsync(string message)
    {
        try
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            var buffer = new ArraySegment<byte>(messageBytes);
            await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            mainThreadActions.Enqueue(() =>
            {
            });
        }
    }
    
    async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[1024 * 4];
        
        try
        {
            while (webSocket != null && webSocket.State == WebSocketState.Open && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    // 메인 스레드에서 처리
                    mainThreadActions.Enqueue(() =>
                    {
                        HandleMatchResult(message);
                    });
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            mainThreadActions.Enqueue(() =>
            {
                ShowStatus("Connection error. Please try again.");
                ResetUI();
            });
        }
        finally
        {
        }
    }
    
    void HandleMatchResult(string message)
    {
        // 세션 만료 메시지 처리
        if (message.Trim() == "SESSION_EXPIRED")
        {
            SessionData.ClearSession();
            StartCoroutine(ShowWarningAndQuit());
            return;
        }
        
        // 서버에서 오는 메시지 형태: "Red OpponentName GameToken OpponentELO MyCurrentELO" 
        string[] parts = message.Split(' ');
        
        if (parts.Length >= 5)
        {
            // 새로운 형식: 본인 ELO도 포함
            playerColor = parts[0];
            opponentName = parts[1];
            gameToken = parts[2];
            opponentELO = parts[3];
            myCurrentELO = parts[4];

            ShowStatus($"Match found! You are {playerColor}. Opponent: {opponentName}");

            // 매치메이킹 WebSocket 정리 (게임에서 새로운 연결 생성)
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }
            
            // 매치메이킹 WebSocket 종료
            _ = CloseWebSocketAsync();
            
            // gameWebSocket은 null로 설정 (GameManager에서 새로 연결)
            gameWebSocket = null;
            
            // 잠시 대기 후 게임 씬으로 이동
            StartCoroutine(LoadGameSceneDelayed());
        }
        else if (parts.Length >= 4)
        {
            // 이전 형식 지원 (본인 ELO 없음)
            playerColor = parts[0];
            opponentName = parts[1];
            gameToken = parts[2];
            opponentELO = parts[3];
            myCurrentELO = SessionData.elo; // 기본값으로 SessionData 사용
            
            ShowStatus($"Match found! You are {playerColor}. Opponent: {opponentName}");
            
            // 매치메이킹 WebSocket 정리 (게임에서 새로운 연결 생성)
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }
            
            // 매치메이킹 WebSocket 종료
            _ = CloseWebSocketAsync();
            
            // gameWebSocket은 null로 설정 (GameManager에서 새로 연결)
            gameWebSocket = null;
            
            // 잠시 대기 후 게임 씬으로 이동
            StartCoroutine(LoadGameSceneDelayed());
        }
        else if (parts.Length >= 3)
        {
            // 이전 형식 지원 (ELO 없음)
            playerColor = parts[0];
            opponentName = parts[1];
            gameToken = parts[2];
            opponentELO = "Unknown";
            
            ShowStatus($"Match found! You are {playerColor}. Opponent: {opponentName}");
            
            
            // 매치메이킹 WebSocket 정리 (게임에서 새로운 연결 생성)
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }
            
            // 매치메이킹 WebSocket 종료
            _ = CloseWebSocketAsync();
            
            // gameWebSocket은 null로 설정 (GameManager에서 새로 연결)
            gameWebSocket = null;
            
            // 잠시 대기 후 게임 씬으로 이동
            StartCoroutine(LoadGameSceneDelayed());
        }
        else
        {
            ShowStatus("Invalid match result. Please try again.");
            ResetUI();
        }
    }
    
    IEnumerator LoadGameSceneDelayed()
    {
        yield return new WaitForSeconds(2f);
        LoadGameScene();
    }
    
    void LoadGameScene()
    {
        // 매치메이킹 WebSocket은 닫지 않고 게임용으로 유지
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }
    
    void CancelMatchmaking()
    {
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
        }
        
        if (webSocket != null)
        {
            _ = CloseWebSocketAsync();
        }
        
        isConnecting = false;
        isMatching = false;
        
        ShowStatus("Matchmaking cancelled.");
        ResetUI();
    }
    
    async Task CloseWebSocketAsync()
    {
        try
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User cancelled", CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
        }
        finally
        {
            webSocket?.Dispose();
            webSocket = null;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
    }
    
    void ShowStatus(string message)
    {
        if (statusPanel) statusPanel.SetActive(true);
        if (statusText) statusText.text = message;
    }
    
    void SetButtonsActive(bool active)
    {
        if (rapidButton) rapidButton.SetActive(active);
        if (blitzButton) blitzButton.SetActive(active);
    }
    
    void ResetUI()
    {
        SetButtonsActive(true);
        if (cancelButton) cancelButton.SetActive(false);
        if (statusPanel) statusPanel.SetActive(false);
        isConnecting = false;
        isMatching = false;
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

                    // 경고 패널 표시 후 3초 후 종료
                    StartCoroutine(ShowWarningAndQuit());
                }
                else
                {
                    // 네트워크 에러 등의 경우 그냥 진행 (선택사항)
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
        // 경고 패널 활성화
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
            // 패널이 없으면 3초 대기
            yield return new WaitForSeconds(3f);
        }
        
        // 프로그램 강제 종료
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    // ============ AI 게임 관련 함수 ============

    public void OnComputerButtonClick()
    {
        // 클릭 카운트 증가
        computerButtonClickCount++;
        Debug.Log($"[TimeControlManager] Computer button clicked {computerButtonClickCount} times");

        // 3번 클릭 시 전면 광고 표시
        if (computerButtonClickCount >= CLICKS_FOR_AD)
        {
            Debug.Log("[TimeControlManager] Showing interstitial ad after 3 clicks");

            // 카운트 리셋
            computerButtonClickCount = 0;

            // 전면 광고 표시
            if (InterstitialAdManager.Instance != null)
            {
                if (InterstitialAdManager.Instance.IsAdReady())
                {
                    InterstitialAdManager.Instance.ShowInterstitialAd();
                }
                else
                {
                    Debug.LogWarning("[TimeControlManager] Interstitial ad not ready");
                }
            }
            else
            {
                Debug.LogWarning("[TimeControlManager] InterstitialAdManager not found");
            }
        }

        // 세션 체크는 필요 없음 (AI 게임은 로그인 없이도 가능)
        // 난이도 선택 패널 표시
        if (aiDifficultyPanel != null)
        {
            aiDifficultyPanel.SetActive(true);
        }
    }

    void OnDifficultySelected(string difficulty)
    {
        // 난이도 및 AI 게임 플래그 저장
        aiDifficulty = difficulty;
        isAIGame = true;

        // 플레이어 색상을 랜덤으로 결정 (50% Red, 50% Blue)
        playerColor = UnityEngine.Random.value > 0.5f ? "Red" : "Blue";
        Debug.Log($"[TIME_CONTROL] AI Difficulty: {difficulty}, Player Color: {playerColor}");

        // 패널 닫기
        if (aiDifficultyPanel != null)
        {
            aiDifficultyPanel.SetActive(false);
        }

        // AI Scene으로 이동
        if (!string.IsNullOrEmpty(aiGameSceneName))
        {
            SceneManager.LoadScene(aiGameSceneName);
        }
    }

    void OnDifficultyPanelCancel()
    {
        // 패널 닫기
        if (aiDifficultyPanel != null)
        {
            aiDifficultyPanel.SetActive(false);
        }
    }

    // ============ 게임 크레딧 관련 함수 ============

    /// <summary>
    /// 씬 진입 시 서버에서 게임 횟수 확인
    /// </summary>
    IEnumerator FetchAvailableGamesFromServer()
    {
        // 초기 로딩 표시
        if (availableGamesText != null)
        {
            availableGamesText.text = "Available Games: ...";
        }

        // 게임 횟수 확인 (GameCreditManager)
        if (GameCreditManager.Instance == null)
        {
            Debug.LogWarning("[TimeControlManager] GameCreditManager not found");
            if (availableGamesText != null)
            {
                availableGamesText.text = "Available Games: N/A";
            }
            yield break;
        }

        bool checkComplete = false;

        GameCreditManager.Instance.CheckCanPlayGameFromServer((result, msg) =>
        {
            checkComplete = true;
            UpdateAvailableGamesUI(0);
            Debug.Log($"[TimeControlManager] Server game credit check complete: {msg}");
        });

        // 서버 응답 대기 (최대 5초)
        float timeout = 5f;
        float elapsed = 0f;
        while (!checkComplete && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!checkComplete)
        {
            Debug.LogWarning("[TimeControlManager] Server check timeout");
            if (availableGamesText != null)
            {
                availableGamesText.text = "Available Games: Error";
            }
        }
    }

    /// <summary>
    /// 게임 횟수 체크 (소비는 게임 종료 시)
    /// </summary>
    bool CheckGameCredit()
    {
        if (GameCreditManager.Instance == null)
        {
            Debug.LogWarning("[TimeControlManager] GameCreditManager not found");
            return true; // 매니저 없으면 그냥 진행
        }

        if (!GameCreditManager.Instance.CanPlayGame())
        {
            Debug.Log("[TimeControlManager] No games available");
            ShowStatus("No games available! Watch a video to earn more.");
            return false;
        }

        // 게임 횟수는 체크만 하고, 실제 소비는 게임 종료 시에 수행
        return true;
    }

    /// <summary>
    /// 게임 횟수 UI 업데이트
    /// </summary>
    void UpdateAvailableGamesUI(int games)
    {
        if (availableGamesText == null)
            return;

        if (GameCreditManager.Instance == null)
        {
            availableGamesText.text = "Available Games: 3";
            return;
        }

        int availableGames = GameCreditManager.Instance.GetAvailableGames();

        availableGamesText.text = $"Available Games: {availableGames}";

        Debug.Log($"[TimeControlManager] ✅ UI updated - Available Games: {availableGames}");
    }

    /// <summary>
    /// Watch Video 버튼 클릭
    /// </summary>
    public void OnWatchVideoButtonClick()
    {
        Debug.Log("[TimeControlManager] Watch Video button clicked");

        if (AdsManager.Instance == null)
        {
            Debug.LogError("[TimeControlManager] AdsManager not found!");
            ShowStatus("Ads not available");
            StartCoroutine(HideStatusAfterDelay(2f));
            return;
        }

        // Unity Ads가 자체 로딩 메시지를 표시하므로 우리 메시지는 표시하지 않음
        // 광고 준비 여부와 관계없이 시도 (AdsManager에서 자동 재시도)
        AdsManager.Instance.ShowRewardedAd();
    }

    /// <summary>
    /// 광고 시청 성공 콜백
    /// </summary>
    void OnAdWatchedSuccess()
    {
        Debug.Log("[TimeControlManager] Ad watched successfully!");
        ShowStatus("Reward granted! +1 game added.");
        UpdateAvailableGamesUI(0);

        // 2초 후 상태 패널 닫기
        StartCoroutine(HideStatusAfterDelay(2f));
    }

    /// <summary>
    /// 광고 시청 실패 콜백
    /// </summary>
    void OnAdWatchFailed()
    {
        Debug.Log("[TimeControlManager] Ad watch failed");
        ShowStatus("Ad not available. Please try again later.");

        // 2초 후 상태 패널 닫기
        StartCoroutine(HideStatusAfterDelay(2f));
    }

    /// <summary>
    /// 일정 시간 후 상태 패널 숨기기
    /// </summary>
    IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (statusPanel != null && !isMatching && !isConnecting)
        {
            statusPanel.SetActive(false);
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (GameCreditManager.Instance != null)
        {
            GameCreditManager.Instance.OnGamesChanged -= UpdateAvailableGamesUI;
        }

        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.OnAdWatchedSuccessfully -= OnAdWatchedSuccess;
            AdsManager.Instance.OnAdFailed -= OnAdWatchFailed;
        }

        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }

        if (webSocket != null)
        {
            webSocket.Dispose();
            webSocket = null;
        }
    }
}