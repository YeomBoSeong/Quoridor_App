using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class StatusManager : MonoBehaviour
{
    private static StatusManager instance;

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

    void Start()
    {
        // 앱 시작 시 온라인 상태로 설정 (로그인되어 있다면)
        if (SessionData.IsValidSession())
        {
            StartCoroutine(UpdateUserStatus("online"));
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (SessionData.IsValidSession())
        {
            if (pauseStatus)
            {
                // 앱이 백그라운드로 갈 때 오프라인 상태로 설정
                StartCoroutine(UpdateUserStatus("offline"));
            }
            else
            {
                // 앱이 포그라운드로 돌아올 때 온라인 상태로 설정
                StartCoroutine(UpdateUserStatus("online"));
            }
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // 에디터에서는 포커스 변경을 무시 (빌드된 앱에서만 동작)
        #if !UNITY_EDITOR
        if (SessionData.IsValidSession())
        {
            if (hasFocus)
            {
                // 앱이 포커스를 얻을 때 온라인 상태로 설정
                StartCoroutine(UpdateUserStatus("online"));
            }
            else
            {
                // 앱이 포커스를 잃을 때 오프라인 상태로 설정
                StartCoroutine(UpdateUserStatus("offline"));
            }
        }
        #endif
    }

    void OnApplicationQuit()
    {
        // 앱 종료 시 오프라인 상태로 설정
        if (SessionData.IsValidSession())
        {
            // 동기적으로 빠르게 처리
            StartCoroutine(UpdateUserStatusSync("offline"));
        }
    }

    public static void SetUserOnline()
    {
        if (instance != null && SessionData.IsValidSession())
        {
            instance.StartCoroutine(instance.UpdateUserStatus("online"));
        }
    }

    public static void SetUserOffline()
    {
        if (instance != null && SessionData.IsValidSession())
        {
            instance.StartCoroutine(instance.UpdateUserStatus("offline"));
        }
    }

    public static void SetUserInGame()
    {
        if (instance != null && SessionData.IsValidSession())
        {
            instance.StartCoroutine(instance.UpdateUserStatus("in_game"));
        }
    }

    public static void SetUserMatchmaking()
    {
        if (instance != null && SessionData.IsValidSession())
        {
            instance.StartCoroutine(instance.UpdateUserStatus("matchmaking"));
        }
    }

    IEnumerator UpdateUserStatus(string status)
    {
        if (!SessionData.IsValidSession())
            yield break;

        string url = $"{ServerConfig.GetHttpUrl()}/status";

        // POST 요청을 위한 JSON 데이터 생성
        string jsonData = $"{{\"username\":\"{SessionData.username}\", \"status\":\"{status}\"}}";
        byte[] bodyData = System.Text.Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Status updated to: {status}");
            }
            else
            {
                Debug.LogWarning($"Failed to update status to {status}: {request.error}");
            }
        }
    }

    IEnumerator UpdateUserStatusSync(string status)
    {
        if (!SessionData.IsValidSession())
            yield break;

        string url = $"{ServerConfig.GetHttpUrl()}/status";

        // POST 요청을 위한 JSON 데이터 생성
        string jsonData = $"{{\"username\":\"{SessionData.username}\", \"status\":\"{status}\"}}";
        byte[] bodyData = System.Text.Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 2; // 빠른 처리

            yield return request.SendWebRequest();

            // 결과에 상관없이 빠르게 처리
            Debug.Log($"App quit - status set to {status}");
        }
    }
}