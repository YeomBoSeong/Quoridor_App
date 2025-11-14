using UnityEngine;
using TMPro;

public class ServerConfig : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverIP = "your-server-domain.com";
    [SerializeField] private int serverPort = 443;
    
    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI serverIPText;
    
    [Header("Debug Info (ReadOnly)")]
    [SerializeField] private string currentHttpUrl;
    [SerializeField] private string currentWebSocketUrl;
    
    // PlayerPrefs keys
    private const string SERVER_IP_KEY = "server_ip";
    private const string SERVER_PORT_KEY = "server_port";
    
    void Start()
    {
        // 디버깅 정보 출력
        
        // PlayerPrefs 로드 전 상태 확인
        
        // 강제로 서버 주소 설정 (테스트용)
        PlayerPrefs.SetString(SERVER_IP_KEY, "your-server-domain.com");
        PlayerPrefs.SetInt(SERVER_PORT_KEY, 443);
        PlayerPrefs.Save();
        
        LoadServerConfig();
        UpdateDisplay();
        UpdateDebugInfo();
        
        // 최종 결과 확인
    }
    
    void OnValidate()
    {
        // Inspector에서 값 변경시 자동 저장 및 업데이트
        if (Application.isPlaying)
        {
            SaveServerConfig();
            UpdateDisplay();
        }
        UpdateDebugInfo();
    }
    
    void LoadServerConfig()
    {
        // PlayerPrefs에서 저장된 값 로드
        serverIP = PlayerPrefs.GetString(SERVER_IP_KEY, serverIP);
        serverPort = PlayerPrefs.GetInt(SERVER_PORT_KEY, serverPort);
    }
    
    void SaveServerConfig()
    {
        // PlayerPrefs에 저장
        PlayerPrefs.SetString(SERVER_IP_KEY, serverIP);
        PlayerPrefs.SetInt(SERVER_PORT_KEY, serverPort);
        PlayerPrefs.Save();
    }
    
    void UpdateDisplay()
    {
        // UI 텍스트 업데이트
        if (serverIPText != null)
        {
            serverIPText.text = $"{serverIP}:{serverPort}";
        }
    }
    
    void UpdateDebugInfo()
    {
        // Debug 정보 업데이트
        currentHttpUrl = $"https://{serverIP}:{serverPort}";
        currentWebSocketUrl = $"wss://{serverIP}:{serverPort}";
    }
    
    // Static 메서드들 - 다른 스크립트에서 사용
    /// <summary>
    /// HTTP URL 가져오기 (예: https://your-server-domain.com:443)
    /// </summary>
    public static string GetHttpUrl()
    {
        string ip = PlayerPrefs.GetString(SERVER_IP_KEY, "your-server-domain.com");
        int port = PlayerPrefs.GetInt(SERVER_PORT_KEY, 443);
        return $"https://{ip}:{port}";
    }
    
    /// <summary>
    /// WebSocket URL 가져오기 (예: wss://your-server-domain.com:443)
    /// </summary>
    public static string GetWebSocketUrl()
    {
        string ip = PlayerPrefs.GetString(SERVER_IP_KEY, "your-server-domain.com");
        int port = PlayerPrefs.GetInt(SERVER_PORT_KEY, 443);
        return $"wss://{ip}:{port}";
    }
    
    /// <summary>
    /// 서버 IP 주소 가져오기
    /// </summary>
    public static string GetServerIP()
    {
        return PlayerPrefs.GetString(SERVER_IP_KEY, "your-server-domain.com");
    }

    /// <summary>
    /// 서버 포트 가져오기
    /// </summary>
    public static int GetServerPort()
    {
        return PlayerPrefs.GetInt(SERVER_PORT_KEY, 443);
    }
}