using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CreateAccount : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] string nextSceneName = "VerificationScene";   // 성공 시 이동할 씬 (인증 씬)
    [SerializeField] string loginSceneName = "LoginScene";         // 뒤로가기 시 이동할 씬

    [Header("UI (TMP)")]
    [SerializeField] TMP_InputField usernameInput;
    [SerializeField] TMP_InputField passwordInput;
    [SerializeField] TMP_InputField emailInput;
    [SerializeField] TextMeshProUGUI resultText;        // (선택)
    [SerializeField] GameObject popup;                  // 팝업 루트
    [SerializeField] TextMeshProUGUI popupMessageText;  // 팝업 메시지
    [SerializeField] Button popupOKButton;              // 팝업 OK 버튼

    [Header("Network")]
    [SerializeField] int requestTimeoutSeconds = 30;    // ⏱ 타임아웃(초) - 30초로 증가

    bool lastRequestSucceeded = false;

    // 이메일 주소를 저장 (VerificationScene에서 사용)
    public static string PendingEmail { get; private set; }

    void Start()
    {
        // 비밀번호 필드를 Password 타입으로 설정
        if (passwordInput != null)
        {
            passwordInput.inputType = TMP_InputField.InputType.Password;
        }

        // 팝업 초기 상태 설정 (OK 버튼 숨김)
        if (popup) popup.SetActive(false);
        if (popupOKButton) popupOKButton.gameObject.SetActive(false);
    }

    public void OnClickCreateAccount()
    {
        StartCoroutine(SignupCoroutine());
    }

    public void OnClickBack()
    {
        if (!string.IsNullOrEmpty(loginSceneName))
            SceneManager.LoadScene(loginSceneName);
    }

    public void OnClickPopupOK()
    {
        if (lastRequestSucceeded)
        {
            if (!string.IsNullOrEmpty(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            if (popup) popup.SetActive(false);
        }
    }

    IEnumerator SignupCoroutine()
    {
        // 즉시 "Please wait..." 팝업 표시 및 OK 버튼 숨김
        if (popup) popup.SetActive(true);
        if (popupMessageText) popupMessageText.text = "Please wait...";
        if (popupOKButton) popupOKButton.gameObject.SetActive(false);

        var email = emailInput.text.Trim();

        var payload = JsonUtility.ToJson(new SignupBody(
            usernameInput.text.Trim(),
            passwordInput.text,
            email
        ));

        // 이메일 인증 요청으로 변경
        var url = $"{ServerConfig.GetHttpUrl()}/request-verification";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        // ✅ 타임아웃 증가
        req.timeout = Mathf.Max(10, requestTimeoutSeconds);

        float startTime = Time.time;
        yield return req.SendWebRequest();
        float duration = Time.time - startTime;
        

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
            // 성공 시 이메일 저장
            PendingEmail = emailInput.text.Trim();
            msg = "Verification code sent to your email!";
            if (resultText) resultText.text = msg;
        }
        else
        {
            msg = BuildReadableError((int)req.responseCode, req.downloadHandler.text, req.error);
            if (resultText) resultText.text = $"Verification Request Error ({req.responseCode})";
        }

        // 팝업 메시지 업데이트 및 OK 버튼 표시
        if (popupMessageText) popupMessageText.text = msg;
        if (popupOKButton) popupOKButton.gameObject.SetActive(true);
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

        // 409: 중복
        if (code == 409)
        {
            var s = TryParseDetailString(body);
            if (!string.IsNullOrEmpty(s)) return s;              // "Email already exists" 등
            if (body.Contains("Email")) return "Email already exists";
            if (body.Contains("Username")) return "Username already exists";
            return "Conflict: already exists.";
        }

        // 422: 유효성 실패
        if (code == 422)
        {
            var firstMsg = TryParseFirstDetailMsg(body);
            if (!string.IsNullOrEmpty(firstMsg)) return firstMsg;
            return "Invalid input.";
        }

        // 기타
        if (!string.IsNullOrEmpty(netErr)) return $"Network error: {netErr}";
        if (!string.IsNullOrEmpty(body)) return $"Error ({code}): {body}";
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
    class SignupBody
    {
        public string username, password, email;
        public SignupBody(string u, string p, string e)
        { username = u; password = p; email = e; }
    }
}
