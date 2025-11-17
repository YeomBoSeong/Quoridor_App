using UnityEngine;
using GoogleMobileAds.Api;
using System;

/// <summary>
/// Google AdMob 광고 관리 매니저
/// - 보상형 비디오 광고 재생
/// - 광고 시청 완료 시 게임 횟수 추가
/// </summary>
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    // AdMob 설정
    [Header("AdMob Settings")]
    [SerializeField] private string androidAdUnitId = "ca-app-pub-XXXXXXXXXXXXXXXX/YYYYYYYYYY"; // TODO: Replace with your actual Ad Unit ID
    [SerializeField] private string iosAdUnitId = "ca-app-pub-XXXXXXXXXXXXXXXX/YYYYYYYYYY"; // TODO: Replace with your actual Ad Unit ID

    // 테스트용 광고 단위 ID (개발 중 사용)
    private const string TEST_ANDROID_AD_UNIT = "ca-app-pub-3940256099942544/5224354917";
    private const string TEST_IOS_AD_UNIT = "ca-app-pub-3940256099942544/1712485313";

    [SerializeField] private bool useTestAds = false; // true로 설정하면 테스트 광고 사용

    private string adUnitId;
    private RewardedAd rewardedAd;

    // 광고 로드 상태
    private bool isAdLoaded = false;
    private bool isInitialized = false;

    // 메인 스레드 콜백 플래그
    private bool pendingRewardSuccess = false;
    private bool pendingAdFailed = false;

    // 이벤트
    public event Action OnAdWatchedSuccessfully;
    public event Action OnAdFailed;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAds();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // 메인 스레드에서 광고 콜백 처리
        if (pendingRewardSuccess)
        {
            pendingRewardSuccess = false;
            Debug.Log("[AdsManager] Processing reward success on main thread");

            // 게임 횟수 추가
            if (GameCreditManager.Instance != null)
            {
                GameCreditManager.Instance.AddGameFromAd();
            }

            // 성공 이벤트 발생
            OnAdWatchedSuccessfully?.Invoke();
        }

        if (pendingAdFailed)
        {
            pendingAdFailed = false;
            Debug.Log("[AdsManager] Processing ad failed on main thread");
            OnAdFailed?.Invoke();
        }
    }

    /// <summary>
    /// Google Mobile Ads SDK 초기화
    /// </summary>
    void InitializeAds()
    {
        // 플랫폼별 광고 단위 ID 설정
#if UNITY_ANDROID
        adUnitId = useTestAds ? TEST_ANDROID_AD_UNIT : androidAdUnitId;
#elif UNITY_IOS
        adUnitId = useTestAds ? TEST_IOS_AD_UNIT : iosAdUnitId;
#else
        adUnitId = TEST_ANDROID_AD_UNIT; // 에디터에서는 테스트 ID 사용
#endif

        Debug.Log($"[AdsManager] Initializing Google Mobile Ads with Ad Unit ID: {adUnitId}, Test Mode: {useTestAds}");

        // Google Mobile Ads SDK 초기화
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("[AdsManager] Google Mobile Ads SDK initialized successfully");

            // 어린이 지향 설정 (COPPA 준수)
            RequestConfiguration requestConfiguration = new RequestConfiguration
            {
                TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
                MaxAdContentRating = MaxAdContentRating.G
            };

            MobileAds.SetRequestConfiguration(requestConfiguration);
            Debug.Log("[AdsManager] Child-directed treatment enabled with G rating");

            isInitialized = true;

            // 초기화 완료 후 광고 로드
            LoadRewardedAd();
        });
    }

    /// <summary>
    /// 보상형 광고 로드
    /// </summary>
    public void LoadRewardedAd()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[AdsManager] SDK not initialized yet");
            return;
        }

        // 기존 광고 정리
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        Debug.Log($"[AdsManager] Loading rewarded ad: {adUnitId}");

        // 광고 요청 생성
        AdRequest adRequest = new AdRequest();

        // 보상형 광고 로드
        RewardedAd.Load(adUnitId, adRequest, (RewardedAd ad, LoadAdError loadError) =>
        {
            if (loadError != null)
            {
                Debug.LogError($"[AdsManager] Failed to load ad: {loadError.GetMessage()}");
                isAdLoaded = false;
                return;
            }
            else if (ad == null)
            {
                Debug.LogError("[AdsManager] Rewarded ad is null");
                isAdLoaded = false;
                return;
            }

            Debug.Log($"[AdsManager] Rewarded ad loaded successfully");
            rewardedAd = ad;
            isAdLoaded = true;

            // 광고 이벤트 리스너 등록
            RegisterAdEvents(ad);
        });
    }

    /// <summary>
    /// 광고 이벤트 리스너 등록
    /// </summary>
    private void RegisterAdEvents(RewardedAd ad)
    {
        // 광고가 열릴 때
        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[AdsManager] Rewarded ad opened");
        };

        // 광고가 닫힐 때
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AdsManager] Rewarded ad closed");

            // 광고가 닫히면 상태만 업데이트 (다음 광고 요청 시 로드)
            isAdLoaded = false;
        };

        // 광고 표시 실패 시
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"[AdsManager] Rewarded ad failed to show: {error.GetMessage()}");
            isAdLoaded = false;

            // 메인 스레드에서 처리하도록 플래그 설정
            pendingAdFailed = true;

            // 실패 시에도 즉시 로드하지 않음 (다음 광고 요청 시 로드)
        };

        // 광고 수익 발생 (사용자가 광고 시청 완료)
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"[AdsManager] Ad paid: {adValue.Value} {adValue.CurrencyCode}");
        };
    }

    /// <summary>
    /// 보상형 광고 표시
    /// </summary>
    public void ShowRewardedAd()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[AdsManager] SDK not initialized");
            OnAdFailed?.Invoke();
            return;
        }

        if (rewardedAd != null && isAdLoaded)
        {
            Debug.Log("[AdsManager] Showing rewarded ad");

            rewardedAd.Show((Reward reward) =>
            {
                // 사용자가 광고를 끝까지 시청하고 보상을 받음
                Debug.Log($"[AdsManager] User earned reward: {reward.Amount} {reward.Type}");
                Debug.Log("[AdsManager] Rewarded ad watched successfully! Granting reward...");

                // 메인 스레드에서 처리하도록 플래그 설정
                pendingRewardSuccess = true;
            });
        }
        else
        {
            Debug.LogWarning("[AdsManager] Rewarded ad not ready. Loading now and retrying...");
            LoadRewardedAd();

            // 광고 로드 후 자동 재시도
            StartCoroutine(RetryShowAdAfterLoad());
        }
    }

    /// <summary>
    /// 광고 로드 후 자동 재시도
    /// </summary>
    private System.Collections.IEnumerator RetryShowAdAfterLoad()
    {
        float waitTime = 0f;
        float maxWaitTime = 10f; // 최대 10초 대기

        while (!isAdLoaded && waitTime < maxWaitTime)
        {
            yield return new WaitForSeconds(0.5f);
            waitTime += 0.5f;
        }

        if (isAdLoaded && rewardedAd != null)
        {
            Debug.Log("[AdsManager] Ad loaded successfully after retry, showing now");

            rewardedAd.Show((Reward reward) =>
            {
                Debug.Log($"[AdsManager] User earned reward: {reward.Amount} {reward.Type}");

                // 메인 스레드에서 처리하도록 플래그 설정
                pendingRewardSuccess = true;
            });
        }
        else
        {
            Debug.LogError("[AdsManager] Ad failed to load after 10 seconds");
            // 메인 스레드에서 처리하도록 플래그 설정
            pendingAdFailed = true;
        }
    }

    /// <summary>
    /// 광고 사용 가능 여부 확인
    /// </summary>
    public bool IsAdReady()
    {
        return isInitialized && isAdLoaded && rewardedAd != null;
    }

    void OnDestroy()
    {
        // 광고 리소스 정리
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
        }
    }
}
