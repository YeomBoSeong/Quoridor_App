using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

[System.Serializable]
public class DatabaseInfo
{
    public DatabaseTables database_tables;
    public RecordCounts record_counts;
    public string server_status;
    public string error;
}

[System.Serializable]
public class DatabaseTables
{
    public bool game_histories_exists;
    public bool game_moves_exists;
}

[System.Serializable]
public class RecordCounts
{
    public int total_games;
    public int total_moves;
}

[System.Serializable]
public class RecentGamesDebug
{
    public RecentGameData[] recent_games;
    public int total_found;
    public string error;
    public string message;
}

[System.Serializable]
public class RecentGameData
{
    public int id;
    public string game_token;
    public string player1;
    public string player2;
    public string game_mode;
    public string game_start_time;
    public string game_end_time;
    public int winner_id;
}

public class GameHistoryDebugger : MonoBehaviour
{
    [Header("Debug Controls")]
    [SerializeField] private bool debugOnStart = true;
    [SerializeField] private KeyCode debugKey = KeyCode.F1;

    void Start()
    {
        if (debugOnStart)
        {
            StartCoroutine(RunFullDebugCheck());
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(debugKey))
        {
            StartCoroutine(RunFullDebugCheck());
        }
    }

    public IEnumerator RunFullDebugCheck()
    {
        Debug.Log("====== GAME HISTORY DEBUG CHECK ======");

        // 1. 서버 연결 확인
        yield return StartCoroutine(CheckServerConnection());

        // 2. 데이터베이스 상태 확인
        yield return StartCoroutine(CheckDatabaseInfo());

        // 3. 최근 게임 확인
        yield return StartCoroutine(CheckRecentGames());

        // 4. 실제 API 호출 테스트
        yield return StartCoroutine(TestGameHistoryAPI());

        Debug.Log("====== DEBUG CHECK COMPLETED ======");
    }

    IEnumerator CheckServerConnection()
    {
        Debug.Log("[DEBUG] Checking server connection...");

        string url = $"{ServerConfig.GetHttpUrl()}/status";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[DEBUG] ✓ Server is running at {ServerConfig.GetHttpUrl()}");
            }
            else
            {
                Debug.LogError($"[DEBUG] ✗ Server connection failed: {request.error}");
            }
        }
    }

    IEnumerator CheckDatabaseInfo()
    {
        Debug.Log("[DEBUG] Checking database status...");

        string url = $"{ServerConfig.GetHttpUrl()}/debug/database-info";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"[DEBUG] Database info response: {jsonResponse}");

                    DatabaseInfo dbInfo = JsonUtility.FromJson<DatabaseInfo>(jsonResponse);

                    if (!string.IsNullOrEmpty(dbInfo.error))
                    {
                        Debug.LogError($"[DEBUG] ✗ Database error: {dbInfo.error}");
                    }
                    else
                    {
                        Debug.Log($"[DEBUG] ✓ Server status: {dbInfo.server_status}");
                        Debug.Log($"[DEBUG] ✓ GameHistory table exists: {dbInfo.database_tables.game_histories_exists}");
                        Debug.Log($"[DEBUG] ✓ GameMove table exists: {dbInfo.database_tables.game_moves_exists}");
                        Debug.Log($"[DEBUG] ✓ Total games recorded: {dbInfo.record_counts.total_games}");
                        Debug.Log($"[DEBUG] ✓ Total moves recorded: {dbInfo.record_counts.total_moves}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DEBUG] ✗ Failed to parse database info: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[DEBUG] ✗ Failed to get database info: {request.error}");
                Debug.LogError($"[DEBUG] Response code: {request.responseCode}");
            }
        }
    }

    IEnumerator CheckRecentGames()
    {
        Debug.Log("[DEBUG] Checking recent games...");

        string url = $"{ServerConfig.GetHttpUrl()}/debug/recent-games";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"[DEBUG] Recent games response: {jsonResponse}");

                    RecentGamesDebug recentGames = JsonUtility.FromJson<RecentGamesDebug>(jsonResponse);

                    if (!string.IsNullOrEmpty(recentGames.error))
                    {
                        Debug.LogError($"[DEBUG] ✗ Recent games error: {recentGames.error}");
                    }
                    else
                    {
                        Debug.Log($"[DEBUG] ✓ Found {recentGames.total_found} recent games");

                        if (recentGames.recent_games != null)
                        {
                            foreach (var game in recentGames.recent_games)
                            {
                                Debug.Log($"[DEBUG]   Game {game.id}: {game.player1} vs {game.player2} ({game.game_mode})");
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DEBUG] ✗ Failed to parse recent games: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[DEBUG] ✗ Failed to get recent games: {request.error}");
            }
        }
    }

    IEnumerator TestGameHistoryAPI()
    {
        Debug.Log("[DEBUG] Testing game history API...");

        string url = $"{ServerConfig.GetHttpUrl()}/game-history";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;
            yield return request.SendWebRequest();

            Debug.Log($"[DEBUG] Game history API - Response code: {request.responseCode}");
            Debug.Log($"[DEBUG] Game history API - Result: {request.result}");

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log($"[DEBUG] ✓ Game history API response: {jsonResponse}");

                try
                {
                    GameHistoryResponse response = JsonUtility.FromJson<GameHistoryResponse>(jsonResponse);
                    if (response.game_history != null)
                    {
                        Debug.Log($"[DEBUG] ✓ Successfully parsed {response.game_history.Length} game records");
                    }
                    else
                    {
                        Debug.LogWarning("[DEBUG] ⚠ game_history field is null in response");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DEBUG] ✗ Failed to parse game history response: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[DEBUG] ✗ Game history API failed: {request.error}");
                Debug.LogError($"[DEBUG] Error response: {request.downloadHandler.text}");

                if (request.responseCode == 404)
                {
                    Debug.LogError("[DEBUG] ✗ API endpoint not found - server may not be updated");
                }
                else if (request.responseCode == 401)
                {
                    Debug.LogError("[DEBUG] ✗ Unauthorized - check session token");
                    Debug.Log($"[DEBUG] Current token: {SessionData.token}");
                }
            }
        }
    }
}