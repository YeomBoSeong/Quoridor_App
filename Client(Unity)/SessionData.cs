using UnityEngine;

public static class SessionData
{
    public static string username;
    public static string token;
    public static string elo;
    public static bool isLoggedIn = false;
    
    public static void SetLoginData(string userUsername, string userToken, string userElo = "1500")
    {
        username = userUsername;
        token = userToken;
        elo = userElo;
        isLoggedIn = true;

        // 로그인 시 이전 계정의 IAP 및 게임 크레딧 데이터 초기화 (안전장치)
        ClearIAPData();

    }
    
    public static void ClearSession()
    {

        username = null;
        token = null;
        elo = null;
        isLoggedIn = false;

        // IAP 및 게임 크레딧 관련 PlayerPrefs 클리어 (계정 분리)
        ClearIAPData();

    }
    
    public static bool IsValidSession()
    {
        return isLoggedIn && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(token);
    }
    
    public static string GetToken()
    {
        return token;
    }

    /// <summary>
    /// IAP 및 게임 크레딧 관련 PlayerPrefs 클리어
    /// 계정 전환 시 이전 계정의 데이터가 남지 않도록 함
    /// </summary>
    private static void ClearIAPData()
    {

        // IAPManager 관련
        PlayerPrefs.DeleteKey("IsProSubscribed");

        // GameCreditManager 관련
        PlayerPrefs.DeleteKey("AvailableGames_Cache");
        PlayerPrefs.DeleteKey("IsPro_Cache");
        PlayerPrefs.DeleteKey("LastResetDate_Cache");

        PlayerPrefs.Save();

    }
}