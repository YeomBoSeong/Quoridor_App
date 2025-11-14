using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System;
using UnityEngine.SceneManagement;

public partial class GameManager : MonoBehaviour
{
    [Header("Player Objects")]
    [SerializeField] private GameObject playerRedObject;
    [SerializeField] private GameObject playerBlueObject;

    [Header("Player Positions")]
    [SerializeField] private Vector3 bottomPosition = new Vector3(0, -396.8182f, 0);
    [SerializeField] private Vector3 topPosition = new Vector3(0, 396.8182f, 0);

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI myNameText;
    [SerializeField] private TextMeshProUGUI opponentNameText;

    [Header("Profile Image UI Elements")]
    [SerializeField] private UnityEngine.UI.Image myProfileImage;
    [SerializeField] private UnityEngine.UI.Image opponentProfileImage;

    [Header("ELO UI Elements")]
    [SerializeField] private TextMeshProUGUI myELOText;
    [SerializeField] private TextMeshProUGUI opponentELOText;

    [Header("Timer UI Elements")]
    [SerializeField] private TextMeshProUGUI myTimeText;
    [SerializeField] private TextMeshProUGUI opponentTimeText;
    
    [Header("Wall Count UI Elements")]
    [SerializeField] private TextMeshProUGUI myWallCountText;
    [SerializeField] private TextMeshProUGUI opponentWallCountText;

    [Header("Additional UI (Optional)")]
    [SerializeField] private TextMeshProUGUI playerColorText;
    [SerializeField] private TextMeshProUGUI gameTokenText;

    [Header("UI Buttons")]
    [SerializeField] private Button moveButton;
    [SerializeField] private Button moveCancelButton;
    [SerializeField] private Button wallButton;
    [SerializeField] private Button wallCancelButton;
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button forfeitButton;
    [SerializeField] private Button placeButton;
    
    [Header("UI Containers")]
    [SerializeField] private GameObject wallContainer;
    
    [Header("Game Result UI")]
    [SerializeField] private GameObject gameResultPanel;
    [SerializeField] private TextMeshProUGUI gameResultText;
    [SerializeField] private Button backToMenuButton;
    
    [Header("Session Warning UI")]
    [SerializeField] private GameObject sessionWarningPanel;
    [SerializeField] private TextMeshProUGUI sessionWarningText;

    [Header("Debug Info")]
    [SerializeField] private string myPlayerColor;
    [SerializeField] private string myUsername;
    [SerializeField] private string myELO;
    [SerializeField] private string opponentName;
    [SerializeField] private string opponentELO;
    [SerializeField] private string gameToken;

    [Header("Game Timer Settings")]
    [SerializeField] private float rapidTimeInSeconds = 600f; // 10분 (Rapid)
    [SerializeField] private float blitzTimeInSeconds = 180f; // 3분 (Blitz)
    [SerializeField] private float blitzIncrementSeconds = 2f; // Blitz 증가 시간
    
    [Header("Timer Debug Info")]
    [SerializeField] private float myTimeRemaining;
    [SerializeField] private float opponentTimeRemaining;
    [SerializeField] private string currentTurn = "Red"; // Red 또는 Blue
    [SerializeField] private bool isMyTurn;
    
    [Header("Wall Count Debug Info")]
    [SerializeField] private int myWallCount = 10;
    [SerializeField] private int opponentWallCount = 10;
    
    [Header("Move Highlighting")]
    [SerializeField] private GameObject moveButtonPrefab; // 이동 가능한 칸에 표시할 버튼 프리팹
    private List<GameObject> possibleMoveButtons = new List<GameObject>();

    [Header("Sound Effects")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip moveSound; // 수를 둘 때 재생할 소리

    [Header("Wall System")]
    [SerializeField] private GameObject wallPrefab; // 벽 프리팹
    private GameObject currentWallPreview; // 현재 벽 미리보기 오브젝트
    private bool isWallMode = false; // 벽 모드 활성화 여부
    private bool isWallHorizontal = true; // 벽 방향 (true: 가로, false: 세로)
    private Vector2 currentWallPosition; // 현재 벽 위치
    private bool isWallPositionValid = false; // 현재 벽 위치 유효성
    private Vector2Int currentBoardCoordinates; // 현재 보드 좌표
    
    
    [Header("WebSocket Connection")]
    private ClientWebSocket gameWebSocket;
    private CancellationTokenSource cancellationTokenSource;
    private bool isReceivingMessages = false;
    private System.Collections.Generic.Queue<System.Action> mainThreadActions = new System.Collections.Generic.Queue<System.Action>();
    private bool isGameEnded = false; // 게임 종료 상태 추적
    private bool hasConsumedGameCredit = false; // 게임 횟수 소비 여부 추적
    
    [Header("Board Grid Settings")]
    [SerializeField] private float cellSize = 99.20454f; // 한 칸 크기 (adjusted for screen ratio)
    [SerializeField] private Vector3 boardCenter = Vector3.zero; // 보드 중앙
    [SerializeField] private Canvas gameCanvas; // UI Canvas 참조
    
    private Board gameBoard;

    void Start()
    {
        InitializeGame();
        InitializeButtons();
    }

    void InitializeGame()
    {
        // TimeControlManager에서 매치 정보 가져오기
        myPlayerColor = TimeControlManager.playerColor;
        opponentName = TimeControlManager.opponentName;
        gameToken = TimeControlManager.gameToken;
        opponentELO = TimeControlManager.opponentELO;

        // SessionData에서 내 유저네임 가져오기
        if (SessionData.IsValidSession())
        {
            myUsername = SessionData.username;
            // 현재 게임 모드에 맞는 최신 ELO 사용 (TimeControlManager에서 매치메이킹 시 받아온 값)
            myELO = !string.IsNullOrEmpty(TimeControlManager.myCurrentELO) ? 
                    TimeControlManager.myCurrentELO : SessionData.elo;
        }
        else
        {
            // 로그인하지 않은 상태 (에디터에서 직접 실행한 경우)
            myUsername = "Test User";
            myELO = "1500";
        }


        // 플레이어 위치 설정
        SetupPlayerPositions();

        // UI 업데이트
        UpdateUI();

        // 프로필 이미지 로드
        LoadProfileImages();

        // 게임 타이머 초기화
        InitializeGameTimer();
        
        // 게임 보드 초기화
        InitializeGameBoard();
        
        // Canvas 자동 찾기
        if (gameCanvas == null)
        {
            gameCanvas = FindObjectOfType<Canvas>();
            if (gameCanvas == null)
            {
                }
        }
        
        // Wall Prefab 체크

        
        // WebSocket 연결 설정
        SetupGameWebSocket();
    }

    void SetupPlayerPositions()
    {
        if (playerRedObject == null || playerBlueObject == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(myPlayerColor))
        {
            return;
        }

        // RectTransform 확인 (Canvas 자식인 경우)
        RectTransform redRect = playerRedObject.GetComponent<RectTransform>();
        RectTransform blueRect = playerBlueObject.GetComponent<RectTransform>();

        if (myPlayerColor.Equals("Red", System.StringComparison.OrdinalIgnoreCase))
        {
            // 내가 Red 플레이어인 경우
            // Red는 아래쪽 (0, -396.8182, 0)
            // Blue는 위쪽 (0, 396.8182, 0)

            if (redRect != null)
                redRect.localPosition = bottomPosition;
            else
                playerRedObject.transform.localPosition = bottomPosition;

            if (blueRect != null)
                blueRect.localPosition = topPosition;
            else
                playerBlueObject.transform.localPosition = topPosition;

        }
        else if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            // 내가 Blue 플레이어인 경우
            // Blue는 아래쪽 (0, -396.8182, 0)
            // Red는 위쪽 (0, 396.8182, 0)

            if (blueRect != null)
                blueRect.localPosition = bottomPosition;
            else
                playerBlueObject.transform.localPosition = bottomPosition;

            if (redRect != null)
                redRect.localPosition = topPosition;
            else
                playerRedObject.transform.localPosition = topPosition;

        }
        else
        {
        }
    }

    void UpdateUI()
    {
        // 주요 UI 업데이트
        if (myNameText != null)
            myNameText.text = myUsername;

        if (opponentNameText != null)
            opponentNameText.text = opponentName;

        // ELO UI 업데이트
        if (myELOText != null)
            myELOText.text = myELO;

        if (opponentELOText != null)
            opponentELOText.text = opponentELO;

        // 추가 UI 업데이트 (옵션)
        if (playerColorText != null)
            playerColorText.text = $"You are: {myPlayerColor}";

        if (gameTokenText != null)
            gameTokenText.text = $"Game: {gameToken}";

        
        // 타이머 UI 업데이트
        UpdateTimerUI();
        
        // 벽 개수 UI 업데이트
        UpdateWallCountUI();
    }

    // 게임 중에 플레이어 색상 확인
    public bool IsMyTurn(string currentPlayer)
    {
        return myPlayerColor.Equals(currentPlayer, System.StringComparison.OrdinalIgnoreCase);
    }

    // 게임 정보 접근자들
    public string GetMyPlayerColor() => myPlayerColor;
    public string GetMyUsername() => myUsername;
    public string GetMyELO() => myELO;
    public string GetOpponentName() => opponentName;
    public string GetOpponentELO() => opponentELO;
    public string GetGameToken() => gameToken;

    // GetMyELOFromServer 함수 제거 - SessionData에서 직접 가져옴
    
    void InitializeGameTimer()
    {
        // 게임 모드에 따른 초기 시간 설정
        float initialTime = GetInitialTimeForMode();
        
        // 양쪽 플레이어 모두 동일한 시간으로 시작
        myTimeRemaining = initialTime;
        opponentTimeRemaining = initialTime;
        
        // Red 플레이어가 항상 먼저 시작
        currentTurn = "Red";
        isMyTurn = myPlayerColor.Equals("Red", System.StringComparison.OrdinalIgnoreCase);
        
        UpdateTimerUI();
        
    }
    
    void Update()
    {
        // 메인 스레드에서 실행할 액션들 처리
        while (mainThreadActions.Count > 0)
        {
            var action = mainThreadActions.Dequeue();
            action?.Invoke();
        }
        
        // 현재 턴의 플레이어 시간을 감소시킴
        if (isMyTurn)
        {
            myTimeRemaining -= Time.deltaTime;
            if (myTimeRemaining <= 0)
            {
                myTimeRemaining = 0;
                OnTimeUp();
            }
        }
        else
        {
            opponentTimeRemaining -= Time.deltaTime;
            if (opponentTimeRemaining <= 0)
            {
                opponentTimeRemaining = 0;
                OnTimeUp();
            }
        }
        
        // UI 업데이트 (매 프레임마다는 비효율적이지만 타이머는 실시간 업데이트가 필요)
        UpdateTimerUI();
        
        // 버튼 활성화 상태 업데이트
        UpdateButtonStates();
        
        // 벽 모드 입력 처리
        if (isWallMode)
        {
            HandleWallModeInput();
        }
        
    }
    
    void UpdateTimerUI()
    {
        if (myTimeText != null)
        {
            myTimeText.text = FormatTime(myTimeRemaining);
            // 글자색을 검은색으로 설정
            myTimeText.color = Color.black;
        }
        
        if (opponentTimeText != null)
        {
            opponentTimeText.text = FormatTime(opponentTimeRemaining);
            // 글자색을 검은색으로 설정
            opponentTimeText.color = Color.black;
        }
    }
    
    string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        return $"{minutes:00}:{seconds:00}";
    }
    
    void UpdateWallCountUI()
    {
        if (myWallCountText != null)
        {
            myWallCountText.text = $": {myWallCount}";
            myWallCountText.color = Color.white;
        }
        
        if (opponentWallCountText != null)
        {
            opponentWallCountText.text = $": {opponentWallCount}";
            opponentWallCountText.color = Color.white;
        }
    }
    
    float GetInitialTimeForMode()
    {
        string timeControl = TimeControlManager.currentTimeControl;
        
        if (timeControl == "Blitz")
        {
            return blitzTimeInSeconds;
        }
        else // "Rapid" 또는 기타
        {
            return rapidTimeInSeconds;
        }
    }
    
    void AddIncrementTime()
    {
        string timeControl = TimeControlManager.currentTimeControl;
        
        // Blitz 모드에서만 시간 증가
        if (timeControl == "Blitz")
        {
            myTimeRemaining += blitzIncrementSeconds;
            
            // UI 즉시 업데이트
            UpdateTimerUI();
            
        }
    }
    
    public void SwitchTurn()
    {
        string previousTurn = currentTurn;
        
        // 턴 전환
        if (currentTurn.Equals("Red"))
        {
            currentTurn = "Blue";
        }
        else
        {
            currentTurn = "Red";
        }
        
        isMyTurn = myPlayerColor.Equals(currentTurn, System.StringComparison.OrdinalIgnoreCase);
        
        
        // 버튼 상태 즉시 업데이트
        UpdateButtonStates();
    }
    
    void OnTimeUp()
    {
        if (isMyTurn)
        {
            // 내 시간이 다 떨어졌으므로 패배 메시지를 서버로 전송
            SendTimeUpMessage();

            // 게임 종료 플래그 설정 및 자신의 화면에 패배 결과 표시
            isGameEnded = true;
            ShowGameResult("Lost");
        }
        else
        {
            // 상대방 시간 초과로 승리 (서버에서 메시지가 올 것임)
        }
    }
    
    void SendTimeUpMessage()
    {
        string timeControl = TimeControlManager.currentTimeControl;
        string pos = "0,0"; // 시간 초과이므로 위치는 의미없음
        string remainTime = "0.0";
        string gameProgress = "Lost"; // 시간 초과로 패배
        
        string message = $"{timeControl} {gameToken} {myUsername} TimeUp {pos} {remainTime} {gameProgress}";
        
        _ = SendMessageAsync(message);
    }
    
    public void SetCurrentTurn(string turnColor)
    {
        currentTurn = turnColor;
        isMyTurn = myPlayerColor.Equals(currentTurn, System.StringComparison.OrdinalIgnoreCase);
        
        // 버튼 상태 즉시 업데이트
        UpdateButtonStates();
    }
    
    void UpdateButtonStates()
    {
        // 내 턴일 때만 Move, Wall 버튼 활성화
        bool buttonsEnabled = isMyTurn;
        
        if (moveButton != null && moveButton.gameObject.activeInHierarchy)
        {
            moveButton.interactable = buttonsEnabled;
        }
        
        if (wallButton != null && wallButton.gameObject.activeInHierarchy)
        {
            // Wall 버튼은 내 턴이고 벽이 남아있을 때만 활성화
            wallButton.interactable = buttonsEnabled && myWallCount > 0;
        }
        
        // Cancel 버튼은 항상 활성화 (자신의 행동을 취소하는 것이므로)
        if (moveCancelButton != null && moveCancelButton.gameObject.activeInHierarchy)
        {
            moveCancelButton.interactable = true;
        }
        
        if (wallCancelButton != null && wallCancelButton.gameObject.activeInHierarchy)
        {
            wallCancelButton.interactable = true;
        }
        
        // Wall 모드일 때 Rotate, Place 버튼 상태 업데이트
        if (rotateButton != null && rotateButton.gameObject.activeInHierarchy)
        {
            rotateButton.interactable = isWallMode && buttonsEnabled;
        }
        
        if (placeButton != null && placeButton.gameObject.activeInHierarchy)
        {
            placeButton.interactable = isWallMode && buttonsEnabled && isWallPositionValid;
        }
        
    }
    
    void InitializeGameBoard()
    {
        // Board 클래스 초기화 (1=Red, 2=Blue)
        int myColorCode = myPlayerColor.Equals("Red", System.StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        gameBoard = new Board(myColorCode);
        
        
        // 초기 보드 상태 디버깅
        
        // 좌표 변환 테스트
        Vector3 myWorldPos = BoardToWorldPosition(gameBoard.my_pos[0], gameBoard.my_pos[1]);
        Vector3 opponentWorldPos = BoardToWorldPosition(gameBoard.opponent_pos[0], gameBoard.opponent_pos[1]);
        
        // Unity에서의 플레이어 실제 위치와 비교
        if (playerRedObject != null && playerBlueObject != null)
        {
        }
    }
    
    public void ShowPossibleMoves()
    {
        if (gameBoard == null)
        {
            return;
        }
        
        // 기존 이동 버튼들 제거
        ClearPossibleMoveButtons();
        
        // 현재 턴인 플레이어 색깔 결정
        int currentPlayerColor = currentTurn.Equals("Red") ? 1 : 2;
        
        // Board 정보 로그 출력 (자기 턴일 때만)
        if (isMyTurn)
        {
        }
        
        // 이동 가능한 칸 계산
        gameBoard.calculate_possible_moves(currentPlayerColor);
        
        
        // 각 이동 가능한 칸에 버튼 생성
        foreach (var move in gameBoard.possible_moves)
        {
            int boardY = move[0];
            int boardX = move[1];
            
            Vector3 worldPos = BoardToWorldPosition(boardY, boardX);
            GameObject moveBtn = CreateMoveButton(worldPos, boardY, boardX);
            possibleMoveButtons.Add(moveBtn);
            
        }
    }
    
    public void ClearPossibleMoveButtons()
    {
        foreach (GameObject btn in possibleMoveButtons)
        {
            if (btn != null)
                Destroy(btn);
        }
        possibleMoveButtons.Clear();
        
    }
    
    Vector3 BoardToWorldPosition(int boardY, int boardX)
    {
        // Red 관점 좌표계(boardY, boardX)를 각 플레이어의 Unity 월드 좌표로 변환
        // 짝수 좌표 (플레이어 위치) 및 홀수 좌표 (벽 위치) 모두 지원
        
        // 0~8 그리드 시스템으로 변환 (홀수 좌표도 지원)
        float gridY = boardY / 2.0f;  // 0->0, 1->0.5, 2->1, 3->1.5, ..., 16->8
        float gridX = boardX / 2.0f;  // 0->0, 1->0.5, 2->1, 3->1.5, ..., 16->8
        
        float worldX, worldY;
        
        if (myPlayerColor.Equals("Red", System.StringComparison.OrdinalIgnoreCase))
        {
            // Red 플레이어 관점: Red 관점 좌표 그대로 사용
            worldX = (gridX - 4) * cellSize; // gridX 4가 중앙
            worldY = (4 - gridY) * cellSize; // Red가 아래쪽(gridY 8), Blue가 위쪽(gridY 0)
        }
        else
        {
            // Blue 플레이어 관점: 보드를 180도 회전한 것처럼 변환
            worldX = (4 - gridX) * cellSize; // X축 뒤집기
            worldY = (gridY - 4) * cellSize; // Y축 뒤집기 (Blue가 아래쪽으로)
        }
        
        Vector3 worldPos = boardCenter + new Vector3(worldX, worldY, 0);
        return worldPos;
    }
    
    Vector3 ServerCoordToWorldPosition(int boardY, int boardX)
    {
        // 서버에서 받은 좌표(Red 관점)를 Unity 월드 좌표로 변환
        // 모든 플레이어가 동일한 방식으로 변환 (플레이어 관점 변환 없음)
        
        // 0~8 그리드 시스템으로 변환 (홀수 좌표도 지원)
        float gridY = boardY / 2.0f;  
        float gridX = boardX / 2.0f;  
        
        // Red 관점 기준으로 통일된 변환
        float worldX = (gridX - 4) * cellSize; 
        float worldY = (4 - gridY) * cellSize; 
        
        Vector3 worldPos = boardCenter + new Vector3(worldX, worldY, 0);
        return worldPos;
    }
    
    // Blue 플레이어 관점에서 좌표 변환 (서버 -> 클라이언트)
    Vector2Int ConvertOpponentCoordinates(int boardY, int boardX)
    {
        if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            // Blue 플레이어 관점에서는 Y축을 뒤집어야 함
            // boardY: 1,3,5,...,15 -> 15,13,11,...,1
            int convertedY = 16 - boardY;
            // X축은 동일
            int convertedX = boardX;
            
            return new Vector2Int(convertedY, convertedX);
        }
        else
        {
            // Red 플레이어는 변환 없이 그대로 사용
            return new Vector2Int(boardY, boardX);
        }
    }
    
    // Blue 플레이어 관점에서 서버로 보낼 좌표 변환 (클라이언트 -> 서버)
    Vector2Int ConvertMyCoordinatesForServer(int boardY, int boardX)
    {
        if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            // Blue 플레이어가 서버로 보낼 때는 역변환
            // 내 관점의 좌표를 서버 기준(Red 기준) 좌표로 변환
            int serverY = 16 - boardY;
            int serverX = boardX;
            
            return new Vector2Int(serverY, serverX);
        }
        else
        {
            // Red 플레이어는 변환 없이 그대로 사용
            return new Vector2Int(boardY, boardX);
        }
    }
    
    GameObject CreateMoveButton(Vector3 position, int boardY, int boardX)
    {
        GameObject button;

        if (moveButtonPrefab != null)
        {
            // Canvas를 부모로 설정
            Transform parent = gameCanvas != null ? gameCanvas.transform : transform;
            button = Instantiate(moveButtonPrefab, parent);
            
        }
        else
        {
            // 프리팹이 없는 경우 기본 버튼 생성
            button = new GameObject($"MoveButton_{boardY}_{boardX}");

            // Canvas를 부모로 설정
            Transform parent = gameCanvas != null ? gameCanvas.transform : transform;
            button.transform.SetParent(parent, false);

            // UI Button 컴포넌트 추가
            var btnComponent = button.AddComponent<Button>();
            var image = button.AddComponent<UnityEngine.UI.Image>();

            // 버튼 스타일 설정
            image.color = new Color(0f, 1f, 0f, 0.7f); // 반투명 초록색

            // RectTransform 설정
            var rectTransform = button.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0.6f, 3f); // 프리팹 크기와 동일하게 설정

        }
        
        // 위치 설정 (Canvas 기준으로)
        var rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = new Vector2(position.x, position.y);
            rect.localScale = Vector3.one; // 스케일 확실히 설정
        }
        else
        {
            button.transform.localPosition = position;
        }
        
        // 버튼 클릭 이벤트 추가
        var buttonComp = button.GetComponent<Button>();
        if (buttonComp != null)
        {
            int capturedY = boardY;
            int capturedX = boardX;
            buttonComp.onClick.AddListener(() => OnMoveButtonClicked(capturedY, capturedX));
        }
        
        
        return button;
    }
    
    void HandleWallModeInput()
    {
        // 마우스 클릭 감지
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePosition = Input.mousePosition;
                HandleBoardTouch(mousePosition);
        }
    }
    
    void HandleBoardTouch(Vector2 screenPosition)
    {
        if (gameCanvas == null) return;
        
        // Canvas의 카메라 설정 확인
        Camera cam = null;
        if (gameCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            cam = null; // Overlay 모드에서는 카메라 불필요
        }
        else
        {
            cam = gameCanvas.worldCamera ?? Camera.main;
        }
        
        // 화면 좌표를 Canvas 로컬 좌표로 변환
        RectTransform canvasRect = gameCanvas.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cam, out localPoint))
            {
                // 보드 범위 체크 (-396.8182 ~ 396.8182)
                if (localPoint.x < -396.8182f || localPoint.x > 396.8182f || localPoint.y < -396.8182f || localPoint.y > 396.8182f)
                {
                    return;
                }
                
                // 보드 그리드에 맞춰 벽 위치 계산
                Vector2 wallPosition = CalculateWallPosition(localPoint);
                
                
                // 벽 미리보기 생성/업데이트
                ShowWallPreview(wallPosition);
            }
        }
    }
    
    Vector2 CalculateWallPosition(Vector2 localPoint)
    {
        // RectTransform 좌표 기준으로 그리드 계산
        float gridSize = cellSize; // 화면 비율에 따라 조정됨

        // Y축: 396.8182(위) ~ -396.8182(아래)를 0~16 그리드로 변환 (Board 배열 인덱스와 일치)
        float gridY = (396.8182f - localPoint.y) / (gridSize / 2.0f);
        // X축: -396.8182(왼쪽) ~ 396.8182(오른쪽)를 0~16 그리드로 변환 (Board 배열 인덱스와 일치)
        float gridX = (localPoint.x + 396.8182f) / (gridSize / 2.0f);

        if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            gridY = 16 - gridY;
            gridX = 16 - gridX;
        }
        
        
        // 가장 가까운 유효한 벽 위치 찾기
        Vector2 result = FindNearestValidWallPosition(gridX, gridY);
        return result;
    }
    
    Vector2 FindNearestValidWallPosition(float touchGridX, float touchGridY)
    {
        Vector2 bestPosition = Vector2.zero;
        float bestDistance = float.MaxValue;
        bool foundValidWall = false;
        
        // 보드 범위 내에서 벽을 놓을 수 있는 모든 위치 검사 (0~16 좌표계 사용)
        for (int boardY = 1; boardY <= 15; boardY += 2) // 홀수 Y (가로벽용)
        {
            for (int boardX = 0; boardX <= 14; boardX += 2) // 짝수 X (가로벽용)
            {
                if (isWallHorizontal && boardX <= 14)
                {
                    Vector2 wallPos = CheckHorizontalWallDirect(boardY, boardX);
                    if (wallPos != Vector2.zero)
                    {
                        float distance = Vector2.Distance(new Vector2(touchGridX, touchGridY), new Vector2(boardX + 1, boardY));
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestPosition = wallPos;
                            foundValidWall = true;
                            currentBoardCoordinates = new Vector2Int(boardY, boardX);
                        }
                    }
                }
            }
        }
        
        for (int boardY = 0; boardY <= 14; boardY += 2) // 짝수 Y (세로벽용)
        {
            for (int boardX = 1; boardX <= 15; boardX += 2) // 홀수 X (세로벽용)
            {
                if (!isWallHorizontal && boardY <= 14)
                {
                    Vector2 wallPos = CheckVerticalWallDirect(boardY, boardX);
                    if (wallPos != Vector2.zero)
                    {
                        float distance = Vector2.Distance(new Vector2(touchGridX, touchGridY), new Vector2(boardX, boardY + 1));
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestPosition = wallPos;
                            foundValidWall = true;
                            currentBoardCoordinates = new Vector2Int(boardY, boardX);
                        }
                    }
                }
            }
        }

        if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            bestPosition.x = -bestPosition.x;
            bestPosition.y = -bestPosition.y;
        }

        // FindNearestValidWallPosition 결과 로그
        if (foundValidWall)
        {
            Debug.Log($"[FIND_WALL] Found valid wall - BoardCoordinates: ({currentBoardCoordinates.x},{currentBoardCoordinates.y}), UIPosition: {bestPosition}, IsHorizontal: {isWallHorizontal}");
        }
        else
        {
            Debug.Log($"[FIND_WALL] No valid wall position found");
        }

        return foundValidWall ? bestPosition : Vector2.zero;
    }

    Vector2 RecalculateWallCoordinatesFromUIPosition(Vector2 uiPosition)
    {
        // Blue 플레이어의 경우 UI 좌표가 이미 반전되어 있으므로, 원래 좌표로 되돌려야 함
        Vector2 localPosition = uiPosition;
        if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            localPosition.x = -localPosition.x;
            localPosition.y = -localPosition.y;
        }

        // 로컬 좌표를 그리드 좌표로 변환 (CalculateWallPosition과 동일한 로직)
        float gridSize = cellSize; // 화면 비율에 따라 조정됨

        // Y축: 396.8182(위) ~ -396.8182(아래)를 0~16 그리드로 변환
        float gridY = (396.8182f - localPosition.y) / (gridSize / 2.0f);
        // X축: -396.8182(왼쪽) ~ 396.8182(오른쪽)를 0~16 그리드로 변환
        float gridX = (localPosition.x + 396.8182f) / (gridSize / 2.0f);

        // Blue 플레이어는 그리드 좌표 반전을 하지 않음 (이미 UI에서 반전된 상태)
        // Red 플레이어만 정상적인 그리드 변환 사용

        // 새로운 벽 방향에 맞는 유효한 위치 재계산
        Vector2 newPosition = FindNearestValidWallPosition(gridX, gridY);

        Debug.Log($"[ROTATE] Recalculated from UI position {uiPosition} -> LocalPos: {localPosition} -> Grid ({gridX},{gridY}) -> New BoardCoordinates: ({currentBoardCoordinates.x},{currentBoardCoordinates.y})");

        return newPosition;
    }

    Vector2 CheckHorizontalWallDirect(int boardY, int boardX)
    {
        // 가로벽: boardY는 홀수, boardX는 첫 번째 짝수 위치
        int boardX2 = boardX + 2; // 두 번째 짝수 위치
        
        // 보드 범위 확인
        if (boardY < 1 || boardY > 15 || boardX < 0 || boardX > 16 || boardX2 < 0 || boardX2 > 16)
            return Vector2.zero;
        
        // Board 클래스의 is_wall_valid 함수로 유효성 검사
        if (gameBoard != null && gameBoard.is_wall_valid(boardY, boardX, boardY, boardX2))
        {
            // RectTransform 좌표계로 변환 (Board 좌표를 Unity UI 좌표로)
            float gridSize = cellSize;
            float unityX = ((boardX + 1) * (gridSize / 2.0f)) - 396.8182f; // 벽 중심 X
            float unityY = 396.8182f - (boardY * (gridSize / 2.0f)); // 벽 중심 Y
            
            return new Vector2(unityX, unityY);
        }
        
        return Vector2.zero;
    }
    
    Vector2 CheckVerticalWallDirect(int boardY, int boardX)
    {
        // 세로벽: boardX는 홀수, boardY는 첫 번째 짝수 위치
        int boardY2 = boardY + 2; // 두 번째 짝수 위치
        
        // 보드 범위 확인
        if (boardX < 1 || boardX > 15 || boardY < 0 || boardY > 16 || boardY2 < 0 || boardY2 > 16)
            return Vector2.zero;
        
        // Board 클래스의 is_wall_valid 함수로 유효성 검사
        if (gameBoard != null && gameBoard.is_wall_valid(boardY, boardX, boardY2, boardX))
        {
            // RectTransform 좌표계로 변환 (Board 좌표를 Unity UI 좌표로)
            float gridSize = cellSize;
            float unityX = (boardX * (gridSize / 2.0f)) - 396.8182f; // 벽 중심 X
            float unityY = 396.8182f - ((boardY + 1) * (gridSize / 2.0f)); // 벽 중심 Y
            
            return new Vector2(unityX, unityY);
        }
        
        return Vector2.zero;
    }
    
    Vector2 CheckHorizontalWall(int gridX, int gridY)
    {
        // 가로벽: Y는 홀수(플레이어 행 사이), X는 두 개의 짝수 위치
        int boardY = gridY * 2 + 1; // 홀수 Y
        int boardX1 = gridX * 2;    // 첫 번째 짝수 X
        int boardX2 = boardX1 + 2;  // 두 번째 짝수 X
        
        // 보드 범위 확인
        if (boardY < 1 || boardY > 15 || boardX1 < 0 || boardX1 > 16 || boardX2 < 0 || boardX2 > 16)
            return Vector2.zero;
        
        // Board 클래스의 is_wall_valid 함수로 유횤성 검사
        if (gameBoard != null && gameBoard.is_wall_valid(boardY, boardX1, boardY, boardX2))
        {
            // RectTransform 좌표계로 변환
            float gridSize = cellSize;
            float unityX = (gridX + 0.5f) * gridSize - 396.8182f;
            float unityY = 396.8182f - (gridY + 0.5f) * gridSize;
            
            return new Vector2(unityX, unityY);
        }
        
        return Vector2.zero;
    }
    
    Vector2 CheckVerticalWall(int gridX, int gridY)
    {
        // 세로벽: X는 홀수(플레이어 열 사이), Y는 두 개의 짝수 위치
        int boardX = gridX * 2 + 1; // 홀수 X
        int boardY1 = gridY * 2;    // 첫 번째 짝수 Y
        int boardY2 = boardY1 + 2;  // 두 번째 짝수 Y
        
        // 보드 범위 확인
        if (boardX < 1 || boardX > 15 || boardY1 < 0 || boardY1 > 16 || boardY2 < 0 || boardY2 > 16)
            return Vector2.zero;
        
        // Board 클래스의 is_wall_valid 함수로 유횤성 검사
        if (gameBoard != null && gameBoard.is_wall_valid(boardY1, boardX, boardY2, boardX))
        {
            // RectTransform 좌표계로 변환
            float gridSize = cellSize;
            float unityX = (gridX + 0.5f) * gridSize - 396.8182f;
            float unityY = 396.8182f - (gridY + 0.5f) * gridSize;
            
            return new Vector2(unityX, unityY);
        }
        
        return Vector2.zero;
    }
    
    bool ValidateWallPlacement(Vector2 position)
    {
        // 보드 범위 내 위치인지 확인
        return position != Vector2.zero;
    }
    
    void ShowWallPreview(Vector2 position)
    {
        // ShowWallPreview 입력값 로그
        Debug.Log($"[SHOW_PREVIEW] Input position: {position}, IsHorizontal: {isWallHorizontal}, CurrentBoardCoordinates: ({currentBoardCoordinates.x},{currentBoardCoordinates.y})");

        // 현재 벽 위치 저장
        currentWallPosition = position;

        // 벽 배치 유효성 검사
        isWallPositionValid = ValidateWallPlacement(position);

        // 기존 미리보기 제거
        ClearWallPreview();

        if (position == Vector2.zero)
        {
            return;
        }
        
        // 새로운 벽 미리보기 생성
        if (wallPrefab != null && gameCanvas != null)
        {
            currentWallPreview = Instantiate(wallPrefab, gameCanvas.transform);
            
            // 위치 설정
            RectTransform wallRect = currentWallPreview.GetComponent<RectTransform>();
            if (wallRect != null)
            {
                wallRect.anchoredPosition = position;

                // 회전 설정 (가로/세로)
                wallRect.rotation = isWallHorizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90);
            }
            
            // 벽의 색상을 유효성에 따라 변경
            UpdateWallPreviewColor();
            
            // 깜박임 효과 시작
            StartCoroutine(BlinkWallPreview());
        }
        
        // 버튼 상태 업데이트
        UpdateButtonStates();
    }
    
    void ClearWallPreview()
    {
        if (currentWallPreview != null)
        {
            Destroy(currentWallPreview);
            currentWallPreview = null;
        }
    }
    
    void UpdateWallPreviewColor()
    {
        if (currentWallPreview == null) return;
        
        Image wallImage = currentWallPreview.GetComponent<Image>();
        if (wallImage != null)
        {
            if (isWallPositionValid)
            {
                wallImage.color = new Color(0f, 1f, 0f, 0.8f); // 유효한 위치: 밝은 초록색
            }
            else
            {
                wallImage.color = new Color(1f, 0f, 0f, 0.8f); // 유효하지 않은 위치: 밝은 빨간색
            }
        }
    }
    
    IEnumerator BlinkWallPreview()
    {
        while (currentWallPreview != null && isWallMode)
        {
            Image wallImage = currentWallPreview?.GetComponent<Image>();
            if (wallImage == null) yield break;
            
            Color originalColor = wallImage.color;
            
            // 투명하게
            wallImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.3f);
            yield return new WaitForSeconds(0.5f);
            
            // 다시 확인 (오브젝트가 파괴되었을 수 있음)
            if (currentWallPreview == null || !isWallMode) break;
            wallImage = currentWallPreview?.GetComponent<Image>();
            if (wallImage == null) break;
            
            // 불투명하게
            wallImage.color = originalColor;
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    void HandleOpponentWall(string posString)
    {
        
        // 벽 위치 파싱: "y1,x1 y2,x2" 형식 또는 다른 형식들
        string[] wallCoords = posString.Split(' ');
        
        if (wallCoords.Length != 2) 
        {
            return;
        }
        
        string[] coord1 = wallCoords[0].Split(',');
        string[] coord2 = wallCoords[1].Split(',');
        
        
        if (coord1.Length != 2 || coord2.Length != 2) 
        {
            return;
        }
        
        if (int.TryParse(coord1[0], out int y1) && int.TryParse(coord1[1], out int x1) &&
            int.TryParse(coord2[0], out int y2) && int.TryParse(coord2[1], out int x2))
        {
            // 상대방 벽 좌표 디버깅
            bool isHorizontal = (y1 == y2);
            Debug.Log($"[OPPONENT_WALL] {(isHorizontal ? "가로벽" : "세로벽")} - 보드좌표: ({y1},{x1})-({y2},{x2})");
            
            // 보드에 벽 추가 (Red 관점 좌표 그대로 사용)
            if (gameBoard != null)
            {
                gameBoard.put_wall(y1, x1, y2, x2);
            }
            
            // Unity에서 벽 시각화
            VisualizeOpponentWall(y1, x1, y2, x2);

            // 상대의 수를 둘 때 소리 재생
            PlayMoveSound();

            // 상대방 벽 개수 감소 및 UI 업데이트
            opponentWallCount--;
            UpdateWallCountUI();
        }
    }
    
    void VisualizeOpponentWall(int y1, int x1, int y2, int x2)
    {
        
        if (wallPrefab == null)
        {
            return;
        }
        
        if (gameCanvas == null)
        {
            return;
        }
        
        
        // 벽 방향 확인
        bool isHorizontal = (y1 == y2); // Y가 같으면 가로벽
        
        // Blue 플레이어의 경우 벽 좌표를 180도 회전 변환
        int displayY1, displayX1, displayY2, displayX2;
        bool displayIsHorizontal = isHorizontal;
        
        if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            // Blue 플레이어: 180도 회전 변환 (16에서 빼기)
            displayY1 = 16 - y1;
            displayX1 = 16 - x1;
            displayY2 = 16 - y2;
            displayX2 = 16 - x2;
            
        }
        else
        {
            // Red 플레이어: 변환 없음
            displayY1 = y1;
            displayX1 = x1;
            displayY2 = y2;
            displayX2 = x2;
        }
        
        // 변환된 좌표의 벽 중심 위치 계산
        float centerY = (displayY1 + displayY2) / 2.0f;
        float centerX = (displayX1 + displayX2) / 2.0f;
        
        // 변환된 좌표를 화면 좌표로 변환
        Vector3 wallWorldPos = ServerCoordToWorldPosition((int)centerY, (int)centerX);
        
        // 벽 오브젝트 생성
        GameObject opponentWall = Instantiate(wallPrefab, gameCanvas.transform);
        opponentWall.name = $"OpponentWall_{displayIsHorizontal}_{displayY1}_{displayX1}"; // 디버그용 이름
        
        // 위치와 회전 설정
        RectTransform wallRect = opponentWall.GetComponent<RectTransform>();
        if (wallRect != null)
        {
            wallRect.anchoredPosition = new Vector2(wallWorldPos.x, wallWorldPos.y);
            wallRect.rotation = displayIsHorizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90);

            // 보드 앞쪽에 보이도록 순서 설정
            wallRect.SetAsLastSibling();
        }
        
        // 상대방 벽 색상 설정
        Image wallImage = opponentWall.GetComponent<Image>();
        if (wallImage != null)
        {
            wallImage.color = Color.white; // 원래 색상 (흰색)
        }
    }
    
    void SendWallToServerWebSocket(int boardY, int boardX, bool horizontal)
    {
        // Blue 플레이어인 경우 서버로 보낼 좌표를 Red 관점으로 변환
        int serverBoardY = boardY;
        int serverBoardX = boardX;
        
        int y1, x1, y2, x2;
        
        if (horizontal)
        {
            // 가로벽: Y는 동일, X는 연속
            y1 = y2 = serverBoardY;
            x1 = serverBoardX;
            x2 = serverBoardX + 2;
        }
        else
        {
            // 세로벽: X는 동일, Y는 연속
            x1 = x2 = serverBoardX;
            y1 = serverBoardY;
            y2 = serverBoardY + 2;
        }
        
        // Blitz 모드일 경우 시간 증가
        AddIncrementTime();
        
        // 벽 배치 후 승패 판정 (상대방이 막혔는지 확인)
        string gameProgress = "Continue";
        if (gameBoard != null)
        {
            int winStatus = gameBoard.win_or_lose();
            if (winStatus == 1) // 승리
            {
                gameProgress = "Won";
                isGameEnded = true; // 게임 종료 플래그 설정
                // 승리 시 결과창 표시
                ShowGameResult("Won");
            }
            else if (winStatus == 2) // 패배
            {
                gameProgress = "Lost";
                isGameEnded = true; // 게임 종료 플래그 설정
                // 패배 시 결과창 표시
                ShowGameResult("Lost");
            }
        }
        
        // 서버 메시지 형식: "TimeControl GameToken UserName Wall Pos RemainTime GameProgress"
        string timeControl = TimeControlManager.currentTimeControl;
        string pos = $"{y1},{x1} {y2},{x2}"; // 벽은 두 좌표 쌍
        string remainTime = myTimeRemaining.ToString("F1");
        
        string message = $"{timeControl} {gameToken} {myUsername} Wall {pos} {remainTime} {gameProgress}";
        
        // 메시지 파트 수 확인
        string[] messageParts = message.Split(' ');
        
        _ = SendMessageAsync(message);
    }
    
    void OnMoveButtonClicked(int boardY, int boardX)
    {
        // 1. 로컬 보드 상태 업데이트
        if (gameBoard != null)
        {
            gameBoard.make_my_move(boardY, boardX);
        }

        // 2. Unity에서 플레이어 말 위치 이동
        MovePLayerPiece(boardY, boardX);

        // 3. 수를 둘 때 소리 재생
        PlayMoveSound();

        // 4. 이동 버튼들 제거
        ClearPossibleMoveButtons();

        // 5. Move Cancel 상태에서 일반 상태로 복귀
        ShowMoveButtons();

        // 6. 턴 전환 (내가 수를 둔 후 상대방 차례)
        SwitchTurn();

        // 7. 버튼 상태 즉시 업데이트
        UpdateButtonStates();

        // 8. 서버에 이동 전송
        SendMoveToServerWebSocket(boardY, boardX);
    }
    
    void MovePLayerPiece(int boardY, int boardX)
    {
        // 보드 좌표를 Unity 월드 좌표로 변환
        Vector3 worldPosition = BoardToWorldPosition(boardY, boardX);
        
        // 내 색깔에 해당하는 플레이어 오브젝트 이동
        GameObject myPlayerObject = null;
        if (myPlayerColor.Equals("Red", System.StringComparison.OrdinalIgnoreCase))
        {
            myPlayerObject = playerRedObject;
        }
        else if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            myPlayerObject = playerBlueObject;
        }
        
        if (myPlayerObject != null)
        {
            // RectTransform인지 Transform인지 확인 후 위치 설정
            RectTransform rectTransform = myPlayerObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localPosition = worldPosition;
            }
            else
            {
                myPlayerObject.transform.localPosition = worldPosition;
            }
            
        }
        else
        {
        }
    }
    
    
    
    
    
    
    
    

    void SetupGameWebSocket()
    {
        // Unity 에디터에서 직접 실행한 경우의 처리
        if (string.IsNullOrEmpty(gameToken) || gameToken == "Test Token")
        {
            return;
        }
        
        // 이미 수신 중이면 중복 시작 방지
        if (isReceivingMessages)
        {
            return;
        }
        
        // 기존 cancellationTokenSource 정리
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
        
        // 새로운 게임 WebSocket 연결 생성
        _ = ConnectToGameWebSocketAsync();
    }
    
    async Task ConnectToGameWebSocketAsync()
    {
        try
        {
            gameWebSocket = new ClientWebSocket();
            cancellationTokenSource = new CancellationTokenSource();
            
            // 게임 WebSocket URL
            string wsUrl = $"{ServerConfig.GetWebSocketUrl()}/game";
            var uri = new Uri(wsUrl);
            
            await gameWebSocket.ConnectAsync(uri, cancellationTokenSource.Token);
            
            
            // 게임 연결 등록 메시지 전송
            string connectMessage = $"{TimeControlManager.currentTimeControl} {gameToken} {myUsername} Connect";
            await SendMessageAsync(connectMessage);
            
            // 메시지 수신 시작
            _ = ReceiveGameMessagesAsync();
        }
        catch (Exception ex)
        {
            mainThreadActions.Enqueue(() =>
            {
            });
        }
    }
    
    async Task ReceiveGameMessagesAsync()
    {
        if (gameWebSocket == null || isReceivingMessages) 
        {
            return;
        }
        
        isReceivingMessages = true;
        var buffer = new byte[1024 * 4];
        
        
        try
        {
            while (gameWebSocket.State == WebSocketState.Open && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = await gameWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    // 메인 스레드에서 처리
                    mainThreadActions.Enqueue(() =>
                    {
                        HandleGameMessage(message);
                    });
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    mainThreadActions.Enqueue(() =>
                    {
                        HandleWebSocketDisconnect();
                    });
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
                HandleWebSocketDisconnect();
            });
        }
        finally
        {
            isReceivingMessages = false;
        }
    }
    
    void HandleGameMessage(string message)
    {
        
        // 서버 메시지 형식을 정확히 파악하기 위해 전체 메시지 분석
        string[] parts = message.Split(' ');
        
        string moveOrWall;
        string pos;
        string remainTime;
        string gameProgress;
        
        // 메시지 형식 판별: 4개 부분 vs 8개 부분
        if (parts.Length == 4)
        {
            // 서버에서 클라이언트로 보내는 형식: "Move 14,8 180.0 Continue"
            moveOrWall = parts[0];
            pos = parts[1];
            remainTime = parts[2]; 
            gameProgress = parts[3];
        }
        else if (parts.Length == 5)
        {
            // 벽의 경우: "Wall 15,10 15,8 180.0 Continue"
            moveOrWall = parts[0];
            pos = $"{parts[1]} {parts[2]}";
            remainTime = parts[3];
            gameProgress = parts[4];
        }
        else if (parts.Length >= 7)
        {
            // 클라이언트에서 서버로 보내는 형식: "TimeControl GameToken UserName Move_or_Wall ..."
            string timeControl = parts[0];  // "Rapid" 
            string gameToken = parts[1];    // "1757661406"
            string userName = parts[2];     // "test1"
            moveOrWall = parts[3];   // "Wall" or "Move"
            
            if (moveOrWall == "Wall" && parts.Length >= 8)
            {
                pos = $"{parts[4]} {parts[5]}";
                remainTime = parts[6];
                gameProgress = parts[7];
            }
            else if (moveOrWall == "Move" && parts.Length >= 7)
            {
                pos = parts[4];
                remainTime = parts[5];
                gameProgress = parts[6];
            }
            else
            {
                return;
            }
        }
        else
        {
            return;
        }
        
        // 파싱된 데이터로 처리
        
        
        // 상대방 시간 업데이트
        if (float.TryParse(remainTime, out float newTime))
        {
            opponentTimeRemaining = newTime;
        }
        
        // 게임 진행 상황 확인
        if (gameProgress == "Win" || gameProgress == "Lose")
        {
            HandleGameEnd(gameProgress);
            return;
        }
        
        // 상대방 이동 처리
        if (moveOrWall == "Move")
        {
            HandleOpponentMove(pos);
        }
        else if (moveOrWall == "Wall")
        {
            HandleOpponentWall(pos);
        }
        else if (moveOrWall == "GameEnd")
        {
            // 게임 종료 전 상대방의 마지막 수 반영
            if (pos != "0,0")
            {
                Debug.Log($"[GAME_END] Applying opponent's final move at position: {pos}");

                // 기존 HandleOpponentMove 함수를 사용해서 상대방의 마지막 수 처리
                HandleOpponentMove(pos);
            }

            // 게임 종료 처리 (서버에서 보낸 최종 결과)
            HandleGameEnd(gameProgress);
            return; // 게임 종료이므로 턴 전환하지 않음
        }
        else if (moveOrWall == "OpponentDisconnect")
        {
            // 상대방 연결 끊김으로 인한 승리
            HandleGameEnd("OpponentDisconnect");
            return;
        }
        
        // 상대방의 수를 받았으므로 이제 내 차례 - 턴 전환
        SwitchTurn();
    }
    
    void HandleOpponentMove(string posString)
    {
        // 위치 파싱: "x,y" 형식
        string[] coords = posString.Split(',');
        if (coords.Length != 2)
        {
            return;
        }
        
        if (int.TryParse(coords[0], out int boardY) && int.TryParse(coords[1], out int boardX))
        {
            
            // 보드 상태 업데이트 (Red 관점 좌표 그대로 사용)
            if (gameBoard != null)
            {
                // 상대방 이전 위치 제거
                gameBoard.board[gameBoard.opponent_pos[0], gameBoard.opponent_pos[1]] = 0;
                // 새 위치에 상대방 배치 (Red 관점 좌표 그대로)
                gameBoard.board[boardY, boardX] = gameBoard.opponent_color;
                gameBoard.opponent_pos[0] = boardY;
                gameBoard.opponent_pos[1] = boardX;
            }
            
            // Unity에서 상대방 말 이동 (Red 관점 좌표, BoardToWorldPosition에서 변환)
            MoveOpponentPiece(boardY, boardX);

            // 상대의 수를 둘 때 소리 재생
            PlayMoveSound();
        }
        else
        {
        }
    }
    
    void MoveOpponentPiece(int boardY, int boardX)
    {
        Vector3 worldPosition = BoardToWorldPosition(boardY, boardX);
        
        // 상대방 색깔에 해당하는 플레이어 오브젝트 이동
        GameObject opponentPlayerObject = null;
        if (myPlayerColor.Equals("Red", System.StringComparison.OrdinalIgnoreCase))
        {
            opponentPlayerObject = playerBlueObject; // 내가 Red면 상대는 Blue
        }
        else if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            opponentPlayerObject = playerRedObject; // 내가 Blue면 상대는 Red
        }
        
        if (opponentPlayerObject != null)
        {
            RectTransform rectTransform = opponentPlayerObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localPosition = worldPosition;
            }
            else
            {
                opponentPlayerObject.transform.localPosition = worldPosition;
            }
            
        }
        else
        {
        }
    }
    
    
    
    void HandleGameEnd(string result)
    {

        // 게임이 이미 끝났으면 중복 처리 방지
        if (isGameEnded && result == "Lost")
        {
            return;
        }

        isGameEnded = true; // 게임 종료 플래그 설정

        // 게임 횟수 소비 (한 번만)
        if (!hasConsumedGameCredit && GameCreditManager.Instance != null)
        {
            hasConsumedGameCredit = true;
            GameCreditManager.Instance.ConsumeGame((success) =>
            {
                if (success)
                {
                    Debug.Log("[GameManager] Game credit consumed after game end");
                }
                else
                {
                    Debug.LogError("[GameManager] Failed to consume game credit after game end");
                }
            });
        }

        // 게임 버튼들 비활성화
        if (moveButton != null) moveButton.interactable = false;
        if (wallButton != null) wallButton.interactable = false;
        if (moveCancelButton != null) moveCancelButton.interactable = false;
        if (wallCancelButton != null) wallCancelButton.interactable = false;
        if (rotateButton != null) rotateButton.interactable = false;
        if (placeButton != null) placeButton.interactable = false;

        // 결과창 표시
        ShowGameResult(result);
    }
    
    void ShowGameResult(string result)
    {

        if (gameResultPanel != null && gameResultText != null)
        {
            string resultMessage = "";

            if (result == "Won")
            {
                resultMessage = "You Won!";
            }
            else if (result == "Lost")
            {
                resultMessage = "You Lost...";
            }
            else if (result == "OpponentDisconnect")
            {
                resultMessage = "Opponent disconnected. You Won!";
            }
            else
            {
                resultMessage = "Game Ended";
            }
            
            gameResultText.text = resultMessage;

            // 패널을 UI 계층의 맨 위로 이동
            gameResultPanel.transform.SetAsLastSibling();

            gameResultPanel.SetActive(true);
        }
    }
    
    public void OnBackToMenuButtonClicked()
    {
        // WebSocket 연결 정리
        CleanupWebSocket();
        
        // StartScene으로 이동
        SceneManager.LoadScene("StartScene");
    }
    
    public void OnForfeitButtonClicked()
    {
        // 게임 포기 처리
        StartCoroutine(ValidateSessionAndProceed(() => 
        {
            ForfeitGame();
        }));
    }
    
    void ForfeitGame()
    {
        // 포기 메시지를 서버에 전송
        string timeControl = TimeControlManager.currentTimeControl;
        string forfeitMessage = $"{timeControl} {gameToken} {myUsername} Forfeit 0,0 0.0 Lost";

        if (gameWebSocket != null && gameWebSocket.State == WebSocketState.Open)
        {
            _ = SendMessageAsync(forfeitMessage);
        }

        // 게임 종료 처리 (게임 횟수 소비 포함)
        HandleGameEnd("Lost");

        // 잠시 후 StartScene으로 이동
        StartCoroutine(DelayedSceneTransition());
    }
    
    IEnumerator DelayedSceneTransition()
    {
        yield return new WaitForSeconds(2f);
        
        // WebSocket 연결 정리
        CleanupWebSocket();
        
        // StartScene으로 이동
        SceneManager.LoadScene("StartScene");
    }
    
    void CleanupWebSocket()
    {
        // WebSocket 수신 중지
        isReceivingMessages = false;
        
        // 게임이 진행 중이었다면 disconnect 메시지 전송
        if (gameWebSocket != null && gameWebSocket.State == WebSocketState.Open &&
            !string.IsNullOrEmpty(gameToken) && gameToken != "Test Token" &&
            !isGameEnded)
        {
            // 일반적인 게임 종료가 아닌 경우에만 disconnect 메시지 전송
            SendDisconnectMessage();
        }
        
        // WebSocket 정리
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
        
        // 게임 WebSocket 정리
        if (gameWebSocket != null)
        {
            if (gameWebSocket.State == WebSocketState.Open)
            {
                _ = gameWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Game ended", CancellationToken.None);
            }
            gameWebSocket.Dispose();
            gameWebSocket = null;
        }
    }
    
    async Task SendMessageAsync(string message)
    {
        if (gameWebSocket == null || gameWebSocket.State != WebSocketState.Open)
        {
            return;
        }
        
        try
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            var buffer = new ArraySegment<byte>(messageBytes);
            await gameWebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationTokenSource?.Token ?? CancellationToken.None);
        }
        catch (Exception ex)
        {
        }
    }
    
    void SendMoveToServerWebSocket(int boardY, int boardX)
    {
        // Blue 플레이어인 경우 서버로 보낼 좌표를 Red 관점으로 변환
        int serverBoardY = boardY;
        int serverBoardX = boardX;
        
        // Blitz 모드일 경우 시간 증가
        AddIncrementTime();
        
        // 이동 후 승패 판정
        string gameProgress = "Continue";
        if (gameBoard != null)
        {
            int winStatus = gameBoard.win_or_lose();
            if (winStatus == 1) // 승리
            {
                gameProgress = "Won";
                isGameEnded = true; // 게임 종료 플래그 설정
                // 승리 시 결과창 표시
                ShowGameResult("Won");
            }
            else if (winStatus == 2) // 패배
            {
                gameProgress = "Lost";
                isGameEnded = true; // 게임 종료 플래그 설정
                // 패배 시 결과창 표시
                ShowGameResult("Lost");
            }
        }
        
        // 서버 메시지 형식: "TimeControl GameToken UserName Move_or_Wall Pos Remain_Time Game_Progress"
        string timeControl = TimeControlManager.currentTimeControl;
        string pos = $"{serverBoardY},{serverBoardX}";
        string remainTime = myTimeRemaining.ToString("F1");
        
        string message = $"{timeControl} {gameToken} {myUsername} Move {pos} {remainTime} {gameProgress}";
        
        _ = SendMessageAsync(message);
    }
    
    
    
    IEnumerator SendMoveToServer(int boardY, int boardX)
    {
        string serverUrl = ServerConfig.GetHttpUrl();
        string endpoint = "/game/move";
        string fullUrl = serverUrl + endpoint;
        
        // JSON 데이터 생성
        var moveData = new
        {
            gameToken = this.gameToken,
            player = myPlayerColor,
            move = new int[] { boardY, boardX }
        };
        
        string jsonData = JsonUtility.ToJson(moveData);
        
        // HTTP POST 요청 생성
        using (UnityWebRequest request = new UnityWebRequest(fullUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            // 요청 전송 및 응답 대기
            yield return request.SendWebRequest();
            
            // 응답 처리 (에러만 로그)
            if (request.result != UnityWebRequest.Result.Success)
            {
            }
        }
    }

    void InitializeButtons()
    {
        // 초기 상태: Move, Wall 버튼은 활성화, Cancel 버튼들과 Container는 비활성화
        ShowMoveButtons();

        // 버튼 이벤트 연결
        if (moveButton != null)
            moveButton.onClick.AddListener(OnMoveButtonClicked);

        if (moveCancelButton != null)
            moveCancelButton.onClick.AddListener(OnMoveCancelButtonClicked);
            
        if (wallButton != null)
            wallButton.onClick.AddListener(OnWallButtonClicked);
            
        if (wallCancelButton != null)
            wallCancelButton.onClick.AddListener(OnWallCancelButtonClicked);
            
        if (rotateButton != null)
            rotateButton.onClick.AddListener(OnRotateButtonClicked);
            
        if (forfeitButton != null)
            forfeitButton.onClick.AddListener(OnForfeitButtonClicked);
            
        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(OnBackToMenuButtonClicked);
            
        // 초기 버튼 상태 설정
        UpdateButtonStates();
    }

    public void OnMoveButtonClicked()
    {
        StartCoroutine(ValidateSessionAndProceed(() => 
        {
            ShowMoveCancelButton();
            ShowPossibleMoves();
        }));
    }

    public void OnMoveCancelButtonClicked()
    {
        // 이동 가능한 칸 버튼들 제거
        ClearPossibleMoveButtons();
        
        ShowMoveButtons();
    }

    private void ShowMoveButtons()
    {
        if (moveButton != null)
            moveButton.gameObject.SetActive(true);
            
        if (wallButton != null)
            wallButton.gameObject.SetActive(true);
        
        if (moveCancelButton != null)
            moveCancelButton.gameObject.SetActive(false);
            
        if (wallContainer != null)
            wallContainer.SetActive(false);
            
        isWallMode = false;
        ClearWallPreview();
    }

    private void ShowMoveCancelButton()
    {
        if (moveButton != null)
            moveButton.gameObject.SetActive(false);
            
        if (wallButton != null)
            wallButton.gameObject.SetActive(false);
        
        if (moveCancelButton != null)
            moveCancelButton.gameObject.SetActive(true);
            
        if (wallContainer != null)
            wallContainer.SetActive(false);
            
        isWallMode = false;
        ClearWallPreview();
    }
    
    private void ShowWallButtons()
    {
        if (moveButton != null)
            moveButton.gameObject.SetActive(false);
            
        if (wallButton != null)
            wallButton.gameObject.SetActive(false);
            
        if (moveCancelButton != null)
            moveCancelButton.gameObject.SetActive(false);
        
        if (wallContainer != null)
            wallContainer.SetActive(true);

        isWallMode = true;
        isWallPositionValid = false;
    }
    
    public void OnWallButtonClicked()
    {
        StartCoroutine(ValidateSessionAndProceed(() => 
        {
            ShowWallButtons();
        }));
    }
    
    public void OnWallCancelButtonClicked()
    {
        ShowMoveButtons();
    }
    
    public void OnRotateButtonClicked()
    {
        isWallHorizontal = !isWallHorizontal;

        // 회전 후 보드 좌표 재계산 및 미리보기 업데이트
        if (currentWallPosition != Vector2.zero)
        {
            // 현재 UI 위치를 기반으로 새로운 벽 방향에 맞는 보드 좌표 재계산
            Vector2 newPosition = RecalculateWallCoordinatesFromUIPosition(currentWallPosition);
            ShowWallPreview(newPosition);
        }
    }
    
    public void OnPlaceButtonClicked()
    {
        StartCoroutine(ValidateSessionAndProceed(() =>
        {
            if (!isWallPositionValid || currentBoardCoordinates == Vector2Int.zero) return;

            // 1. 로컬 보드 상태 업데이트
            PlaceWallOnBoard();

            // 2. 수를 둘 때 소리 재생
            PlayMoveSound();

            // 3. 미리보기 제거
            ClearWallPreview();

            // 4. Move Cancel 상태에서 일반 상태로 복귀 (Move 버튼과 동일한 순서)
            ShowMoveButtons();

            // 5. 턴 전환 (내가 벽을 둔 후 상대방 차례)
            SwitchTurn();

            // 6. 버튼 상태 즉시 업데이트
            UpdateButtonStates();

            // 7. 서버에 벽 전송
            SendWallToServerWebSocket(currentBoardCoordinates.x, currentBoardCoordinates.y, isWallHorizontal);
        }));
    }
    
    void PlaceWallOnBoard()
    {
        if (gameBoard == null) return;
        
        
        int y1, x1, y2, x2;
        
        if (isWallHorizontal)
        {
            // 가로벽: currentBoardCoordinates.x = boardY, currentBoardCoordinates.y = boardX
            y1 = y2 = currentBoardCoordinates.x;  // boardY
            x1 = currentBoardCoordinates.y;       // boardX
            x2 = x1 + 2;
        }
        else
        {
            // 세로벽: currentBoardCoordinates.x = boardY, currentBoardCoordinates.y = boardX
            x1 = x2 = currentBoardCoordinates.y;  // boardX
            y1 = currentBoardCoordinates.x;       // boardY
            y2 = y1 + 2;
        }

        // 벽 좌표 디버깅
        Debug.Log($"[WALL] {(isWallHorizontal ? "가로벽" : "세로벽")} - 보드좌표: ({y1},{x1})-({y2},{x2}), UI좌표: {currentWallPosition}");

        gameBoard.put_wall(y1, x1, y2, x2);

        // 내 벽 개수 감소 및 UI 업데이트
        myWallCount--;
        UpdateWallCountUI();
        
        // 보드에 실제 벽 시각화 (미리보기가 아닌 실제 벽)
        if (wallPrefab != null && gameCanvas != null)
        {
            GameObject actualWall = Instantiate(wallPrefab, gameCanvas.transform);
            actualWall.name = $"MyWall_{isWallHorizontal}_{currentBoardCoordinates.x}_{currentBoardCoordinates.y}"; // 디버그용 이름
            
            RectTransform wallRect = actualWall.GetComponent<RectTransform>();
            if (wallRect != null)
            {
                wallRect.anchoredPosition = currentWallPosition;
                wallRect.rotation = isWallHorizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90);

                // 보드 앞쪽에 보이도록 순서 설정
                wallRect.SetAsLastSibling();
            }
            
            // 내 벽 색상 및 크기 설정
            Image wallImage = actualWall.GetComponent<Image>();
            if (wallImage != null)
            {
                wallImage.color = Color.white; // 원래 색상 (흰색)
            }
        }
    }
    
    IEnumerator ValidateSessionAndProceed(System.Action onSuccess)
    {
        string serverUrl = ServerConfig.GetHttpUrl();
        string endpoint = "/me";
        string fullUrl = serverUrl + endpoint;
        
        string token = SessionData.GetToken();
        if (string.IsNullOrEmpty(token))
        {
            ShowSessionWarningAndQuit();
            yield break;
        }
        
        using (UnityWebRequest request = UnityWebRequest.Get(fullUrl))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            
            yield return request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success || request.responseCode == 401)
            {
                ShowSessionWarningAndQuit();
            }
            else
            {
                onSuccess?.Invoke();
            }
        }
    }
    
    void ShowSessionWarningAndQuit()
    {
        if (sessionWarningPanel != null)
        {
            sessionWarningPanel.SetActive(true);
            
            // 상대방에게 패배 메시지 전송
            SendForfeitMessage();
            
            StartCoroutine(ShowWarningCountdown());
        }
        else
        {
            Application.Quit();
        }
    }
    
    IEnumerator ShowWarningCountdown()
    {
        for (int i = 3; i > 0; i--)
        {
            if (sessionWarningText != null)
            {
                sessionWarningText.text = $"Your account logged in another device. Program terminates in {i} seconds....";
            }
            yield return new WaitForSeconds(1.0f);
        }
        
        Application.Quit();
    }
    
    void SendForfeitMessage()
    {
        string timeControl = TimeControlManager.currentTimeControl;
        string pos = "0,0";
        string remainTime = "0.0";
        string gameProgress = "Lost";
        
        string message = $"{timeControl} {gameToken} {myUsername} Forfeit {pos} {remainTime} {gameProgress}";
        
        _ = SendMessageAsync(message);
    }
    
    void SendDisconnectMessage()
    {
        string timeControl = TimeControlManager.currentTimeControl;
        string pos = "0,0";
        string remainTime = "0.0";
        string gameProgress = "Lost";
        
        string message = $"{timeControl} {gameToken} {myUsername} Disconnect {pos} {remainTime} {gameProgress}";
        
        _ = SendMessageAsync(message);
    }
    
    void HandleWebSocketDisconnect()
    {

        // 게임이 이미 끝났으면 연결 끊김 무시
        if (isGameEnded)
        {
            return;
        }

        // 연결이 끊어졌을 때 서버에 패배 메시지 전송 시도
        SendDisconnectMessage();

        // 게임 종료 처리
        isGameEnded = true;
        HandleGameEnd("Lost");
    }
    
    void OnDestroy()
    {
        CleanupWebSocket();
    }

    // 디버그용 - 위치 강제 재설정
    [ContextMenu("Reset Player Positions")]
    public void ResetPlayerPositions()
    {
        SetupPlayerPositions();
    }
    
    // 디버그용 - 강제로 이동 가능한 칸 표시
    [ContextMenu("Debug Show Possible Moves")]
    public void DebugShowPossibleMoves()
    {
        ShowPossibleMoves();
    }
    
    // 디버그용 - 테스트 버튼 생성
    [ContextMenu("Create Test Button")]
    public void CreateTestButton()
    {
        Vector3 testPos = new Vector3(0, 0, 0);
        GameObject testBtn = CreateMoveButton(testPos, 1, 1);
    }
}

public class Board
{
    public int[,] board = new int[17, 17]; // 0 = Blank, 1 = Red, 2 = Blue, 3 = Wall
    // odd coordinates are for players, even coordinates is for walls.
    public int[] my_pos = { 0, 0 };
    public int[] opponent_pos = { 0, 0 };
    public int my_color = 0; // 1 = Red, 2 = Blue
    public int opponent_color = 0;
    int wall_num = 10;
    public List<List<int>> possible_moves = new List<List<int>> { };

    public Board(int color)
    {
        this.my_color = color;

        // 모든 좌표는 Red 플레이어 관점 기준으로 통일
        // Red는 항상 (16,8)에서 시작, Blue는 항상 (0,8)에서 시작
        if (color == 1) // Red 플레이어
        {
            this.my_pos[0] = 16;        // Red 시작 위치
            this.my_pos[1] = 8;
            this.opponent_pos[0] = 0;   // Blue 시작 위치
            this.opponent_pos[1] = 8;
            this.opponent_color = 2;
        }
        else // Blue 플레이어
        {
            this.my_pos[0] = 0;         // Blue 시작 위치 (Red 관점 좌표)
            this.my_pos[1] = 8;
            this.opponent_pos[0] = 16;  // Red 시작 위치 (Red 관점 좌표)
            this.opponent_pos[1] = 8;
            this.opponent_color = 1;
        }

        // 보드 초기화 (17x17 전체)
        for (int i = 0; i < 17; i++)
        {
            for (int j = 0; j < 17; j++)
            {
                this.board[i, j] = 0;
            }
        }
        
        // 플레이어 초기 위치에 표시
        this.board[this.my_pos[0], this.my_pos[1]] = this.my_color;
        this.board[this.opponent_pos[0], this.opponent_pos[1]] = this.opponent_color;

    }

    public int win_or_lose() // Not Ended: 0 , win: 1, lose : 2
    {
        for (int i = 0; i < 17; i = i + 2)
        {
            if (this.board[0, i] == 1)
            {
                if (this.my_color == 1)
                {
                    return 1; // win
                }
                else
                {
                    return 2; // lose
                }

            }
            if (this.board[16, i] == 2)
            {
                if (this.my_color == 1)
                {
                    return 2; // lose
                }
                else
                {
                    return 1; // win
                }

            }
        }
        return 0;
    }

    public void calculate_possible_moves(int currentPlayerColor)
    {
        possible_moves.Clear();
        
        // 현재 턴인 플레이어의 위치 결정
        int current_y, current_x, other_y, other_x;
        
        // 현재 턴인 플레이어 색깔에 따라 위치 설정
        if (currentPlayerColor == my_color)
        {
            // 내 턴이면 my_pos 사용
            current_y = my_pos[0];
            current_x = my_pos[1];
            other_y = opponent_pos[0];
            other_x = opponent_pos[1];
        }
        else
        {
            // 상대 턴이면 opponent_pos 사용
            current_y = opponent_pos[0];
            current_x = opponent_pos[1];
            other_y = my_pos[0];
            other_x = my_pos[1];
        }
        
        // 4방향 확인 (상, 하, 좌, 우)
        int[] dy = { -2, 2, 0, 0 };
        int[] dx = { 0, 0, -2, 2 };
        
        for (int i = 0; i < 4; i++)
        {
            int new_y = current_y + dy[i];
            int new_x = current_x + dx[i];
            
            // 보드 경계 확인
            if (new_y < 0 || new_y > 16 || new_x < 0 || new_x > 16) continue;
            
            // 벽 확인 (중간 위치에 벽이 있는지)
            int wall_y = current_y + dy[i] / 2;
            int wall_x = current_x + dx[i] / 2;
            if (board[wall_y, wall_x] == 3) continue; // 벽이 있으면 이동 불가
            
            // 상대방 말이 있는지 확인
            if (new_y == other_y && new_x == other_x)
            {
                // 상대방 말이 있으면 점프 시도
                int jump_y = new_y + dy[i];
                int jump_x = new_x + dx[i];
                
                // 점프 위치가 보드 내부이고 벽이 없으면 직진 점프
                if (jump_y >= 0 && jump_y <= 16 && jump_x >= 0 && jump_x <= 16)
                {
                    int jump_wall_y = new_y + dy[i] / 2;
                    int jump_wall_x = new_x + dx[i] / 2;
                    if (board[jump_wall_y, jump_wall_x] != 3)
                    {
                        possible_moves.Add(new List<int> { jump_y, jump_x });
                        continue;
                    }
                }
                
                // 직진 점프가 불가능하면 사이드 점프 시도
                for (int j = 0; j < 4; j++)
                {
                    if (j == i || j == (i ^ 1)) continue; // 원래 방향과 반대 방향 제외
                    
                    int side_y = new_y + dy[j];
                    int side_x = new_x + dx[j];
                    
                    // 사이드 점프 위치 확인
                    if (side_y >= 0 && side_y <= 16 && side_x >= 0 && side_x <= 16)
                    {
                        int side_wall_y = new_y + dy[j] / 2;
                        int side_wall_x = new_x + dx[j] / 2;
                        if (board[side_wall_y, side_wall_x] != 3)
                        {
                            possible_moves.Add(new List<int> { side_y, side_x });
                        }
                    }
                }
            }
            else
            {
                // 일반 이동
                possible_moves.Add(new List<int> { new_y, new_x });
            }
        }
    }

    public void make_my_move(int y, int x)
    {
        // 보드에서 현재 내 말의 위치 찾기
        for (int i = 0; i < 17; i += 2)
        {
            for (int j = 0; j < 17; j += 2)
            {
                if (board[i, j] == my_color)
                {
                    // 이전 위치 제거
                    board[i, j] = 0;
                    break;
                }
            }
        }
        
        // 새 위치에 내 말 배치
        this.board[y, x] = this.my_color;
        
        // my_pos 업데이트
        this.my_pos[0] = y;
        this.my_pos[1] = x;
    }

    public bool is_wall_valid(int y1, int x1, int y2, int x2)
    {
        
        // 1. 벽이 보드 범위 내에 있는지 확인
        if (y1 < 0 || y1 > 16 || x1 < 0 || x1 > 16 || y2 < 0 || y2 > 16 || x2 < 0 || x2 > 16)
        {
            return false;
        }
        
        // 2. 해당 위치에 이미 벽이 있는지 확인
        if (this.board[y1, x1] == 3 || this.board[y2, x2] == 3)
        {
            return false;
        }
        
        // 3. 벽 방향 및 교차/인접 확인
        // 벽의 중심점 계산
        int center_y = (y1 + y2) / 2;
        int center_x = (x1 + x2) / 2;

        // 중심점에 이미 벽이 있는지 확인 (십자 교차 방지)
        if (this.board[center_y, center_x] == 3)
        {
            return false;
        }

        // 세로 벽인 경우 (x 좌표가 같음)
        if (x1 == x2)
        {
            // 같은 방향(세로) 벽이 위아래로 인접했는지 확인
            // 위쪽 인접 확인: center_y-2 위치에 세로벽이 있는지
            if (center_y >= 2 && this.board[center_y - 1, center_x] == 3)
            {
                return false;
            }
            // 아래쪽 인접 확인: center_y+2 위치에 세로벽이 있는지
            if (center_y <= 14 && this.board[center_y + 1, center_x] == 3)
            {
                return false;
            }
        }
        // 가로 벽인 경우 (y 좌표가 같음)
        else if (y1 == y2)
        {
            // 같은 방향(가로) 벽이 좌우로 인접했는지 확인
            // 왼쪽 인접 확인: center_x-2 위치에 가로벽이 있는지
            if (center_x >= 2 && this.board[center_y, center_x - 1] == 3)
            {
                return false;
            }
            // 오른쪽 인접 확인: center_x+2 위치에 가로벽이 있는지
            if (center_x <= 14 && this.board[center_y, center_x + 1] == 3)
            {
                return false;
            }
        }
        else
        {
            return false; // 대각선 벽은 불가능
        }
        
        // 4. 임시로 벽을 놓고 BFS 테스트
        this.board[y1, x1] = 3;
        this.board[y2, x2] = 3;
        
        // 목표 설정
        int my_goal = (my_color == 1) ? 0 : 16;
        int opponent_goal = (my_color == 1) ? 16 : 0;
        
        // 내가 목표에 도달할 수 있는지 BFS 확인
        bool can_i_reach = CanReachGoal(my_pos[0], my_pos[1], my_goal);
        
        // 상대방이 목표에 도달할 수 있는지 BFS 확인
        bool can_opponent_reach = CanReachGoal(opponent_pos[0], opponent_pos[1], opponent_goal);
        
        // 임시 벽 제거
        this.board[y1, x1] = 0;
        this.board[y2, x2] = 0;
        
        // 둘 다 목표에 도달할 수 있어야 유효한 벽
        bool result = can_i_reach && can_opponent_reach;
        if (!result)
        {
        }
        else
        {
        }
        
        return result;
    }
    
    private bool CanReachGoal(int start_y, int start_x, int goal_y)
    {
        Queue<List<int>> queue = new Queue<List<int>>();
        bool[,] visited = new bool[17, 17];
        
        queue.Enqueue(new List<int> { start_y, start_x });
        visited[start_y, start_x] = true;
        
        while (queue.Count > 0)
        {
            List<int> pos = queue.Dequeue();
            int y = pos[0];
            int x = pos[1];
            
            // 목표 라인에 도달했는지 확인
            if (y == goal_y)
            {
                return true;
            }
            
            // 4방향 탐색
            int[] dy = { -2, 2, 0, 0 };
            int[] dx = { 0, 0, -2, 2 };
            
            for (int i = 0; i < 4; i++)
            {
                int new_y = y + dy[i];
                int new_x = x + dx[i];
                
                // 보드 경계 확인
                if (new_y < 0 || new_y > 16 || new_x < 0 || new_x > 16) continue;
                
                // 이미 방문했는지 확인
                if (visited[new_y, new_x]) continue;
                
                // 벽이 있는지 확인 (중간 위치)
                int wall_y = y + dy[i] / 2;
                int wall_x = x + dx[i] / 2;
                if (this.board[wall_y, wall_x] == 3) continue;
                
                queue.Enqueue(new List<int> { new_y, new_x });
                visited[new_y, new_x] = true;
            }
        }
        
        return false;
    }

    public void put_wall(int y1, int x1, int y2, int x2)
    {
        // 벽의 양 끝점을 3으로 표시
        this.board[y1, x1] = 3;
        this.board[y2, x2] = 3;
        
        // 벽의 중간 위치도 3으로 표시
        int centerY = (y1 + y2) / 2;
        int centerX = (x1 + x2) / 2;
        this.board[centerY, centerX] = 3;
        
        this.wall_num--;

    }

}

// GameManager 클래스 외부에서 프로필 이미지 로딩 기능 추가
public partial class GameManager
{
    void LoadProfileImages()
    {
        // 내 프로필 이미지 로드
        if (!string.IsNullOrEmpty(myUsername))
        {
            StartCoroutine(LoadMyProfileImage());
        }

        // 상대방 프로필 이미지 로드
        Debug.Log($"OpponentName value: '{opponentName}' (IsNull: {opponentName == null}, IsEmpty: {string.IsNullOrEmpty(opponentName)})");
        if (!string.IsNullOrEmpty(opponentName))
        {
            StartCoroutine(LoadOpponentProfileImage());
        }
        else
        {
            Debug.LogWarning("OpponentName is null or empty, cannot load opponent profile image");
        }
    }

    IEnumerator LoadMyProfileImage()
    {
        // 먼저 내 사용자 ID를 가져와야 함
        string url = $"{ServerConfig.GetHttpUrl()}/me";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                MeResponse meResponse = null;
                try
                {
                    meResponse = JsonUtility.FromJson<MeResponse>(request.downloadHandler.text);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing my user info: {e.Message}");
                    yield break;
                }

                if (meResponse != null)
                {
                    yield return StartCoroutine(LoadProfileImageById(meResponse.id, myProfileImage));
                }
            }
        }
    }

    IEnumerator LoadOpponentProfileImage()
    {
        Debug.Log($"Loading opponent profile image for: '{opponentName}'");
        Debug.Log($"ServerConfig.GetHttpUrl(): {ServerConfig.GetHttpUrl()}");

        // 먼저 상대방의 사용자 ID를 가져옴
        string encodedOpponentName = UnityEngine.Networking.UnityWebRequest.EscapeURL(opponentName);
        string url = $"{ServerConfig.GetHttpUrl()}/user/{encodedOpponentName}";
        Debug.Log($"Original opponent name: '{opponentName}'");
        Debug.Log($"Encoded opponent name: '{encodedOpponentName}'");
        Debug.Log($"Requesting URL: {url}");

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
                    Debug.LogError($"Error parsing opponent user info: {e.Message}");
                    yield break;
                }

                if (userResponse != null)
                {
                    yield return StartCoroutine(LoadProfileImageById(userResponse.id, opponentProfileImage));
                }
            }
            else
            {
                Debug.LogWarning($"Failed to get opponent user info. Status: {request.responseCode}, Error: {request.error}");
                Debug.LogWarning($"Response text: {request.downloadHandler?.text}");
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
                Texture2D texture = null;
                try
                {
                    texture = DownloadHandlerTexture.GetContent(request);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error getting texture from profile image: {e.Message}");
                    yield break;
                }

                if (texture != null && targetImage != null)
                {
                    try
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        targetImage.sprite = sprite;
                        Debug.Log($"Loaded profile image for user ID: {userId}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error creating sprite from texture: {e.Message}");
                    }
                }
            }
            else
            {
                Debug.Log($"No profile image found for user ID: {userId}");
            }
        }
    }

    void PlayMoveSound()
    {
        if (audioSource != null && moveSound != null)
        {
            audioSource.PlayOneShot(moveSound);
        }
    }

    [System.Serializable]
    public class MeResponse
    {
        public int id;
        public string username;
        public string email;
        public string created_at;
    }
}