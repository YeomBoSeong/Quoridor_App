using UnityEngine;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System;

public class HeartbeatManager : MonoBehaviour
{
    private static HeartbeatManager instance;
    public static HeartbeatManager Instance => instance;

    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellationTokenSource;
    private bool isConnected = false;
    private bool isReceiving = false;

    void Awake()
    {
        // 싱글톤 패턴
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

    /// <summary>
    /// 로그인 성공 시 호출하여 heartbeat WebSocket 연결
    /// </summary>
    public void ConnectHeartbeat(string username)
    {
        if (isConnected)
        {
            return;
        }

        _ = ConnectAsync(username);
    }

    /// <summary>
    /// 로그아웃 시 또는 앱 종료 시 연결 해제
    /// </summary>
    public void DisconnectHeartbeat()
    {
        _ = DisconnectAsync();
    }

    async Task ConnectAsync(string username)
    {
        try
        {
            webSocket = new ClientWebSocket();
            cancellationTokenSource = new CancellationTokenSource();

            string wsUrl = $"{ServerConfig.GetWebSocketUrl()}/heartbeat";
            Uri uri = new Uri(wsUrl);

            await webSocket.ConnectAsync(uri, cancellationTokenSource.Token);

            if (webSocket.State == WebSocketState.Open)
            {
                isConnected = true;

                // 첫 메시지로 username 전송
                await SendMessage($"connect {username}");

                // 메시지 수신 시작
                StartReceiving();
            }
            else
            {
            }
        }
        catch (Exception ex)
        {
            isConnected = false;
        }
    }

    async Task DisconnectAsync()
    {
        try
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }

            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
            }

            isConnected = false;
            isReceiving = false;
        }
        catch (Exception ex)
        {
        }
    }

    void StartReceiving()
    {
        if (isReceiving) return;
        isReceiving = true;

        _ = ReceiveMessagesAsync();
    }

    async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[1024];

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    // ping 메시지를 받으면 pong 응답
                    if (message.Trim() == "ping")
                    {
                        await SendMessage("pong");
                    }
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
        }
        finally
        {
            isReceiving = false;
            isConnected = false;
        }
    }

    async Task SendMessage(string message)
    {
        try
        {
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationTokenSource.Token);
            }
        }
        catch (Exception ex)
        {
        }
    }

    void OnApplicationQuit()
    {
        DisconnectHeartbeat();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        // 백그라운드로 갈 때는 연결 유지
        // StatusManager에서 status만 변경하고, heartbeat는 계속 유지
        // 이렇게 하면 앱이 백그라운드에 있어도 연결은 유지되다가
        // 실제로 앱이 종료되면 자동으로 끊어짐
    }

    void OnDestroy()
    {
        DisconnectHeartbeat();
    }

    /// <summary>
    /// Heartbeat 연결 상태 확인
    /// </summary>
    public bool IsConnected()
    {
        return isConnected && webSocket != null && webSocket.State == WebSocketState.Open;
    }
}
