using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;

[System.Serializable]
public class GameHistoryData
{
    public int id;
    public string game_token;
    public string opponent_username;
    public string game_mode;
    public string result;
    public int my_elo_before;
    public int my_elo_after;
    public int my_elo_change;
    public string game_start_time;
    public string game_end_time;
    public string game_result;
}

[System.Serializable]
public class GameHistoryResponse
{
    public GameHistoryData[] game_history;
}

[System.Serializable]
public class GameMoveData
{
    public int move_number;
    public string player_username;
    public string move_type;
    public string position_from;
    public string position_to;
    public float remaining_time;
    public string move_timestamp;
}

[System.Serializable]
public class GameDetailResponse
{
    public int game_id;
    public string game_token;
    public string player1_username;
    public string player2_username;
    public string game_mode;
    public string game_start_time;
    public string game_end_time;
    public string game_result;
    public GameMoveData[] moves;
}

public class GameHistoryManager : MonoBehaviour
{
    public static GameHistoryManager Instance { get; private set; }

    [Header("Events")]
    public System.Action<GameHistoryData[]> OnGameHistoryLoaded;
    public System.Action<GameDetailResponse> OnGameDetailLoaded;
    public System.Action<string> OnError;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadGameHistory()
    {
        StartCoroutine(LoadGameHistoryCoroutine());
    }

    public void LoadGameDetail(int gameId)
    {
        StartCoroutine(LoadGameDetailCoroutine(gameId));
    }

    IEnumerator LoadGameHistoryCoroutine()
    {
        string url = $"{ServerConfig.GetHttpUrl()}/game-history";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();


            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;

                    GameHistoryResponse response = JsonUtility.FromJson<GameHistoryResponse>(jsonResponse);

                    if (response.game_history != null)
                    {
                        OnGameHistoryLoaded?.Invoke(response.game_history);
                    }
                    else
                    {
                        OnGameHistoryLoaded?.Invoke(new GameHistoryData[0]);
                    }
                }
                catch (System.Exception e)
                {
                    OnError?.Invoke($"Failed to parse game history: {e.Message}");
                }
            }
            else
            {
                long statusCode = request.responseCode;
                string errorResponse = request.downloadHandler.text;

                if (statusCode == 401)
                {
                    SessionData.ClearSession();
                    OnError?.Invoke("Session expired. Please login again.");
                }
                else
                {
                    OnError?.Invoke($"Failed to load game history: HTTP {statusCode} - {request.error}");
                }
            }
        }
    }

    IEnumerator LoadGameDetailCoroutine(int gameId)
    {
        string url = $"{ServerConfig.GetHttpUrl()}/game-history/{gameId}/moves";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;

                    GameDetailResponse response = JsonUtility.FromJson<GameDetailResponse>(jsonResponse);
                    OnGameDetailLoaded?.Invoke(response);
                }
                catch (System.Exception e)
                {
                    OnError?.Invoke($"Failed to parse game detail: {e.Message}");
                }
            }
            else
            {
                long statusCode = request.responseCode;
                if (statusCode == 401)
                {
                    SessionData.ClearSession();
                    OnError?.Invoke("Session expired. Please login again.");
                }
                else
                {
                    OnError?.Invoke($"Failed to load game detail: {request.error}");
                }
            }
        }
    }
}