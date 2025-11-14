using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모든 씬에서 Logo 이미지를 관리하는 매니저
/// PlayerPrefs에 저장된 Logo 색상에 따라 logo_black 또는 logo_white 이미지 적용
/// </summary>
public class LogoImageManager : MonoBehaviour
{
    [Header("Logo Image Reference")]
    [SerializeField] private Image logoImage; // Logo 이미지 컴포넌트

    [Header("Logo Sprites")]
    [SerializeField] private Sprite logoBlackSprite; // logo_black 스프라이트
    [SerializeField] private Sprite logoWhiteSprite; // logo_white 스프라이트

    // PlayerPrefs 키
    private const string LOGO_COLOR_KEY = "LogoColor";

    void Start()
    {
        // 씬이 로드될 때 저장된 색상을 불러와서 Logo 이미지 적용
        ApplyLogoImageFromPrefs();
    }

    /// <summary>
    /// PlayerPrefs에서 저장된 Logo 색상을 불러와서 이미지 적용
    /// </summary>
    public void ApplyLogoImageFromPrefs()
    {
        string logoColor = GetCurrentLogoColor();
        ApplyLogoImage(logoColor);
    }

    /// <summary>
    /// 특정 색상에 맞는 Logo 이미지 적용 (정적 메서드)
    /// </summary>
    /// <param name="colorName">색상 이름 (Black 또는 White)</param>
    public static void ApplyLogoImage(string colorName)
    {
        // 현재 씬의 모든 LogoImageManager 찾기
        LogoImageManager[] managers = FindObjectsOfType<LogoImageManager>();

        foreach (LogoImageManager manager in managers)
        {
            manager.SetLogoImage(colorName);
        }

        if (managers.Length == 0)
        {
            Debug.LogWarning("[LogoImageManager] No LogoImageManager found in the current scene.");
        }
        else
        {
            Debug.Log($"[LogoImageManager] Applied logo image for color: {colorName} to {managers.Length} manager(s)");
        }
    }

    /// <summary>
    /// Logo 이미지를 색상에 맞게 설정
    /// </summary>
    /// <param name="colorName">색상 이름 (Black 또는 White)</param>
    private void SetLogoImage(string colorName)
    {
        if (logoImage == null)
        {
            Debug.LogError("[LogoImageManager] Logo Image component is not assigned!");
            return;
        }

        Sprite targetSprite = null;

        if (colorName.Equals("Black", System.StringComparison.OrdinalIgnoreCase))
        {
            targetSprite = logoBlackSprite;
        }
        else if (colorName.Equals("White", System.StringComparison.OrdinalIgnoreCase))
        {
            targetSprite = logoWhiteSprite;
        }
        else
        {
            Debug.LogWarning($"[LogoImageManager] Unknown logo color: {colorName}, using default (Black)");
            targetSprite = logoBlackSprite;
        }

        if (targetSprite != null)
        {
            logoImage.sprite = targetSprite;
            Debug.Log($"[LogoImageManager] Logo image changed to: {targetSprite.name} (Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name})");
        }
        else
        {
            Debug.LogError($"[LogoImageManager] Sprite for color '{colorName}' is not assigned!");
        }
    }

    /// <summary>
    /// 현재 저장된 Logo 색상 이름 반환
    /// </summary>
    /// <returns>저장된 색상 이름 (없으면 "Black")</returns>
    public static string GetCurrentLogoColor()
    {
        return PlayerPrefs.GetString(LOGO_COLOR_KEY, "Black");
    }

    /// <summary>
    /// Inspector에서 스프라이트 자동 로드 (에디터 전용)
    /// </summary>
    void OnValidate()
    {
        // 에디터에서 스프라이트가 할당되지 않았을 때 자동으로 찾아서 할당
        #if UNITY_EDITOR
        if (logoBlackSprite == null)
        {
            logoBlackSprite = Resources.Load<Sprite>("Images/logo_black");
            if (logoBlackSprite == null)
            {
                // Assets/Images 폴더에서 찾기
                string[] guids = UnityEditor.AssetDatabase.FindAssets("logo_black t:Sprite");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    logoBlackSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
        }

        if (logoWhiteSprite == null)
        {
            logoWhiteSprite = Resources.Load<Sprite>("Images/logo_white");
            if (logoWhiteSprite == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("logo_white t:Sprite");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    logoWhiteSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
        }
        #endif
    }
}
