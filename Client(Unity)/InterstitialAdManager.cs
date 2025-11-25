using UnityEngine;
using GoogleMobileAds.Api;
using System;

/// <summary>
/// 전면 광고 관리 매니저
/// - 전면 광고 로드 및 표시
/// - 싱글톤 패턴으로 모든 씬에서 접근 가능
/// </summary>
public class InterstitialAdManager : MonoBehaviour
{
    public static InterstitialAdManager Instance { get; private set; }

    [Header("AdMob Settings")]
    [SerializeField] private string androidAdUnitId = "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX"; // 전면 광고 단위 ID로 변경하세요
    [SerializeField] private string iosAdUnitId = "ca-app-pub-3940256099942544/4411468910"; // iOS 테스트 ID

    // 테스트용 전면 광고 단위 ID (개발 중 사용)
    private const string TEST_ANDROID_AD_UNIT = "ca-app-pub-3940256099942544/1033173712";
    private const string TEST_IOS_AD_UNIT = "ca-app-pub-3940256099942544/4411468910";

    [SerializeField] private bool useTestAds = false; // true로 설정하면 테스트 광고 사용

    private InterstitialAd interstitialAd;
    private string adUnitId;

    // 광고 로드 상태
    private bool isAdLoaded = false;
    private bool isInitialized = false;

    // 이벤트
    public event Action OnAdClosed;
    public event Action OnAdFailed;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAd();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 전면 광고 초기화
    /// </summary>
    void InitializeAd()
    {
        // 플랫폼별 광고 단위 ID 설정
#if UNITY_ANDROID
        adUnitId = useTestAds ? TEST_ANDROID_AD_UNIT : androidAdUnitId;
#elif UNITY_IOS
        adUnitId = useTestAds ? TEST_IOS_AD_UNIT : iosAdUnitId;
#else
        adUnitId = TEST_ANDROID_AD_UNIT; // 에디터에서는 테스트 ID 사용
#endif


        // 어린이 지향 설정 (COPPA 준수)
        RequestConfiguration requestConfiguration = new RequestConfiguration
        {
            TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
            MaxAdContentRating = MaxAdContentRating.G
        };

        MobileAds.SetRequestConfiguration(requestConfiguration);

        isInitialized = true;

        // 광고 미리 로드
        LoadInterstitialAd();
    }

    /// <summary>
    /// 전면 광고 로드
    /// </summary>
    public void LoadInterstitialAd()
    {
        if (!isInitialized)
        {
            return;
        }

        // 기존 광고 정리
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }


        // 광고 요청 생성
        AdRequest adRequest = new AdRequest();

        // 전면 광고 로드
        InterstitialAd.Load(adUnitId, adRequest, (InterstitialAd ad, LoadAdError loadError) =>
        {
            if (loadError != null)
            {
                isAdLoaded = false;
                return;
            }
            else if (ad == null)
            {
                isAdLoaded = false;
                return;
            }

            interstitialAd = ad;
            isAdLoaded = true;

            // 광고 이벤트 리스너 등록
            RegisterAdEvents(ad);
        });
    }

    /// <summary>
    /// 광고 이벤트 리스너 등록
    /// </summary>
    private void RegisterAdEvents(InterstitialAd ad)
    {
        // 광고가 열릴 때
        ad.OnAdFullScreenContentOpened += () =>
        {
        };

        // 광고가 닫힐 때
        ad.OnAdFullScreenContentClosed += () =>
        {

            // 광고가 닫히면 다음 광고 로드
            isAdLoaded = false;
            LoadInterstitialAd();

            // 닫힘 이벤트 발생
            OnAdClosed?.Invoke();
        };

        // 광고 표시 실패 시
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            isAdLoaded = false;
            OnAdFailed?.Invoke();

            // 실패 후 재시도
            LoadInterstitialAd();
        };

        // 광고 수익 발생
        ad.OnAdPaid += (AdValue adValue) =>
        {
        };
    }

    /// <summary>
    /// 전면 광고 표시
    /// </summary>
    public void ShowInterstitialAd()
    {
        if (!isInitialized)
        {
            OnAdFailed?.Invoke();
            return;
        }

        if (interstitialAd != null && isAdLoaded)
        {
            interstitialAd.Show();
        }
        else
        {
            LoadInterstitialAd();
            OnAdFailed?.Invoke();
        }
    }

    /// <summary>
    /// 광고 사용 가능 여부 확인
    /// </summary>
    public bool IsAdReady()
    {
        return isInitialized && isAdLoaded && interstitialAd != null;
    }

    void OnDestroy()
    {
        // 광고 리소스 정리
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
        }
    }
}
