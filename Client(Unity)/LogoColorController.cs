using UnityEngine;
using TMPro;

/// <summary>
/// Logo 색상을 순환시키는 컨트롤러
/// Black <-> White 순환
/// </summary>
public class LogoColorController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI colorText; // LogoColor 텍스트

    // 색상 순환 배열 (Black, White)
    private readonly string[] colors = { "Black", "White" };
    private int currentIndex = 0; // 현재 색상 인덱스

    // PlayerPrefs 키
    private const string LOGO_COLOR_KEY = "LogoColor";

    void Start()
    {
        // 저장된 색상 불러오기
        LoadSavedColor();
        UpdateColorText();
        ApplyLogoImage();
    }

    /// <summary>
    /// 오른쪽 화살표 버튼 클릭 시 호출
    /// Black -> White -> Black 순으로 순환
    /// </summary>
    public void OnRightButtonClick()
    {
        // 인덱스를 1 증가시키고 배열 길이로 나눈 나머지를 사용 (순환)
        currentIndex = (currentIndex + 1) % colors.Length;
        UpdateColorText();
        SaveColor();
        ApplyLogoImage();

    }

    /// <summary>
    /// 왼쪽 화살표 버튼 클릭 시 호출
    /// White -> Black -> White 순으로 역순환
    /// </summary>
    public void OnLeftButtonClick()
    {
        // 인덱스를 1 감소시키고, 음수가 되지 않도록 배열 길이를 더한 후 나머지 연산
        currentIndex = (currentIndex - 1 + colors.Length) % colors.Length;
        UpdateColorText();
        SaveColor();
        ApplyLogoImage();

    }

    /// <summary>
    /// UI 텍스트를 현재 색상으로 업데이트
    /// </summary>
    private void UpdateColorText()
    {
        if (colorText != null)
        {
            colorText.text = colors[currentIndex];
        }
        else
        {
        }
    }

    /// <summary>
    /// 현재 선택된 색상 이름을 반환
    /// </summary>
    public string GetCurrentColor()
    {
        return colors[currentIndex];
    }

    /// <summary>
    /// 특정 색상으로 설정 (외부에서 호출 가능)
    /// </summary>
    public void SetColor(string colorName)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            if (colors[i].Equals(colorName, System.StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                UpdateColorText();
                SaveColor();
                ApplyLogoImage();
                return;
            }
        }

    }

    /// <summary>
    /// 현재 색상에 맞는 로고 이미지 적용
    /// </summary>
    private void ApplyLogoImage()
    {
        // LogoImageManager를 통해 로고 이미지 적용
        LogoImageManager.ApplyLogoImage(colors[currentIndex]);
    }

    /// <summary>
    /// PlayerPrefs에서 저장된 색상 불러오기
    /// </summary>
    private void LoadSavedColor()
    {
        if (PlayerPrefs.HasKey(LOGO_COLOR_KEY))
        {
            string savedColor = PlayerPrefs.GetString(LOGO_COLOR_KEY);
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i].Equals(savedColor, System.StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    return;
                }
            }
        }

        // 저장된 색상이 없으면 기본값 (Black)
        currentIndex = 0;
    }

    /// <summary>
    /// 현재 색상을 PlayerPrefs에 저장
    /// </summary>
    private void SaveColor()
    {
        PlayerPrefs.SetString(LOGO_COLOR_KEY, colors[currentIndex]);
        PlayerPrefs.Save();
    }
}
