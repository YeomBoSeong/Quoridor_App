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

        // 1. 서버 연결 확인
        yield return StartCoroutine(CheckServerConnection());

        // 2. 데이터베이스 상태 확인
        yield return StartCoroutine(CheckDatabaseInfo());

        // 3. 최근 게임 확인
        yield return StartCoroutine(CheckRecentGames());

        // 4. 실제 API 호출 테스트
        yield return StartCoroutine(TestGameHistoryAPI());

    }

    IEnumerator CheckServerConnection()
    {

        string url = $"{ServerConfig.GetHttpUrl()}/status";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
            }
            else
            {
            }
        }
    }

    IEnumerator CheckDatabaseInfo()
    {

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

                    DatabaseInfo dbInfo = JsonUtility.FromJson<DatabaseInfo>(jsonResponse);

                    if (!string.IsNullOrEmpty(dbInfo.error))
                    {
                    }
                    else
                    {
                    }
                }
                catch (System.Exception e)
                {
                }
            }
            else
            {
            }
        }
    }

    IEnumerator CheckRecentGames()
    {

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

                    RecentGamesDebug recentGames = JsonUtility.FromJson<RecentGamesDebug>(jsonResponse);

                    if (!string.IsNullOrEmpty(recentGames.error))
                    {
                    }
                    else
                    {

                        if (recentGames.recent_games != null)
                        {
                            foreach (var game in recentGames.recent_games)
                            {
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                }
            }
            else
            {
            }
        }
    }

    IEnumerator TestGameHistoryAPI()
    {

        string url = $"{ServerConfig.GetHttpUrl()}/game-history";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;
            yield return request.SendWebRequest();


            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;

                try
                {
                    GameHistoryResponse response = JsonUtility.FromJson<GameHistoryResponse>(jsonResponse);
                    if (response.game_history != null)
                    {
                    }
                    else
                    {
                    }
                }
                catch (System.Exception e)
                {
                }
            }
            else
            {

                if (request.responseCode == 404)
                {
                }
                else if (request.responseCode == 401)
                {
                }
            }
        }
    }
}