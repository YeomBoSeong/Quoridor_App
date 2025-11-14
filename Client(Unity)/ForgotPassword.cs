using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ForgotPassword : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] string loginSceneName = "LoginScene";   // 성공/취소 시 돌아갈 씬

    [Header("UI (TMP)")]
    [SerializeField] TMP_InputField emailInput;              // 이메일 입력 필드
    [SerializeField] TextMeshProUGUI resultText;             // (선택) 결과 표시
    [SerializeField] GameObject popup;                       // 팝업 루트
    [SerializeField] TextMeshProUGUI popupMessageText;       // 팝업 메시지
    [SerializeField] Button popupOKButton;                   // 팝업 OK 버튼

    [Header("Network")]
    [SerializeField] int requestTimeoutSeconds = 30;         // ⏱ 타임아웃(초)

    bool lastRequestSucceeded = false;

    void Start()
    {
        // 팝업 초기 상태 설정 (OK 버튼 숨김)
        if (popup) popup.SetActive(false);
        if (popupOKButton) popupOKButton.gameObject.SetActive(false);
    }

    public void OnClickSendEmail()
    {
        StartCoroutine(ForgotPasswordCoroutine());
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

    IEnumerator ForgotPasswordCoroutine()
    {
        var email = emailInput.text.Trim();

        // 이메일 입력 검증
        if (string.IsNullOrEmpty(email))
        {
            ShowPopup("Please enter your email address.");
            yield break;
        }

        // 즉시 "Please wait..." 팝업 표시 및 OK 버튼 숨김
        if (popup) popup.SetActive(true);
        if (popupMessageText) popupMessageText.text = "Please wait...";
        if (popupOKButton) popupOKButton.gameObject.SetActive(false);

        var payload = JsonUtility.ToJson(new ForgotPasswordBody(email));
        var url = $"{ServerConfig.GetHttpUrl()}/forgot-password";

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
            msg = "Account information sent to your email!";
            if (resultText) resultText.text = msg;
        }
        else
        {
            msg = BuildReadableError((int)req.responseCode, req.downloadHandler.text, req.error);
            if (resultText) resultText.text = $"Failed ({req.responseCode})";
        }

        // 팝업 메시지 업데이트 및 OK 버튼 표시
        if (popupMessageText) popupMessageText.text = msg;
        if (popupOKButton) popupOKButton.gameObject.SetActive(true);
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

        // 404: 등록된 이메일이 아님
        if (code == 404)
        {
            var s = TryParseDetailString(body);
            if (!string.IsNullOrEmpty(s)) return s;  // "No account found with this email address"
            return "No account found with this email address.\n\nPlease check your email or create a new account.";
        }

        // 500: 이메일 전송 실패
        if (code == 500)
        {
            return "Failed to send recovery email.\n\nPlease try again later.";
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

    // ==== DTO ====
    [System.Serializable]
    class ForgotPasswordBody
    {
        public string email;
        public ForgotPasswordBody(string e)
        { email = e; }
    }
}
