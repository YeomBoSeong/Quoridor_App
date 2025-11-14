using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class ChangePassword : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] string loginSceneName = "LoginScene";   // 성공/취소 시 돌아갈 씬

    [Header("UI (TMP)")]
    [SerializeField] TMP_InputField usernameInput;           // 유저네임 입력 필드
    [SerializeField] TMP_InputField oldPasswordInput;        // 기존 패스워드 입력 필드
    [SerializeField] TMP_InputField newPasswordInput;        // 새 패스워드 입력 필드
    [SerializeField] TextMeshProUGUI resultText;             // (선택) 결과 표시
    [SerializeField] GameObject popup;                       // 팝업 루트
    [SerializeField] TextMeshProUGUI popupMessageText;       // 팝업 메시지

    [Header("Network")]
    [SerializeField] int requestTimeoutSeconds = 30;         // ⏱ 타임아웃(초)

    bool lastRequestSucceeded = false;

    void Start()
    {
        // 비밀번호 필드를 Password 타입으로 설정
        if (oldPasswordInput != null)
        {
            oldPasswordInput.inputType = TMP_InputField.InputType.Password;
        }
        if (newPasswordInput != null)
        {
            newPasswordInput.inputType = TMP_InputField.InputType.Password;
        }
    }

    public void OnClickChangePassword()
    {
        StartCoroutine(ChangePasswordCoroutine());
    }

    public void OnClickBack()
    {
        // 로그인 씬으로 돌아가기
        if (!string.IsNullOrEmpty(loginSceneName))
            SceneManager.LoadScene(loginSceneName);
    }

    public void OnClickCancel()
    {
        // 로그인 씬으로 돌아가기
        if (!string.IsNullOrEmpty(loginSceneName))
            SceneManager.LoadScene(loginSceneName);
    }

    public void OnClickPopupOK()
    {
        if (lastRequestSucceeded)
        {
            // 성공 시 로그인 씬으로 돌아가기
            if (!string.IsNullOrEmpty(loginSceneName))
                SceneManager.LoadScene(loginSceneName);
        }
        else
        {
            if (popup) popup.SetActive(false);
        }
    }

    IEnumerator ChangePasswordCoroutine()
    {
        var username = usernameInput.text.Trim();
        var oldPassword = oldPasswordInput.text;
        var newPassword = newPasswordInput.text;

        // 입력 검증
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
        {
            ShowPopup("Please fill in all fields.");
            yield break;
        }

        var payload = JsonUtility.ToJson(new ChangePasswordBody(username, oldPassword, newPassword));
        var url = $"{ServerConfig.GetHttpUrl()}/change-password";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        req.timeout = Mathf.Max(10, requestTimeoutSeconds);

        yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        bool ok = req.result == UnityWebRequest.Result.Success &&
                  req.responseCode >= 200 && req.responseCode < 300;
#else
        bool ok = !(req.isNetworkError || req.isHttpError);
#endif
        lastRequestSucceeded = ok;

        string msg;
        if (ok)
        {
            msg = "Changed password successfully!";
            if (resultText) resultText.text = msg;
        }
        else
        {
            msg = BuildReadableError((int)req.responseCode, req.downloadHandler.text, req.error);
            if (resultText) resultText.text = $"Failed ({req.responseCode})";
        }

        ShowPopup(msg);
    }

    void ShowPopup(string message)
    {
        if (popup) popup.SetActive(true);
        if (popupMessageText) popupMessageText.text = message;
    }

    // ==== 에러 메시지 생성 ====
    string BuildReadableError(int code, string body, string netErr)
    {
        // 🔹 타임아웃/연결 실패 (대개 code == 0)
        if (code == 0)
        {
            if (!string.IsNullOrEmpty(netErr) && netErr.ToLower().Contains("timed"))
                return "Request timed out. Please check your network or server.";
            return $"Network error: {netErr}";
        }

        // 404: 유저네임이 존재하지 않음
        if (code == 404)
        {
            var s = TryParseDetailString(body);
            if (!string.IsNullOrEmpty(s)) return s;  // "Username doesn't exist."
            return "Username doesn't exist.";
        }

        // 401: 기존 비밀번호가 틀림
        if (code == 401)
        {
            var s = TryParseDetailString(body);
            if (!string.IsNullOrEmpty(s)) return s;  // "Incorrect password."
            return "Incorrect password.";
        }

        // 422: 유효성 검증 실패
        if (code == 422)
        {
            var firstMsg = TryParseFirstDetailMsg(body);
            if (!string.IsNullOrEmpty(firstMsg)) return firstMsg;
            return "Invalid input.";
        }

        // 500: 서버 오류
        if (code == 500)
        {
            return "Password change failed.\n\nPlease try again later.";
        }

        // 기타
        if (!string.IsNullOrEmpty(netErr)) return $"Network error: {netErr}";
        if (!string.IsNullOrEmpty(body))
        {
            var s = TryParseDetailString(body);
            if (!string.IsNullOrEmpty(s)) return s;
            return $"Error ({code}): {body}";
        }
        return $"Error ({code})";
    }

    // {"detail":"..."} → 문자열 detail 추출
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

    // {"detail":[{"msg":"..."}...]} → 첫 msg 추출
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

    // ==== DTO ====
    [System.Serializable]
    class ChangePasswordBody
    {
        public string username, old_password, new_password;
        public ChangePasswordBody(string u, string op, string np)
        { username = u; old_password = op; new_password = np; }
    }
}
