using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 모든 씬에서 Panel 색상을 관리하는 매니저
/// PlayerPrefs에 저장된 Background 색상을 불러와서 Panel에 적용
/// </summary>
public class PanelColorManager : MonoBehaviour
{
    // PlayerPrefs 키
    private const string BACKGROUND_COLOR_KEY = "BackgroundColor";

    // 색상 이름과 RGB 값 매핑
    private static readonly Dictionary<string, Color> colorMap = new Dictionary<string, Color>
    {
        { "Blue", new Color(0f / 255f, 156f / 255f, 247f / 255f) },      // RGB(0, 156, 247)
        { "Salmon", new Color(255f / 255f, 102f / 255f, 116f / 255f) },  // RGB(255, 102, 116)
        { "Purple", new Color(188f / 255f, 102f / 255f, 255f / 255f) },  // RGB(188, 102, 255)
        { "Green", new Color(102f / 255f, 255f / 255f, 163f / 255f) },   // RGB(102, 255, 163)
        { "Black", new Color(0f / 255f, 0f / 255f, 0f / 255f) }          // RGB(0, 0, 0)
    };

    void Start()
    {
        // 씬이 로드될 때 저장된 색상을 불러와서 Panel에 적용
        ApplyBackgroundColorFromPrefs();
    }

    /// <summary>
    /// PlayerPrefs에서 저장된 색상을 불러와서 현재 씬의 Panel에 적용
    /// </summary>
    public void ApplyBackgroundColorFromPrefs()
    {
        if (PlayerPrefs.HasKey(BACKGROUND_COLOR_KEY))
        {
            string colorName = PlayerPrefs.GetString(BACKGROUND_COLOR_KEY);
            ApplyBackgroundColor(colorName);
        }
        else
        {
            // 저장된 색상이 없으면 기본값 (Blue) 적용
            ApplyBackgroundColor("Blue");
        }
    }

    /// <summary>
    /// 특정 색상을 현재 씬의 Panel에 적용 (정적 메서드)
    /// </summary>
    /// <param name="colorName">적용할 색상 이름 (Blue, Salmon, Purple, Green, Black)</param>
    public static void ApplyBackgroundColor(string colorName)
    {
        if (!colorMap.ContainsKey(colorName))
        {
            Debug.LogWarning($"[PanelColorManager] Unknown color name: {colorName}");
            return;
        }

        Color targetColor = colorMap[colorName];

        // "Panel"이라는 이름을 가진 모든 GameObject 찾기
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        int appliedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name == "Panel")
            {
                // Image 컴포넌트가 있으면 색상 변경
                Image imageComponent = obj.GetComponent<Image>();
                if (imageComponent != null)
                {
                    imageComponent.color = targetColor;
                    appliedCount++;
                    Debug.Log($"[PanelColorManager] Applied {colorName} to Panel: {obj.name} (Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name})");
                }
            }
        }

        if (appliedCount == 0)
        {
            Debug.LogWarning($"[PanelColorManager] No Panel objects found in the current scene.");
        }
        else
        {
            Debug.Log($"[PanelColorManager] Total {appliedCount} Panel(s) updated with color: {colorName}");
        }
    }

    /// <summary>
    /// 색상 이름으로 Color 값 가져오기
    /// </summary>
    /// <param name="colorName">색상 이름</param>
    /// <returns>Color 값 (없으면 Blue 반환)</returns>
    public static Color GetColor(string colorName)
    {
        if (colorMap.ContainsKey(colorName))
        {
            return colorMap[colorName];
        }

        Debug.LogWarning($"[PanelColorManager] Color '{colorName}' not found, returning default (Blue)");
        return colorMap["Blue"];
    }

    /// <summary>
    /// 현재 저장된 배경 색상 이름 반환
    /// </summary>
    /// <returns>저장된 색상 이름 (없으면 "Blue")</returns>
    public static string GetCurrentColorName()
    {
        return PlayerPrefs.GetString(BACKGROUND_COLOR_KEY, "Blue");
    }
}
