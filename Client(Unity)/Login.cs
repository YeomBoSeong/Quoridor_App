using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using Debug = UnityEngine.Debug;

public class Login : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] string nextSceneName = "Lobby";
    [SerializeField] string signupSceneName = "CreateAccountScene";
    [SerializeField] string forgotSceneName = "ForgotScene";
    [SerializeField] string changePasswordSceneName = "ChangePasswordScene";

    [Header("UI (TMP)")]
    [SerializeField] TMP_InputField usernameInput;
    [SerializeField] TMP_InputField passwordInput;
    [SerializeField] GameObject popup;
    [SerializeField] TextMeshProUGUI popupMessageText;
    [SerializeField] GameObject popupOkButton;

    bool isBusy = false;

    void Start()
    {
        // 비밀번호 필드를 Password 타입으로 설정
        if (passwordInput != null)
        {
            passwordInput.inputType = TMP_InputField.InputType.Password;
        }
    }

    public void OnClickLogin()
    {
        if (!isBusy) StartCoroutine(LoginCoroutine());
    }

    public void OnClickGoSignup()
    {
        if (!string.IsNullOrEmpty(signupSceneName))
            SceneManager.LoadScene(signupSceneName);
    }

    public void OnClickForgot()
    {
        if (!string.IsNullOrEmpty(forgotSceneName))
            SceneManager.LoadScene(forgotSceneName);
    }

    public void OnClickChangePassword()
    {
        if (!string.IsNullOrEmpty(changePasswordSceneName))
            SceneManager.LoadScene(changePasswordSceneName);
    }

    public void OnClickPopupOK()
    {
        if (popup) popup.SetActive(false);
    }

    IEnumerator LoginCoroutine()
    {
        isBusy = true;

        // Show loading popup without OK button
        ShowPopup("Please Wait...", showOkButton: false);

        var u = usernameInput.text.Trim();
        var p = passwordInput.text;

        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
        {
            ShowPopup("Please enter username and password.");
            isBusy = false;
            yield break;
        }

        string url = $"{ServerConfig.GetHttpUrl()}/login";
        WWWForm form = new WWWForm();
        form.AddField("username", u);
        form.AddField("password", p);

        using (UnityWebRequest req = UnityWebRequest.Post(url, form))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool ok = req.result == UnityWebRequest.Result.Success &&
                      req.responseCode >= 200 && req.responseCode < 300;
#else
            bool ok = !(req.isNetworkError || req.isHttpError);
#endif
            if (!ok)
            {
                string reason = BuildReadableError((int)req.responseCode, req.downloadHandler.text, req.error);
                ShowPopup($"Login failed ({req.responseCode}).\n{reason}");
                isBusy = false;
                yield break;
            }

            // 성공: 토큰 저장 (SessionData 사용)
            var json = req.downloadHandler.text;
            var token = JsonUtility.FromJson<TokenResponse>(json);
            if (token != null && !string.IsNullOrEmpty(token.access_token))
            {
                // SessionData에 로그인 정보 저장 (앱 종료시 사라짐)
                SessionData.SetLoginData(u, token.access_token);

                // FightSocketManager 생성 및 연결
                CreateAndConnectFightSocket(u);
            }

            // Close popup on success
            if (popup) popup.SetActive(false);

            if (!string.IsNullOrEmpty(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        }

        isBusy = false;
    }

    void CreateAndConnectFightSocket(string username)
    {
        // FightSocketManager가 없다면 생성
        if (FightSocketManager.Instance == null)
        {
            GameObject fightSocketGO = new GameObject("FightSocketManager");
            fightSocketGO.AddComponent<FightSocketManager>();
        }

        // WebSocket 연결 및 "start 유저네임 dummy_text" 메시지 전송
        if (FightSocketManager.Instance != null)
        {
            FightSocketManager.Instance.ConnectAndStartFightSocket(username);
        }

        // HeartbeatManager가 없으면 생성
        if (HeartbeatManager.Instance == null)
        {
            GameObject heartbeatGO = new GameObject("HeartbeatManager");
            heartbeatGO.AddComponent<HeartbeatManager>();
        }

        // Heartbeat WebSocket 연결
        if (HeartbeatManager.Instance != null)
        {
            HeartbeatManager.Instance.ConnectHeartbeat(username);
        }
    }

    void ShowPopup(string message, bool showOkButton = true)
    {
        if (popup)
        {
            popup.SetActive(true);
            if (popupMessageText) popupMessageText.text = message;
            if (popupOkButton) popupOkButton.SetActive(showOkButton);
        }
    }

    string BuildReadableError(int code, string body, string netErr)
    {
        if (code == 401) return "Invalid username or password.";
        if (code == 422)
        {
            var msg = TryParseFirstDetailMsg(body);
            return string.IsNullOrEmpty(msg) ? "Invalid input." : msg;
        }
        if (!string.IsNullOrEmpty(netErr)) return $"Network error: {netErr}";
        if (!string.IsNullOrEmpty(body))
        {
            var s = TryParseDetailString(body);
            if (!string.IsNullOrEmpty(s)) return s;
            return body;
        }
        return "Unknown error.";
    }

    string TryParseDetailString(string json)
    {
        const string k = "\"detail\":\"";
        int i = json.IndexOf(k);
        if (i < 0) return null;
        int s = i + k.Length;
        int e = json.IndexOf('"', s);
        if (e < 0) return null;
        return json.Substring(s, e - s);
    }

    string TryParseFirstDetailMsg(string json)
    {
        const string k = "\"msg\":\"";
        int i = json.IndexOf(k);
        if (i < 0) return null;
        int s = i + k.Length;
        int e = json.IndexOf('"', s);
        if (e < 0) return null;
        return json.Substring(s, e - s);
    }

    [System.Serializable]
    class TokenResponse { public string access_token; public string token_type; }
}