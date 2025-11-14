using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// 배너 광고 관리 매니저
/// - 화면 하단에 배너 광고 표시
/// - 씬별로 독립적으로 동작
/// </summary>
public class BannerAdManager : MonoBehaviour
{
    [Header("AdMob Settings")]
    [SerializeField] private string androidAdUnitId = "ca-app-pub-4921016092440788/5253482248"; // 배너 광고 단위 ID
    [SerializeField] private string iosAdUnitId = "ca-app-pub-3940256099942544/2934735716"; // iOS 테스트 ID

    // 테스트용 배너 광고 단위 ID (개발 중 사용)
    private const string TEST_ANDROID_AD_UNIT = "ca-app-pub-3940256099942544/6300978111";
    private const string TEST_IOS_AD_UNIT = "ca-app-pub-3940256099942544/2934735716";

    [SerializeField] private bool useTestAds = false; // true로 설정하면 테스트 광고 사용

    [Header("Banner Settings")]
    [SerializeField] private AdPosition bannerPosition = AdPosition.Bottom; // 배너 위치 (Bottom, Top 등)

    private BannerView bannerView;
    private string adUnitId;

    void Start()
    {
        // 플랫폼별 광고 단위 ID 설정
#if UNITY_ANDROID
        adUnitId = useTestAds ? TEST_ANDROID_AD_UNIT : androidAdUnitId;
#elif UNITY_IOS
        adUnitId = useTestAds ? TEST_IOS_AD_UNIT : iosAdUnitId;
#else
        adUnitId = TEST_ANDROID_AD_UNIT; // 에디터에서는 테스트 ID 사용
#endif

        Debug.Log($"[BannerAdManager] Initializing banner ad with ID: {adUnitId}, Test Mode: {useTestAds}");

        // 어린이 지향 설정 (COPPA 준수)
        RequestConfiguration requestConfiguration = new RequestConfiguration
        {
            TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
            MaxAdContentRating = MaxAdContentRating.G
        };

        MobileAds.SetRequestConfiguration(requestConfiguration);
        Debug.Log("[BannerAdManager] Child-directed treatment enabled with G rating");

        // 배너 광고 로드
        LoadBannerAd();
    }

    /// <summary>
    /// 배너 광고 로드 및 표시
    /// </summary>
    private void LoadBannerAd()
    {
        // 기존 배너 정리
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }

        // 배너 크기 설정 (320x50 표준 배너)
        AdSize adSize = AdSize.Banner;

        // 배너뷰 생성
        bannerView = new BannerView(adUnitId, adSize, bannerPosition);

        // 배너 이벤트 리스너 등록
        RegisterBannerEvents();

        // 광고 요청 생성
        AdRequest adRequest = new AdRequest();

        // 배너 광고 로드
        Debug.Log("[BannerAdManager] Loading banner ad...");
        bannerView.LoadAd(adRequest);
    }

    /// <summary>
    /// 배너 광고 이벤트 리스너 등록
    /// </summary>
    private void RegisterBannerEvents()
    {
        // 광고 로드 완료
        bannerView.OnBannerAdLoaded += () =>
        {
            Debug.Log("[BannerAdManager] Banner ad loaded successfully");
        };

        // 광고 로드 실패
        bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            Debug.LogError($"[BannerAdManager] Banner ad failed to load: {error.GetMessage()}");
        };

        // 광고 클릭 시
        bannerView.OnAdClicked += () =>
        {
            Debug.Log("[BannerAdManager] Banner ad clicked");
        };

        // 광고로 인한 앱 이탈 시
        bannerView.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[BannerAdManager] Banner ad full screen content opened");
        };

        // 전체 화면 콘텐츠 닫힘
        bannerView.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[BannerAdManager] Banner ad full screen content closed");
        };

        // 광고 수익 발생
        bannerView.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"[BannerAdManager] Banner ad paid: {adValue.Value} {adValue.CurrencyCode}");
        };
    }

    /// <summary>
    /// 배너 광고 숨기기
    /// </summary>
    public void HideBanner()
    {
        if (bannerView != null)
        {
            bannerView.Hide();
            Debug.Log("[BannerAdManager] Banner ad hidden");
        }
    }

    /// <summary>
    /// 배너 광고 표시
    /// </summary>
    public void ShowBanner()
    {
        if (bannerView != null)
        {
            bannerView.Show();
            Debug.Log("[BannerAdManager] Banner ad shown");
        }
    }

    /// <summary>
    /// 씬 종료 시 배너 광고 정리
    /// </summary>
    void OnDestroy()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
            Debug.Log("[BannerAdManager] Banner ad destroyed");
        }
    }
}
