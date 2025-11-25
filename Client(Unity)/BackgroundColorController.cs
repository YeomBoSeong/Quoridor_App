using UnityEngine;
using TMPro;

/// <summary>
/// Background 색상을 순환시키는 컨트롤러
/// Blue -> Salmon -> Purple -> Green -> Black -> Blue (순환)
/// </summary>
public class BackgroundColorController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI colorText; // BackgroundColor 텍스트

    // 색상 순환 배열
    private readonly string[] colors = { "Blue", "Salmon", "Purple", "Green", "Black" };
    private int currentIndex = 0; // 현재 색상 인덱스

    // PlayerPrefs 키
    private const string BACKGROUND_COLOR_KEY = "BackgroundColor";

    void Start()
    {
        // 저장된 색상 불러오기
        LoadSavedColor();
        UpdateColorText();
        ApplyColorToAllPanels();
    }

    /// <summary>
    /// 오른쪽 화살표 버튼 클릭 시 호출
    /// Blue -> Salmon -> Purple -> Green -> Black -> Blue 순으로 순환
    /// </summary>
    public void OnRightButtonClick()
    {
        // 인덱스를 1 증가시키고 배열 길이로 나눈 나머지를 사용 (순환)
        currentIndex = (currentIndex + 1) % colors.Length;
        UpdateColorText();
        SaveColor();
        ApplyColorToAllPanels();

    }

    /// <summary>
    /// 왼쪽 화살표 버튼 클릭 시 호출
    /// Black -> Green -> Purple -> Salmon -> Blue 순으로 역순환
    /// </summary>
    public void OnLeftButtonClick()
    {
        // 인덱스를 1 감소시키고, 음수가 되지 않도록 배열 길이를 더한 후 나머지 연산
        currentIndex = (currentIndex - 1 + colors.Length) % colors.Length;
        UpdateColorText();
        SaveColor();
        ApplyColorToAllPanels();

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
                ApplyColorToAllPanels();
                return;
            }
        }

    }

    /// <summary>
    /// PlayerPrefs에서 저장된 색상 불러오기
    /// </summary>
    private void LoadSavedColor()
    {
        if (PlayerPrefs.HasKey(BACKGROUND_COLOR_KEY))
        {
            string savedColor = PlayerPrefs.GetString(BACKGROUND_COLOR_KEY);
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i].Equals(savedColor, System.StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    return;
                }
            }
        }

        // 저장된 색상이 없으면 기본값 (Blue)
        currentIndex = 0;
    }

    /// <summary>
    /// 현재 색상을 PlayerPrefs에 저장
    /// </summary>
    private void SaveColor()
    {
        PlayerPrefs.SetString(BACKGROUND_COLOR_KEY, colors[currentIndex]);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 모든 씬의 Panel에 색상 적용
    /// </summary>
    private void ApplyColorToAllPanels()
    {
        // PanelColorManager를 통해 색상 적용
        PanelColorManager.ApplyBackgroundColor(colors[currentIndex]);
    }
}
