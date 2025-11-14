using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

/// <summary>
/// 게임 횟수(크레딧) 관리 매니저 - 서버 연동 버전
/// - 서버에서 게임 횟수 관리
/// - 로컬 캐시로 오프라인 대응
/// </summary>
public class GameCreditManager : MonoBehaviour
{
    public static GameCreditManager Instance { get; private set; }

    // 현재 게임 횟수 (캐시)
    private int availableGames = 5;
    private DateTime lastResetDate;

    // 이벤트
    public event Action<int> OnGamesChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLocalCache();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 서버에서 최신 데이터 가져오기
        RefreshFromServer();
    }

    /// <summary>
    /// 로컬 캐시 로드 (오프라인 대응용)
    /// </summary>
    void LoadLocalCache()
    {
        availableGames = PlayerPrefs.GetInt("AvailableGames_Cache", 5);

        string lastResetStr = PlayerPrefs.GetString("LastResetDate_Cache", "");
        if (string.IsNullOrEmpty(lastResetStr))
        {
            lastResetDate = DateTime.Today;
        }
        else
        {
            lastResetDate = DateTime.Parse(lastResetStr);
        }

        Debug.Log($"[GameCreditManager] Local cache loaded: {availableGames} games");
    }

    /// <summary>
    /// 로컬 캐시 저장
    /// </summary>
    void SaveLocalCache()
    {
        PlayerPrefs.SetInt("AvailableGames_Cache", availableGames);
        PlayerPrefs.SetString("LastResetDate_Cache", lastResetDate.ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 서버에서 게임 횟수 새로고침
    /// </summary>
    public void RefreshFromServer()
    {
        StartCoroutine(RefreshFromServerCoroutine());
    }

    IEnumerator RefreshFromServerCoroutine()
    {
        if (!SessionData.IsValidSession())
        {
            Debug.LogWarning("[GameCreditManager] No valid session. Using local cache.");
            yield break;
        }

        string url = $"{ServerConfig.GetHttpUrl()}/api/game-credits";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                Debug.Log($"[GameCreditManager] Server response JSON: {json}");

                GameCreditResponse response = JsonUtility.FromJson<GameCreditResponse>(json);

                availableGames = response.available_games;
                lastResetDate = DateTime.Parse(response.last_reset_date);

                SaveLocalCache();
                OnGamesChanged?.Invoke(availableGames);

                Debug.Log($"[GameCreditManager] ✅ Server data loaded - Available Games: {availableGames}, can_play: {response.can_play}");
            }
            else
            {
                Debug.LogWarning($"[GameCreditManager] Failed to refresh from server: {request.error}. Using cache.");
            }
        }
    }

    /// <summary>
    /// 현재 사용 가능한 게임 횟수 반환
    /// </summary>
    public int GetAvailableGames()
    {
        return availableGames;
    }

    /// <summary>
    /// 게임 시작 가능 여부 확인 (로컬 캐시 기반 - UI 표시용)
    /// </summary>
    public bool CanPlayGame()
    {
        return availableGames > 0;
    }

    /// <summary>
    /// 서버에서 실시간으로 게임 시작 가능 여부 확인
    /// </summary>
    public void CheckCanPlayGameFromServer(Action<bool, string> callback)
    {
        StartCoroutine(CheckCanPlayGameFromServerCoroutine(callback));
    }

    IEnumerator CheckCanPlayGameFromServerCoroutine(Action<bool, string> callback)
    {
        if (!SessionData.IsValidSession())
        {
            Debug.LogWarning("[GameCreditManager] No valid session");
            callback?.Invoke(false, "No valid session");
            yield break;
        }

        string url = $"{ServerConfig.GetHttpUrl()}/api/game-credits";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                Debug.Log($"[GameCreditManager] CheckCanPlayGame Server response JSON: {json}");

                GameCreditResponse response = JsonUtility.FromJson<GameCreditResponse>(json);

                // 로컬 캐시 업데이트 (UI 표시용)
                availableGames = response.available_games;
                SaveLocalCache();
                OnGamesChanged?.Invoke(availableGames);

                bool canPlay = response.can_play;
                string message = canPlay ? "Can play" : "No games available";

                Debug.Log($"[GameCreditManager] ✅ Server check - Available Games: {availableGames}, can_play: {canPlay}");
                callback?.Invoke(canPlay, message);
            }
            else
            {
                Debug.LogError($"[GameCreditManager] Failed to check from server: {request.error}");
                callback?.Invoke(false, $"Server error: {request.error}");
            }
        }
    }

    /// <summary>
    /// 게임 1회 소비 (서버에 요청)
    /// </summary>
    public void ConsumeGame(Action<bool> callback = null)
    {
        StartCoroutine(ConsumeGameCoroutine(callback));
    }

    IEnumerator ConsumeGameCoroutine(Action<bool> callback)
    {
        if (!SessionData.IsValidSession())
        {
            Debug.LogWarning("[GameCreditManager] No valid session");
            callback?.Invoke(false);
            yield break;
        }

        string url = $"{ServerConfig.GetHttpUrl()}/api/game-credits/consume";

        using (UnityWebRequest request = UnityWebRequest.Post(url, ""))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                ConsumeGameResponse response = JsonUtility.FromJson<ConsumeGameResponse>(json);

                if (response.success)
                {
                    availableGames = response.remaining_games;
                    SaveLocalCache();
                    OnGamesChanged?.Invoke(availableGames);
                    Debug.Log($"[GameCreditManager] Game consumed. Remaining: {availableGames}");
                    callback?.Invoke(true);
                }
                else
                {
                    Debug.LogWarning($"[GameCreditManager] {response.message}");
                    callback?.Invoke(false);
                }
            }
            else
            {
                Debug.LogError($"[GameCreditManager] Failed to consume game: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// 광고 시청으로 게임 횟수 추가 (서버에 요청)
    /// </summary>
    public void AddGameFromAd()
    {
        StartCoroutine(AddGameFromAdCoroutine());
    }

    IEnumerator AddGameFromAdCoroutine()
    {
        if (!SessionData.IsValidSession())
        {
            Debug.LogWarning("[GameCreditManager] No valid session");
            yield break;
        }

        string url = $"{ServerConfig.GetHttpUrl()}/api/game-credits/add-from-ad";

        using (UnityWebRequest request = UnityWebRequest.Post(url, ""))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                ConsumeGameResponse response = JsonUtility.FromJson<ConsumeGameResponse>(json);

                if (response.success)
                {
                    availableGames = response.remaining_games;
                    SaveLocalCache();
                    OnGamesChanged?.Invoke(availableGames);
                    Debug.Log($"[GameCreditManager] {response.message}");
                }
            }
            else
            {
                Debug.LogError($"[GameCreditManager] Failed to add game from ad: {request.error}");
            }
        }
    }

    /// <summary>
    /// 최대 일일 게임 횟수 반환
    /// </summary>
    public int GetMaxDailyGames()
    {
        return 5;
    }
}

// ================== JSON 응답 모델 ==================

[Serializable]
public class GameCreditResponse
{
    public int available_games;
    public bool can_play;
    public string last_reset_date;
}

[Serializable]
public class ConsumeGameResponse
{
    public bool success;
    public int remaining_games;
    public string message;
}
