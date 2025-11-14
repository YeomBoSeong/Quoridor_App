using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class EmailVerification : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] string nextSceneName = "Lobby";              // 인증 성공 시 이동할 씬
    [SerializeField] string createAccountSceneName = "CreateAccountScene";  // 뒤로가기 시 이동할 씬

    [Header("UI (TMP)")]
    [SerializeField] TMP_InputField verificationCodeInput;  // 6자리 코드 입력 필드
    [SerializeField] TextMeshProUGUI emailDisplayText;      // 이메일 주소 표시
    [SerializeField] TextMeshProUGUI resultText;            // (선택)
    [SerializeField] GameObject popup;                      // 팝업 루트
    [SerializeField] TextMeshProUGUI popupMessageText;      // 팝업 메시지

    [Header("Network")]
    [SerializeField] int requestTimeoutSeconds = 30;        // ⏱ 타임아웃(초)

    bool lastRequestSucceeded = false;
    string pendingEmail;

    void Start()
    {
        // CreateAccount에서 저장한 이메일 주소 가져오기
        pendingEmail = CreateAccount.PendingEmail;

        if (string.IsNullOrEmpty(pendingEmail))
        {
            SceneManager.LoadScene("CreateAccountScene");
            return;
        }

        // 이메일 주소 표시
        if (emailDisplayText != null)
        {
            emailDisplayText.text = $"Verification code sent to:\n{pendingEmail}";
        }
    }

    public void OnClickVerify()
    {
        StartCoroutine(VerifyCodeCoroutine());
    }

    public void OnClickBack()
    {
        if (!string.IsNullOrEmpty(createAccountSceneName))
            SceneManager.LoadScene(createAccountSceneName);
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

    IEnumerator VerifyCodeCoroutine()
    {
        var code = verificationCodeInput.text.Trim();

        // 6자리 검증
        if (code.Length != 6)
        {
            ShowPopup("Please enter a 6-digit verification code.");
            yield break;
        }

        var payload = JsonUtility.ToJson(new VerifyCodeBody(pendingEmail, code));
        var url = $"{ServerConfig.GetHttpUrl()}/verify-code";

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
            msg = "Account created successfully!";
            if (resultText) resultText.text = msg;
        }
        else
        {
            msg = BuildReadableError((int)req.responseCode, req.downloadHandler.text, req.error);
            if (resultText) resultText.text = $"Verification Error ({req.responseCode})";
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

        // 400: 잘못된 코드 또는 만료
        if (code == 400)
        {
            var s = TryParseDetailString(body);
            if (!string.IsNullOrEmpty(s)) return s;  // "Invalid verification code" 또는 "Verification code expired"
            return "Invalid or expired verification code.";
        }

        // 404: 인증 요청을 찾을 수 없음
        if (code == 404)
        {
            return "No verification request found. Please request a new code.";
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

    // ==== DTO ====
    [System.Serializable]
    class VerifyCodeBody
    {
        public string email, code;
        public VerifyCodeBody(string e, string c)
        { email = e; code = c; }
    }
}
