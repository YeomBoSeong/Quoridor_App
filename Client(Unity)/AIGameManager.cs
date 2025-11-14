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
using System.Diagnostics;

using Debug = UnityEngine.Debug;

/// <summary>
/// AI 대전용 게임 매니저
/// GameManager와 유사하지만 AI 서버와 통신
/// </summary>
public class AIGameManager : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private string myPlayerColor;
    [SerializeField] private string aiDifficulty;
    [SerializeField] private bool isMyTurn;

    [Header("Player Info UI")]
    [SerializeField] private TextMeshProUGUI myNameText;

    [Header("Profile Image UI")]
    [SerializeField] private UnityEngine.UI.Image myProfileImage;

    [Header("Game Result UI")]
    [SerializeField] private GameObject gameResultPanel;
    [SerializeField] private TextMeshProUGUI gameResultText;
    [SerializeField] private Button backToMenuButton;

    [Header("Session Warning UI")]
    [SerializeField] private GameObject sessionWarningPanel;
    [SerializeField] private TextMeshProUGUI sessionWarningText;

    [Header("Game Objects")]
    [SerializeField] private GameObject playerRedObject;
    [SerializeField] private GameObject playerBlueObject;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private Canvas gameCanvas;

    [Header("Board Settings")]
    [SerializeField] private float cellSize = 99.20454f; // 한 칸 크기 (adjusted for screen ratio)
    [SerializeField] private Vector3 boardCenter = Vector3.zero; // 보드 중앙

    [Header("Wall Count UI")]
    [SerializeField] private TextMeshProUGUI myWallCountText;
    [SerializeField] private TextMeshProUGUI opponentWallCountText;

    [Header("UI Buttons")]
    [SerializeField] private Button moveButton;
    [SerializeField] private Button moveCancelButton;
    [SerializeField] private Button wallButton;
    [SerializeField] private Button wallCancelButton;
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button placeButton;
    [SerializeField] private Button forfeitButton;
    [SerializeField] private GameObject wallContainer; // 벽 관련 UI 컨테이너

    [Header("Move Highlighting")]
    [SerializeField] private GameObject moveButtonPrefab; // 이동 가능한 칸에 표시할 버튼 프리팹
    private List<GameObject> possibleMoveButtons = new List<GameObject>();

    [Header("Sound Effects")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip moveSound; // 수를 둘 때 재생할 소리

    [Header("Wall System")]
    private GameObject currentWallPreview; // 현재 벽 미리보기 오브젝트
    private bool isWallMode = false; // 벽 모드 활성화 여부
    private bool isWallHorizontal = true; // 벽 방향 (true: 가로, false: 세로)
    private Vector2 currentWallPosition; // 현재 벽 위치
    private bool isWallPositionValid = false; // 현재 벽 위치 유효성
    private Vector2Int currentBoardCoordinates; // 현재 보드 좌표

    [Header("WebSocket")]
    private ClientWebSocket aiWebSocket;
    private CancellationTokenSource cancellationTokenSource;
    private Queue<System.Action> mainThreadActions = new Queue<System.Action>();
    private bool isReceivingMessages = false;
    private bool isGameEnded = false;

    // 게임 보드 (GameManager와 공유할 Board 클래스)
    private Board gameBoard;

    // 벽 개수
    private int myWallCount = 10;
    private int opponentWallCount = 10;

    void Start()
    {
        InitializeAIGame();
        InitializeButtons();
        ConnectToAIServer();
    }

    void InitializeAIGame()
    {
        // TimeControlManager에서 설정한 값 가져오기
        myPlayerColor = TimeControlManager.playerColor;
        aiDifficulty = TimeControlManager.aiDifficulty;

        Debug.Log($"[AI_GAME] Player Color: {myPlayerColor}, AI Difficulty: {aiDifficulty}");

        // 내 정보 설정
        if (SessionData.IsValidSession())
        {
            myNameText.text = SessionData.username;
        }
        else
        {
            myNameText.text = "Player";
        }

        // 게임 보드 초기화 (1 = Red, 2 = Blue)
        int myColorInt = (myPlayerColor == "Red") ? 1 : 2;
        gameBoard = new Board(myColorInt);

        // 결과 패널 숨김
        if (gameResultPanel != null)
            gameResultPanel.SetActive(false);

        // 백 버튼 이벤트
        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(OnBackToMenu);

        // 벽 개수 UI 업데이트
        UpdateWallCountUI();

        // 플레이어 말 초기 위치 설정
        InitializePlayerPositions();

        // 프로필 이미지 로드
        LoadProfileImages();

        // 턴 설정 (Red가 먼저 시작)
        isMyTurn = (myPlayerColor == "Red");

        // AI가 Red(먼저 시작)인 경우 AI에게 첫 수를 요청
        if (myPlayerColor == "Blue")
        {
            // AI 서버 연결 후 자동으로 AI의 첫 수를 받을 것임
        }
        else
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

        // 벽 모드일 때 입력 처리
        if (isWallMode && isMyTurn && !isGameEnded)
        {
            HandleWallModeInput();
        }
    }

    // ============ WebSocket 연결 ============

    async void ConnectToAIServer()
    {
        try
        {
            aiWebSocket = new ClientWebSocket();
            cancellationTokenSource = new CancellationTokenSource();

            string wsUrl = $"{ServerConfig.GetWebSocketUrl()}/ai-game";
            var uri = new Uri(wsUrl);


            await aiWebSocket.ConnectAsync(uri, cancellationTokenSource.Token);

            // 초기 메시지 전송: "PlayerColor Difficulty"
            string initialMessage = $"{myPlayerColor} {aiDifficulty}";
            await SendMessageAsync(initialMessage);

            // 메시지 수신 시작
            _ = ReceiveMessagesAsync();
        }
        catch (Exception ex)
        {
            ShowGameResult("Connection Failed");
        }
    }

    async Task SendMessageAsync(string message)
    {
        try
        {
            if (aiWebSocket == null || aiWebSocket.State != WebSocketState.Open)
            {
                return;
            }

            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            var buffer = new ArraySegment<byte>(messageBytes);
            await aiWebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationTokenSource.Token);

        }
        catch (Exception ex)
        {
        }
    }

    async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[1024 * 4];
        isReceivingMessages = true;

        try
        {
            while (aiWebSocket != null && aiWebSocket.State == WebSocketState.Open && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = await aiWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);


                    // 메인 스레드에서 처리
                    mainThreadActions.Enqueue(() =>
                    {
                        HandleAIMessage(message);
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

    // ============ AI 메시지 처리 ============

    void HandleAIMessage(string message)
    {
        string[] parts = message.Split(' ');

        if (parts.Length == 0)
            return;

        string command = parts[0];

        switch (command)
        {
            case "Move":
                // AI 이동: "Move Y,X"
                if (parts.Length >= 2)
                {
                    HandleAIMove(parts[1]);
                }
                break;

            case "Wall":
                // AI 벽 배치: "Wall horizontal Y,X" 또는 "Wall vertical Y,X"
                if (parts.Length >= 3)
                {
                    string wallType = parts[1]; // "horizontal" or "vertical"
                    string position = parts[2]; // "Y,X"
                    HandleAIWall(wallType, position);
                }
                break;

            case "GameEnd":
                // 게임 종료: "GameEnd winner"
                if (parts.Length >= 2)
                {
                    string winner = parts[1]; // "red" or "blue"
                    HandleGameEnd(winner);
                }
                break;

            default:
                break;
        }
    }

    void HandleAIMove(string position)
    {
        // 위치 파싱: "Y,X"
        string[] coords = position.Split(',');
        if (coords.Length != 2)
        {
            return;
        }

        int y = int.Parse(coords[0]);
        int x = int.Parse(coords[1]);


        // 보드 업데이트 (상대방 이동 처리)
        if (gameBoard != null)
        {
            // 상대방 이전 위치 제거
            gameBoard.board[gameBoard.opponent_pos[0], gameBoard.opponent_pos[1]] = 0;
            // 새 위치에 상대방 배치
            gameBoard.board[y, x] = gameBoard.opponent_color;
            gameBoard.opponent_pos[0] = y;
            gameBoard.opponent_pos[1] = x;
        }

        // Unity에서 AI 말 이동
        MoveOpponentPiece(y, x);

        // AI의 수를 둘 때 소리 재생
        PlayMoveSound();

        // AI 이동 후 승패 체크
        CheckWinCondition();

        // 턴 전환
        isMyTurn = true;

        // 버튼 상태 업데이트 (활성화)
        UpdateButtonStates();
    }

    void HandleAIWall(string wallType, string position)
    {
        // 위치 파싱: "Y,X" (중심 좌표 - 항상 홀수)
        string[] coords = position.Split(',');
        if (coords.Length != 2)
        {
            return;
        }

        int centerY = int.Parse(coords[0]);
        int centerX = int.Parse(coords[1]);
        bool isHorizontal = (wallType == "horizontal");

        Debug.Log($"[AI_WALL_RECV] AI placed {wallType} wall at center (Y={centerY},X={centerX})");

        // 벽의 중심 좌표를 시작점/끝점 좌표로 변환
        int y1, x1, y2, x2;
        if (isHorizontal)
        {
            // 가로 벽: 중심 (centerY, centerX) → 시작점 (centerY, centerX-1), 끝점 (centerY, centerX+1)
            y1 = y2 = centerY;
            x1 = centerX - 1;
            x2 = centerX + 1;
        }
        else
        {
            // 세로 벽: 중심 (centerY, centerX) → 시작점 (centerY-1, centerX), 끝점 (centerY+1, centerX)
            x1 = x2 = centerX;
            y1 = centerY - 1;
            y2 = centerY + 1;
        }

        Debug.Log($"[AI_WALL_RECV] Converted to endpoints: (Y1={y1},X1={x1}) - (Y2={y2},X2={x2})");

        // 보드 업데이트
        if (gameBoard != null)
        {
            gameBoard.put_wall(y1, x1, y2, x2);
            Debug.Log($"[AI_WALL_BOARD] Wall placed on board array at positions: [{y1},{x1}], [{(y1+y2)/2},{(x1+x2)/2}], [{y2},{x2}]");
        }

        // Unity에서 벽 표시 (벽 개수는 VisualizeOpponentWall에서 감소됨)
        VisualizeOpponentWall(y1, x1, y2, x2);

        // AI의 수를 둘 때 소리 재생
        PlayMoveSound();

        // 턴 전환
        isMyTurn = true;

        // 버튼 상태 업데이트 (활성화)
        UpdateButtonStates();
    }

    void HandleGameEnd(string winner)
    {

        isGameEnded = true;

        string result;
        if ((winner.ToLower() == "red" && myPlayerColor == "Red") ||
            (winner.ToLower() == "blue" && myPlayerColor == "Blue"))
        {
            result = "You Won!";
        }
        else
        {
            result = "You Lost!";
        }

        ShowGameResult(result);
    }

    // ============ 플레이어 액션 ============

    public void SendPlayerMove(int y, int x)
    {
        if (!isMyTurn || isGameEnded)
        {
            return;
        }

        // 보드 업데이트
        if (gameBoard != null)
        {
            gameBoard.make_my_move(y, x);
        }

        // Unity에서 내 말 이동
        MoveMyPiece(y, x);

        // 서버에 전송: "Y,X"
        string message = $"{y},{x}";
        _ = SendMessageAsync(message);


        // 승리 조건 확인
        CheckWinCondition();

        // 턴 전환
        isMyTurn = false;
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

        Debug.Log($"[MY_WALL_PLACE] Placing {(isWallHorizontal ? "horizontal" : "vertical")} wall at (Y1={y1},X1={x1}) - (Y2={y2},X2={x2})");
        Debug.Log($"[MY_WALL_PLACE] Board coordinates: boardY={currentBoardCoordinates.x}, boardX={currentBoardCoordinates.y}");
        gameBoard.put_wall(y1, x1, y2, x2);
        Debug.Log($"[MY_WALL_BOARD] Wall placed on board array at positions: [{y1},{x1}], [{(y1+y2)/2},{(x1+x2)/2}], [{y2},{x2}]");

        // 보드에 실제 벽 시각화 (미리보기가 아닌 실제 벽)
        // 벽 개수는 OnPlaceButtonClicked에서 감소함
        if (wallPrefab != null && gameCanvas != null)
        {
            GameObject actualWall = Instantiate(wallPrefab, gameCanvas.transform);
            actualWall.name = $"MyWall_{isWallHorizontal}_{currentBoardCoordinates.x}_{currentBoardCoordinates.y}";

            RectTransform wallRect = actualWall.GetComponent<RectTransform>();
            if (wallRect != null)
            {
                wallRect.anchoredPosition = currentWallPosition;
                wallRect.rotation = isWallHorizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90);
                wallRect.SetAsLastSibling();
            }

            Image wallImage = actualWall.GetComponent<Image>();
            if (wallImage != null)
            {
                wallImage.color = Color.white; // 원래 색상 (흰색)
            }
        }
    }

    void SendWallToAIServer(int boardY, int boardX, bool horizontal)
    {
        int y1, x1, y2, x2;
        int centerY, centerX;

        if (horizontal)
        {
            // 가로벽: Y는 동일, X는 연속
            y1 = y2 = boardY;
            x1 = boardX;
            x2 = boardX + 2;
            // 벽 중심 좌표 (홀수)
            centerY = boardY;
            centerX = boardX + 1;
        }
        else
        {
            // 세로벽: X는 동일, Y는 연속
            x1 = x2 = boardX;
            y1 = boardY;
            y2 = boardY + 2;
            // 벽 중심 좌표 (홀수)
            centerY = boardY + 1;
            centerX = boardX;
        }

        // 벽 배치 후 승패 판정
        if (gameBoard != null)
        {
            int winStatus = gameBoard.win_or_lose();
            if (winStatus == 1) // 승리
            {
                isGameEnded = true;
                ShowGameResult("Won");
            }
            else if (winStatus == 2) // 패배
            {
                isGameEnded = true;
                ShowGameResult("Lost");
            }
        }

        // AI 서버에 전송: "wall:horizontal:centerY:centerX" (중심 좌표는 항상 홀수)
        string wallType = horizontal ? "horizontal" : "vertical";
        string message = $"wall:{wallType}:{centerY}:{centerX}";
        Debug.Log($"[AI_WALL_SEND] {message} (y1={y1},x1={x1},y2={y2},x2={x2})");
        _ = SendMessageAsync(message);

        // 턴 전환
        isMyTurn = false;
    }

    // ============ UI 업데이트 ============

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
            else if (result == "Connection Lost")
            {
                resultMessage = "Connection Lost";
            }
            else if (result == "Connection Failed")
            {
                resultMessage = "Connection Failed";
            }
            else
            {
                resultMessage = result;
            }

            gameResultText.text = resultMessage;

            // 패널을 UI 계층의 맨 위로 이동
            gameResultPanel.transform.SetAsLastSibling();

            gameResultPanel.SetActive(true);
        }
    }

    void OnBackToMenu()
    {
        // WebSocket 빠르게 정리 (CloseAsync를 기다리지 않음)
        isReceivingMessages = false;

        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
        }

        if (aiWebSocket != null && aiWebSocket.State == WebSocketState.Open)
        {
            // 비동기로 닫되 기다리지 않음 (백그라운드에서 처리)
            _ = aiWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Game ended", CancellationToken.None);
        }

        // StartScene으로 즉시 이동
        SceneManager.LoadScene("StartScene");
    }

    public void OnForfeitButtonClicked()
    {
        // 게임 포기 처리
        ForfeitGame();
    }

    void ForfeitGame()
    {
        // 게임 종료 플래그 설정
        isGameEnded = true;

        // WebSocket 빠르게 정리
        isReceivingMessages = false;

        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
        }

        if (aiWebSocket != null && aiWebSocket.State == WebSocketState.Open)
        {
            // 비동기로 닫되 기다리지 않음 (백그라운드에서 처리)
            _ = aiWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Game ended", CancellationToken.None);
        }

        // StartScene으로 즉시 이동
        SceneManager.LoadScene("StartScene");
    }

    IEnumerator DelayedSceneTransition()
    {
        yield return new WaitForSeconds(2f);

        // WebSocket 연결 정리
        CleanupWebSocket();

        // TimeControlScene으로 이동
        SceneManager.LoadScene("TimeControl");
    }

    void HandleWebSocketDisconnect()
    {
        // 게임이 이미 끝났으면 연결 끊김 무시
        if (isGameEnded)
        {
            return;
        }

        // 게임 종료 처리
        isGameEnded = true;
        ShowGameResult("Connection Lost");
    }

    void CleanupWebSocket()
    {
        // WebSocket 수신 중지
        isReceivingMessages = false;

        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }

        if (aiWebSocket != null)
        {
            if (aiWebSocket.State == WebSocketState.Open)
            {
                _ = aiWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Game ended", CancellationToken.None);
            }
            aiWebSocket.Dispose();
            aiWebSocket = null;
        }
    }

    void OnDestroy()
    {
        CleanupWebSocket();
    }

    // ============ 게임 시각화 함수들 ============

    void InitializePlayerPositions()
    {
        // Red 플레이어 초기 위치 (16, 8)
        if (playerRedObject != null)
        {
            Vector3 redPos = BoardToWorldPosition(16, 8);
            RectTransform redRect = playerRedObject.GetComponent<RectTransform>();
            if (redRect != null)
            {
                redRect.localPosition = redPos;
            }
        }

        // Blue 플레이어 초기 위치 (0, 8)
        if (playerBlueObject != null)
        {
            Vector3 bluePos = BoardToWorldPosition(0, 8);
            RectTransform blueRect = playerBlueObject.GetComponent<RectTransform>();
            if (blueRect != null)
            {
                blueRect.localPosition = bluePos;
            }
        }
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
        // BoardToWorldPosition과 동일하지만 명확성을 위해 분리
        float gridY = boardY / 2.0f;
        float gridX = boardX / 2.0f;

        float worldX = (gridX - 4) * cellSize;
        float worldY = (4 - gridY) * cellSize;

        Vector3 worldPos = boardCenter + new Vector3(worldX, worldY, 0);
        return worldPos;
    }

    void MoveOpponentPiece(int boardY, int boardX)
    {
        Vector3 worldPosition = BoardToWorldPosition(boardY, boardX);

        // 상대방 색깔에 해당하는 플레이어 오브젝트 이동
        GameObject opponentPlayerObject = null;
        if (myPlayerColor.Equals("Red", System.StringComparison.OrdinalIgnoreCase))
        {
            opponentPlayerObject = playerBlueObject; // 내가 Red면 상대는 Blue (AI)
        }
        else if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            opponentPlayerObject = playerRedObject; // 내가 Blue면 상대는 Red (AI)
        }

        if (opponentPlayerObject != null)
        {
            RectTransform rectTransform = opponentPlayerObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localPosition = worldPosition;
            }
        }
    }

    void MoveMyPiece(int boardY, int boardX)
    {
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
            RectTransform rectTransform = myPlayerObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localPosition = worldPosition;
            }
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

        // 17x17 좌표계 사용: AI 서버에서 Red 관점 좌표를 직접 전송
        // BoardToWorldPosition이 자동으로 플레이어 관점에 맞게 변환함

        // 벽 중심 위치 계산 (홀수 좌표)
        float centerY = (y1 + y2) / 2.0f;
        float centerX = (x1 + x2) / 2.0f;

        // BoardToWorldPosition을 사용하여 플레이어 관점에 맞게 변환
        Vector3 wallWorldPos = BoardToWorldPosition((int)centerY, (int)centerX);

        // 벽 오브젝트 생성
        GameObject opponentWall = Instantiate(wallPrefab, gameCanvas.transform);
        opponentWall.name = $"AIWall_{isHorizontal}_{y1}_{x1}";

        // 위치와 회전 설정
        RectTransform wallRect = opponentWall.GetComponent<RectTransform>();
        if (wallRect != null)
        {
            wallRect.anchoredPosition = new Vector2(wallWorldPos.x, wallWorldPos.y);
            wallRect.rotation = isHorizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90);
            wallRect.SetAsLastSibling();
        }

        // 상대방 벽 색상 설정
        Image wallImage = opponentWall.GetComponent<Image>();
        if (wallImage != null)
        {
            wallImage.color = Color.white;
        }

        // 상대방 벽 개수 감소
        opponentWallCount--;
        UpdateWallCountUI();

    }

    void VisualizeMyWall(int y1, int x1, int y2, int x2)
    {
        if (wallPrefab == null || gameCanvas == null)
        {
            return;
        }

        // 벽 방향 확인
        bool isHorizontal = (y1 == y2);

        // Blue 플레이어의 경우 벽 좌표를 180도 회전 변환
        int displayY1, displayX1, displayY2, displayX2;

        if (myPlayerColor.Equals("Blue", System.StringComparison.OrdinalIgnoreCase))
        {
            displayY1 = 16 - y1;
            displayX1 = 16 - x1;
            displayY2 = 16 - y2;
            displayX2 = 16 - x2;
        }
        else
        {
            displayY1 = y1;
            displayX1 = x1;
            displayY2 = y2;
            displayX2 = x2;
        }

        // 변환된 좌표의 벽 중심 위치 계산
        float centerY = (displayY1 + displayY2) / 2.0f;
        float centerX = (displayX1 + displayX2) / 2.0f;

        // 화면 좌표로 변환
        Vector3 wallWorldPos = ServerCoordToWorldPosition((int)centerY, (int)centerX);
        Debug.Log($"WorldPos=({wallWorldPos.x},{wallWorldPos.y})");

        // 벽 오브젝트 생성
        GameObject myWall = Instantiate(wallPrefab, gameCanvas.transform);
        myWall.name = $"MyWall_{isHorizontal}_{displayY1}_{displayX1}";

        RectTransform wallRect = myWall.GetComponent<RectTransform>();
        if (wallRect != null)
        {
            wallRect.anchoredPosition = new Vector2(wallWorldPos.x, wallWorldPos.y);
            wallRect.rotation = isHorizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90);
            wallRect.localScale = Vector3.one;
            wallRect.SetAsLastSibling();
        }

        Image wallImage = myWall.GetComponent<Image>();
        if (wallImage != null)
        {
            wallImage.color = new Color(0.5f, 1f, 0.5f); // 연한 초록색으로 구분
        }

        // 벽 개수는 OnPlaceButtonClicked에서 감소함
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

    void CheckWinCondition()
    {
        // 승패 판정
        if (gameBoard != null)
        {
            int winStatus = gameBoard.win_or_lose();
            if (winStatus == 1) // 승리
            {
                isGameEnded = true;
                ShowGameResult("Won");
                Debug.Log("[AI_GAME] Player Won!");
            }
            else if (winStatus == 2) // 패배
            {
                isGameEnded = true;
                ShowGameResult("Lost");
                Debug.Log("[AI_GAME] Player Lost!");
            }
        }
    }

    // 게임 턴 표시를 위한 public 함수 (UI에서 사용 가능)
    public bool IsMyTurn()
    {
        return isMyTurn && !isGameEnded;
    }

    public int GetMyWallCount()
    {
        return myWallCount;
    }

    public int GetOpponentWallCount()
    {
        return opponentWallCount;
    }

    // ============ 버튼 초기화 및 입력 처리 ============

    void InitializeButtons()
    {
        // Move 버튼 이벤트
        if (moveButton != null)
            moveButton.onClick.AddListener(OnMoveButtonClicked);

        if (moveCancelButton != null)
            moveCancelButton.onClick.AddListener(OnMoveCancelButtonClicked);

        // Wall 버튼 이벤트
        if (wallButton != null)
            wallButton.onClick.AddListener(OnWallButtonClicked);

        if (wallCancelButton != null)
            wallCancelButton.onClick.AddListener(OnWallCancelButtonClicked);

        if (rotateButton != null)
            rotateButton.onClick.AddListener(OnRotateButtonClicked);

        if (placeButton != null)
            placeButton.onClick.AddListener(OnPlaceButtonClicked);

        if (forfeitButton != null)
            forfeitButton.onClick.AddListener(OnForfeitButtonClicked);

        // 초기 버튼 상태 설정
        UpdateButtonStates();
    }

    void UpdateButtonStates()
    {
        // 내 턴이 아니거나 게임이 종료되었으면 모든 버튼 비활성화
        bool buttonsEnabled = isMyTurn && !isGameEnded;

        if (moveButton != null)
            moveButton.interactable = buttonsEnabled;

        if (wallButton != null)
            wallButton.interactable = buttonsEnabled && myWallCount > 0;
    }

    public void OnMoveButtonClicked()
    {
        if (!isMyTurn || isGameEnded)
        {
            return;
        }

        StartCoroutine(ValidateSessionAndProceed(() =>
        {
            ShowMoveCancelButton();
            ShowPossibleMoves();
        }));
    }

    public void OnMoveCancelButtonClicked()
    {
        ClearPossibleMoveButtons();
        ShowMoveButtons();
    }

    public void OnWallButtonClicked()
    {
        if (!isMyTurn || isGameEnded)
        {
            return;
        }

        if (myWallCount <= 0)
        {
            return;
        }

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

        // 회전 후 미리보기 업데이트
        if (currentWallPosition != Vector2.zero)
        {
            Vector2 newPosition = RecalculateWallCoordinatesFromUIPosition(currentWallPosition);
            ShowWallPreview(newPosition);
        }
    }

    public void OnPlaceButtonClicked()
    {
        if (!isWallPositionValid || currentBoardCoordinates == Vector2Int.zero)
        {
            return;
        }

        StartCoroutine(ValidateSessionAndProceed(() =>
        {
            // 1. 로컬 보드 상태 업데이트
            PlaceWallOnBoard();

            // 2. 수를 둘 때 소리 재생
            PlayMoveSound();

            // 3. 벽 개수 감소 및 UI 업데이트
            myWallCount--;
            UpdateWallCountUI();

            // 4. 미리보기 제거
            ClearWallPreview();

            // 5. Move Cancel 상태에서 일반 상태로 복귀
            ShowMoveButtons();

            // 6. 버튼 상태 즉시 업데이트
            UpdateButtonStates();

            // 7. AI 서버에 벽 전송
            SendWallToAIServer(currentBoardCoordinates.x, currentBoardCoordinates.y, isWallHorizontal);
        }));
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

    public void ShowPossibleMoves()
    {
        if (gameBoard == null)
        {
            return;
        }

        // 기존 이동 버튼들 제거
        ClearPossibleMoveButtons();

        // 현재 턴인 플레이어 색깔 결정
        int currentPlayerColor = (myPlayerColor == "Red") ? 1 : 2;

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
            var image = button.AddComponent<Image>();

            // 버튼 스타일 설정
            image.color = new Color(0f, 1f, 0f, 0.7f); // 반투명 초록색

            // RectTransform 설정
            var rectTransform = button.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100f, 100f);
        }

        // 위치 설정
        var rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = new Vector2(position.x, position.y);
            rect.localScale = Vector3.one;
        }

        // 버튼 클릭 이벤트 (캡처를 위해 로컬 변수 사용)
        int capturedY = boardY;
        int capturedX = boardX;
        var buttonComp = button.GetComponent<Button>();
        if (buttonComp != null)
        {
            buttonComp.onClick.AddListener(() => OnMoveButtonClickedAt(capturedY, capturedX));
        }

        return button;
    }

    void OnMoveButtonClickedAt(int boardY, int boardX)
    {
        // 이동 실행
        SendPlayerMove(boardY, boardX);

        // 수를 둘 때 소리 재생
        PlayMoveSound();

        // 이동 버튼들 제거
        ClearPossibleMoveButtons();

        // 일반 상태로 복귀
        ShowMoveButtons();

        // 버튼 상태 업데이트
        UpdateButtonStates();

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

        isWallPositionValid = foundValidWall;
        return foundValidWall ? bestPosition : Vector2.zero;
    }

    Vector2 CheckHorizontalWallDirect(int boardY, int boardX)
    {
        // 가로벽: boardY는 홀수, boardX는 첫 번째 짝수 위치
        int boardX2 = boardX + 2; // 두 번째 짝수 위치

        // 보드 범위 확인
        if (boardY < 1 || boardY > 15 || boardX < 0 || boardX > 16 || boardX2 < 0 || boardX2 > 16)
        {
            Debug.Log($"[WALL_CHECK] Horizontal wall at (Y={boardY},X={boardX}) - OUT OF BOUNDS");
            return Vector2.zero;
        }

        // Board 클래스의 is_wall_valid 함수로 유효성 검사
        if (gameBoard != null && gameBoard.is_wall_valid(boardY, boardX, boardY, boardX2))
        {
            // RectTransform 좌표계로 변환 (Board 좌표를 Unity UI 좌표로)
            float gridSize = cellSize;
            float unityX = ((boardX + 1) * (gridSize / 2.0f)) - 396.8182f; // 벽 중심 X
            float unityY = 396.8182f - (boardY * (gridSize / 2.0f)); // 벽 중심 Y

            return new Vector2(unityX, unityY);
        }
        else
        {
            Debug.Log($"[WALL_CHECK] Horizontal wall at (Y={boardY},X={boardX}) to (Y={boardY},X={boardX2}) - INVALID (is_wall_valid returned false)");
        }

        return Vector2.zero;
    }

    Vector2 CheckVerticalWallDirect(int boardY, int boardX)
    {
        // 세로벽: boardX는 홀수, boardY는 첫 번째 짝수 위치
        int boardY2 = boardY + 2; // 두 번째 짝수 위치

        // 보드 범위 확인
        if (boardX < 1 || boardX > 15 || boardY < 0 || boardY > 16 || boardY2 < 0 || boardY2 > 16)
        {
            Debug.Log($"[WALL_CHECK] Vertical wall at (Y={boardY},X={boardX}) - OUT OF BOUNDS");
            return Vector2.zero;
        }

        // Board 클래스의 is_wall_valid 함수로 유효성 검사
        if (gameBoard != null && gameBoard.is_wall_valid(boardY, boardX, boardY2, boardX))
        {
            // RectTransform 좌표계로 변환 (Board 좌표를 Unity UI 좌표로)
            float gridSize = cellSize;
            float unityX = (boardX * (gridSize / 2.0f)) - 396.8182f; // 벽 중심 X
            float unityY = 396.8182f - ((boardY + 1) * (gridSize / 2.0f)); // 벽 중심 Y

            return new Vector2(unityX, unityY);
        }
        else
        {
            Debug.Log($"[WALL_CHECK] Vertical wall at (Y={boardY},X={boardX}) to (Y={boardY2},X={boardX}) - INVALID (is_wall_valid returned false)");
        }

        return Vector2.zero;
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

        // 새로운 벽 방향에 맞는 유효한 위치 재계산
        Vector2 newPosition = FindNearestValidWallPosition(gridX, gridY);
        return newPosition;
    }

    bool ValidateWallPlacement(Vector2 position)
    {
        // 보드 범위 내 위치인지 확인
        return position != Vector2.zero;
    }

    void ShowWallPreview(Vector2 position)
    {
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

    void ClearWallPreview()
    {
        if (currentWallPreview != null)
        {
            Destroy(currentWallPreview);
            currentWallPreview = null;
        }
    }

    // ============ 프로필 이미지 로딩 ============

    void LoadProfileImages()
    {
        // 내 프로필 이미지 로드
        if (SessionData.IsValidSession())
        {
            StartCoroutine(LoadMyProfileImage());
        }

        // AI는 프로필 이미지가 없으므로 상대방 이미지는 로드하지 않음
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
                    yield break;
                }

                if (meResponse != null)
                {
                    yield return StartCoroutine(LoadProfileImageById(meResponse.id, myProfileImage));
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
                Texture2D texture = null;
                try
                {
                    texture = DownloadHandlerTexture.GetContent(request);
                }
                catch (System.Exception e)
                {
                    yield break;
                }

                if (texture != null && targetImage != null)
                {
                    try
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        targetImage.sprite = sprite;
                    }
                    catch (System.Exception e)
                    {
                    }
                }
            }
        }
    }

    // ============ 세션 검증 ============

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
