using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FightRequestManager : MonoBehaviour
{
    [Header("Fight Request Panel")]
    [SerializeField] GameObject fightRequestPanel;
    [SerializeField] TextMeshProUGUI fightRequestText;
    [SerializeField] Button acceptButton;
    [SerializeField] Button declineButton;

    private string currentOpponent;
    private string currentGameMode;

    void Start()
    {
        // 싱글톤 패턴과 DontDestroyOnLoad 설정
        FightRequestManager[] existing = FindObjectsOfType<FightRequestManager>();
        if (existing.Length > 1)
        {
            // 중복이면 가장 최신 것 제외하고 파괴
            for (int i = 0; i < existing.Length - 1; i++)
            {
                Destroy(existing[i].gameObject);
            }
        }

        DontDestroyOnLoad(gameObject);

        // 기존 DontDestroyOnLoad된 FightRequestPanel이 있는지 확인
        GameObject[] dontDestroyPanels = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (GameObject obj in dontDestroyPanels)
        {
            if (obj.name == "FightRequestPanel" && obj.scene.name == "DontDestroyOnLoad")
            {
                fightRequestPanel = obj;
                FindUIComponents();
                SetupUI();
                return;
            }
        }

        // 새로운 패널을 찾아서 설정
        FindExistingUI();
        SetupUI();
    }

    void FindExistingUI()
    {
        // 씬에서 FightRequestPanel을 이름으로 찾기 (비활성화 상태도 포함)
        GameObject foundPanel = null;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("FightRequestPanel") && obj.scene.name != "" && obj.scene.name != "DontDestroyOnLoad")
            {
                foundPanel = obj;
                break;
            }
        }

        // 더 넓은 범위로 검색 (Fight, Request 등 키워드 포함)
        if (foundPanel == null)
        {
            foreach (GameObject obj in allObjects)
            {
                string objName = obj.name.ToLower();
                if ((objName.Contains("fight") && objName.Contains("request")) ||
                    (objName.Contains("fight") && objName.Contains("panel")) ||
                    objName.Contains("fightrequest"))
                {
                    if (obj.scene.name != "" && obj.scene.name != "DontDestroyOnLoad")
                    {
                        foundPanel = obj;
                        break;
                    }
                }
            }
        }

        if (foundPanel != null)
        {
            fightRequestPanel = foundPanel;

            // FightRequestPanel도 DontDestroyOnLoad로 설정
            DontDestroyOnLoad(fightRequestPanel);

            FindUIComponents();
        }
        else
        {
            ListAllGameObjectsForDebug();
            return;
        }
    }

    void ListAllGameObjectsForDebug()
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        int count = 0;
        foreach (GameObject obj in allObjects)
        {
            if (obj.scene.name != "" && obj.scene.name != "DontDestroyOnLoad")
            {
                count++;
                if (count > 20) // 너무 많은 로그 방지
                {
                    break;
                }
            }
        }
    }

    void FindUIComponents()
    {
        if (fightRequestPanel == null) return;

        // 텍스트 컴포넌트 찾기 (비활성화 상태에서도 검색)
        fightRequestText = fightRequestPanel.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (fightRequestText == null)
        {

            // 이름으로 텍스트 찾기
            Transform[] allChildren = fightRequestPanel.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.name.ToLower().Contains("text"))
                {
                    fightRequestText = child.GetComponent<TMPro.TextMeshProUGUI>();
                    if (fightRequestText != null)
                    {
                        break;
                    }
                }
            }
        }

        // 버튼들 찾기 (비활성화 상태에서도 검색)
        Button[] buttons = fightRequestPanel.GetComponentsInChildren<Button>(true);

        foreach (Button btn in buttons)
        {
            string btnName = btn.name.ToLower();

            if (btnName.Contains("accept") || btnName.Contains("yes"))
            {
                acceptButton = btn;
            }
            else if (btnName.Contains("decline") || btnName.Contains("no") || btnName.Contains("cancel"))
            {
                declineButton = btn;
            }
        }

        // 버튼이 여전히 없다면 Transform으로 찾기
        if (acceptButton == null || declineButton == null)
        {
            Transform[] allChildren = fightRequestPanel.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in allChildren)
            {
                string childName = child.name.ToLower();
                Button btn = child.GetComponent<Button>();

                if (btn != null)
                {
                    if (acceptButton == null && (childName.Contains("accept") || childName.Contains("yes")))
                    {
                        acceptButton = btn;
                    }
                    else if (declineButton == null && (childName.Contains("decline") || childName.Contains("no") || childName.Contains("cancel")))
                    {
                        declineButton = btn;
                    }
                }
            }
        }

        // 결과 확인

    }


    void SetupUI()
    {
        // 패널 초기에는 비활성화
        if (fightRequestPanel != null)
        {
            fightRequestPanel.SetActive(false);
        }

        // Accept 버튼 설정
        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(() => OnAcceptButtonClicked());
        }

        // Decline 버튼 설정
        if (declineButton != null)
        {
            declineButton.onClick.RemoveAllListeners();
            declineButton.onClick.AddListener(() => OnDeclineButtonClicked());
        }
    }

    public void ShowFightRequest(string fromUser, string gameMode)
    {

        currentOpponent = fromUser;
        currentGameMode = gameMode;

        // UI 참조가 없으면 다시 찾기 (씬 전환 등으로 인해)
        if (fightRequestPanel == null || fightRequestText == null)
        {
            FindExistingUI();
            SetupUI();
        }

        // 패널 활성화
        if (fightRequestPanel != null)
        {
            fightRequestPanel.SetActive(true);
        }
        else
        {
            return;
        }

        // 텍스트 설정: "친구이름 wants to fight you in rapid/blitz."
        if (fightRequestText != null)
        {
            fightRequestText.text = $"{fromUser} wants to fight you in {gameMode}.";
        }
        else
        {
        }
    }

    public void OnAcceptButtonClicked()
    {

        // FightSocketManager를 통해 accept 메시지 전송
        if (FightSocketManager.Instance != null)
        {
            FightSocketManager.Instance.SendAcceptMessage(currentOpponent, currentGameMode);
        }
        else
        {
        }

        // 패널 닫기
        HideFightRequestPanel();
    }

    public void OnDeclineButtonClicked()
    {

        // FightSocketManager를 통해 decline 메시지 전송
        if (FightSocketManager.Instance != null)
        {
            FightSocketManager.Instance.SendDeclineMessage(currentOpponent, currentGameMode);
        }
        else
        {
        }

        // 패널 닫기
        HideFightRequestPanel();
    }

    void HideFightRequestPanel()
    {
        if (fightRequestPanel != null)
        {
            fightRequestPanel.SetActive(false);
        }

        currentOpponent = null;
        currentGameMode = null;
    }
}