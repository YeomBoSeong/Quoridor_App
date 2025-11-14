using UnityEngine;
using UnityEngine.SceneManagement;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using System;

public class FightSocketManager : MonoBehaviour
{
    private static FightSocketManager instance;
    public static FightSocketManager Instance => instance;

    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellationTokenSource;
    private bool isReceivingMessages = false;
    private Queue<string> messageQueue = new Queue<string>();

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
        }
    }

    void Update()
    {
        // 메인 스레드에서 메시지 처리
        lock (messageQueue)
        {
            while (messageQueue.Count > 0)
            {
                string message = messageQueue.Dequeue();
                ProcessFightMessage(message);
            }
        }
    }

    public void ConnectAndStartFightSocket(string username)
    {
        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            return;
        }

        _ = ConnectWebSocketAsync(username);
    }

    async Task ConnectWebSocketAsync(string username)
    {
        try
        {
            webSocket = new ClientWebSocket();
            cancellationTokenSource = new CancellationTokenSource();

            string wsUrl = $"{ServerConfig.GetWebSocketUrl()}/fight";
            Uri uri = new Uri(wsUrl);

            await webSocket.ConnectAsync(uri, cancellationTokenSource.Token);

            if (webSocket.State == WebSocketState.Open)
            {
                // 로그인 메시지 전송: "start 유저네임 dummy_text"
                await SendMessage($"start {username} dummy_text");

                StartReceivingMessages();
            }
            else
            {
                Debug.LogError($"Fight WebSocket connection failed. State: {webSocket.State}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Fight WebSocket connection failed: {ex.Message}");
        }
    }

    void StartReceivingMessages()
    {
        if (isReceivingMessages) return;
        isReceivingMessages = true;

        _ = ReceiveMessagesAsync();
    }

    async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[1024 * 4];

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    // fight 메시지인 경우에만 로그 출력
                    if (message.StartsWith("fight "))
                    {
                        Debug.Log($"[FIGHT] Received fight request: {message}");
                    }

                    // 메인 스레드에서 처리하기 위해 큐에 저장
                    lock (messageQueue)
                    {
                        messageQueue.Enqueue(message);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Fight WebSocket receive error: {ex.Message}");
        }
        finally
        {
            isReceivingMessages = false;
        }
    }

    void ProcessFightMessage(string message)
    {
        string[] parts = message.Split(' ');
        if (parts.Length < 2) return;

        string command = parts[0];

        switch (command)
        {
            case "fight":
                // "fight 상대방유저네임 게임모드" - 대전 요청 받음
                if (parts.Length >= 3)
                {
                    string fromUser = parts[1];
                    string gameMode = parts[2];
                    ShowFightRequest(fromUser, gameMode);
                }
                break;

            case "Red":
            case "Blue":
                // 게임 생성 메시지: "Red/Blue 상대이름 게임토큰 상대ELO 본인ELO 게임모드"
                if (parts.Length >= 6)
                {
                    HandleGameCreated(parts);
                }
                else if (parts.Length >= 5)
                {
                    // 이전 형식 지원 (게임 모드 없음)
                    HandleGameCreated(parts);
                }
                break;

            default:
                // 상태 응답 메시지 처리: "유저네임 상태 게임모드"
                if (parts.Length >= 3)
                {
                    string username = parts[0];
                    string status = parts[1];
                    string gameMode = parts[2];
                    HandleStatusResponse(username, status);
                }
                else if (parts.Length >= 2)
                {
                    // 이전 형식 지원: "유저네임 상태"
                    string username = parts[0];
                    string status = parts[1];
                    HandleStatusResponse(username, status);
                }
                break;
        }
    }

    void HandleGameCreated(string[] parts)
    {
        // 메시지 형식: "Red/Blue 상대이름 게임토큰 상대ELO 본인ELO 게임모드"
        string playerColor = parts[0];
        string opponentName = parts[1];
        string gameToken = parts[2];
        string opponentELO = parts[3];
        string myCurrentELO = parts[4];
        string gameMode = parts.Length >= 6 ? parts[5] : "Rapid"; // 기본값은 Rapid

        Debug.Log($"[FIGHT] Game created - Color: {playerColor}, Opponent: {opponentName}, Token: {gameToken}, Mode: {gameMode}");

        // TimeControlManager의 정적 변수에 저장 (GameManager에서 읽을 수 있도록)
        TimeControlManager.playerColor = playerColor;
        TimeControlManager.opponentName = opponentName;
        TimeControlManager.gameToken = gameToken;
        TimeControlManager.opponentELO = opponentELO;
        TimeControlManager.myCurrentELO = myCurrentELO;
        TimeControlManager.currentTimeControl = gameMode;

        // 상태를 in_game으로 변경
        StatusManager.SetUserInGame();

        // PlayScene으로 이동
        SceneManager.LoadScene("PlayScene");
    }

    void ShowFightRequest(string fromUser, string gameMode)
    {
        // 글로벌 fight 요청 패널 표시
        ShowGlobalFightRequestPanel(fromUser, gameMode);
    }

    void ShowGlobalFightRequestPanel(string fromUser, string gameMode)
    {
        // FightRequestManager를 찾아서 패널 표시
        FightRequestManager fightRequestManager = FindObjectOfType<FightRequestManager>();
        if (fightRequestManager != null)
        {
            fightRequestManager.ShowFightRequest(fromUser, gameMode);
        }
        else
        {
            // FightRequestManager가 없으면 생성
            GameObject fightRequestGO = new GameObject("FightRequestManager");
            FightRequestManager newManager = fightRequestGO.AddComponent<FightRequestManager>();
            newManager.ShowFightRequest(fromUser, gameMode);
        }
    }

    void HandleStatusResponse(string username, string status)
    {
        // 해당 친구의 FriendItemUI를 찾아서 상태 메시지 표시
        FriendItemUI friendItemUI = FriendItemUI.GetFriendItemByUsername(username);
        if (friendItemUI != null)
        {
            friendItemUI.ShowStatusMessage(status);
        }
        else
        {
            // 백업: UIManager를 통해 메시지 표시
            string message = "";
            switch (status)
            {
                case "offline":
                    message = "친구가 오프라인입니다.";
                    break;
                case "in_game":
                    message = "친구가 게임 중입니다.";
                    break;
                case "online":
                    message = "대전 요청을 보냈습니다!";
                    break;
                default:
                    message = $"친구 상태: {status}";
                    break;
            }

            UIManager uiManager = FindObjectOfType<UIManager>();
            if (uiManager != null)
            {
                uiManager.ShowMessage(message, 3f);
            }
        }
    }

    public void SendFightRequest(string targetUsername)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            Debug.LogError("Fight WebSocket is not connected!");
            return;
        }

        string myUsername = SessionData.username;
        _ = SendMessage($"{myUsername} {targetUsername}");
    }

    public void SendBattleRequest(string targetUsername, string gameMode)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            Debug.LogError("Fight WebSocket is not connected!");
            return;
        }

        string myUsername = SessionData.username;
        string messageToSend = $"{gameMode} {myUsername} {targetUsername}";
        _ = SendMessage(messageToSend);
    }

    public void SendAcceptMessage(string opponent, string gameMode)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            Debug.LogError("Fight WebSocket is not connected!");
            return;
        }

        string messageToSend = $"accept {opponent} {gameMode}";
        _ = SendMessage(messageToSend);
    }

    public void SendDeclineMessage(string opponent, string gameMode)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            Debug.LogError("Fight WebSocket is not connected!");
            return;
        }

        string messageToSend = $"decline {opponent} {gameMode}";
        _ = SendMessage(messageToSend);
    }

    async Task SendMessage(string message)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send fight message: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            _ = webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
        }
    }

    void OnApplicationQuit()
    {
        OnDestroy();
    }
}