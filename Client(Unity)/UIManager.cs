using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("Message Panel")]
    [SerializeField] GameObject messagePanel;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] Button okButton;

    private static UIManager instance;
    private Coroutine hideCoroutine;

    void Awake()
    {
        // 싱글톤 패턴 (선택사항)
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 시작할 때 메시지 패널 숨김
        if (messagePanel != null)
        {
            messagePanel.SetActive(false);
        }

        // OK 버튼 클릭 이벤트 설정
        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(OnOkButtonClicked);
        }
    }

    public void ShowMessage(string message, float duration = 3f)
    {
        if (messagePanel != null && messageText != null)
        {
            messageText.text = message;
            messagePanel.SetActive(true);

            // 이전 타이머가 있다면 취소
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }

            // 지정된 시간 후 패널 숨김
            hideCoroutine = StartCoroutine(HideMessageAfterDelay(duration));
        }
        else
        {
            Debug.LogWarning("UIManager: Message panel or text not assigned!");
            Debug.Log($"Message: {message}");
        }
    }

    IEnumerator HideMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideMessage();
    }

    public void HideMessage()
    {
        if (messagePanel != null)
        {
            messagePanel.SetActive(false);
        }

        // 실행 중인 타이머 취소
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    public void OnOkButtonClicked()
    {
        HideMessage();
    }

    // 정적 메서드로 접근 가능하게 (선택사항)
    public static void ShowStaticMessage(string message, float duration = 3f)
    {
        if (instance != null)
        {
            instance.ShowMessage(message, duration);
        }
    }
}