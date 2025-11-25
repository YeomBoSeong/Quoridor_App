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
    [SerializeField] private string androidAdUnitId = "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX"; // 실제 광고 단위 ID로 변경하세요
    [SerializeField] private string iosAdUnitId = "ca-app-pub-3940256099942544/1712485313"; // iOS 테스트 ID (필요시 실제 ID로 변경)

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

        // Google Mobile Ads SDK 초기화
        MobileAds.Initialize(initStatus =>
        {
            // 어린이 지향 설정 (COPPA 준수)
            RequestConfiguration requestConfiguration = new RequestConfiguration
            {
                TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
                MaxAdContentRating = MaxAdContentRating.G
            };

            MobileAds.SetRequestConfiguration(requestConfiguration);

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
            return;
        }

        // 기존 광고 정리
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        // 광고 요청 생성
        AdRequest adRequest = new AdRequest();

        // 보상형 광고 로드
        RewardedAd.Load(adUnitId, adRequest, (RewardedAd ad, LoadAdError loadError) =>
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
        };

        // 광고가 닫힐 때
        ad.OnAdFullScreenContentClosed += () =>
        {
            // 광고가 닫히면 상태만 업데이트 (다음 광고 요청 시 로드)
            isAdLoaded = false;
        };

        // 광고 표시 실패 시
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            isAdLoaded = false;

            // 메인 스레드에서 처리하도록 플래그 설정
            pendingAdFailed = true;

            // 실패 시에도 즉시 로드하지 않음 (다음 광고 요청 시 로드)
        };

        // 광고 수익 발생 (사용자가 광고 시청 완료)
        ad.OnAdPaid += (AdValue adValue) =>
        {
        };
    }

    /// <summary>
    /// 보상형 광고 표시
    /// </summary>
    public void ShowRewardedAd()
    {
        if (!isInitialized)
        {
            OnAdFailed?.Invoke();
            return;
        }

        if (rewardedAd != null && isAdLoaded)
        {
            rewardedAd.Show((Reward reward) =>
            {
                // 사용자가 광고를 끝까지 시청하고 보상을 받음

                // 메인 스레드에서 처리하도록 플래그 설정
                pendingRewardSuccess = true;
            });
        }
        else
        {
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
            rewardedAd.Show((Reward reward) =>
            {
                // 메인 스레드에서 처리하도록 플래그 설정
                pendingRewardSuccess = true;
            });
        }
        else
        {
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
