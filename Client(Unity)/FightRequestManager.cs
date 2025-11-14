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
                Debug.Log("[FIGHT] Found existing DontDestroyOnLoad FightRequestPanel, using it");
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
                Debug.Log($"[FIGHT] Found GameObject '{obj.name}' in scene '{obj.scene.name}'");
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
                        Debug.Log($"[FIGHT] Found GameObject by keyword '{obj.name}' in scene '{obj.scene.name}'");
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

            Debug.Log($"[FIGHT] Found FightRequestPanel '{foundPanel.name}' in scene '{foundPanel.scene.name}' and set DontDestroyOnLoad");
            FindUIComponents();
        }
        else
        {
            Debug.LogError("[FIGHT] FightRequestPanel not found in any scene! Please add the panel to a scene.");
            ListAllGameObjectsForDebug();
            return;
        }
    }

    void ListAllGameObjectsForDebug()
    {
        Debug.Log("[FIGHT] Listing all GameObjects for debugging:");
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        int count = 0;
        foreach (GameObject obj in allObjects)
        {
            if (obj.scene.name != "" && obj.scene.name != "DontDestroyOnLoad")
            {
                Debug.Log($"[FIGHT] GameObject: '{obj.name}' in scene '{obj.scene.name}'");
                count++;
                if (count > 20) // 너무 많은 로그 방지
                {
                    Debug.Log("[FIGHT] ... (showing first 20 objects)");
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
            Debug.LogWarning("[FIGHT] TextMeshProUGUI not found in FightRequestPanel! Searching by name...");

            // 이름으로 텍스트 찾기
            Transform[] allChildren = fightRequestPanel.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.name.ToLower().Contains("text"))
                {
                    fightRequestText = child.GetComponent<TMPro.TextMeshProUGUI>();
                    if (fightRequestText != null)
                    {
                        Debug.Log($"[FIGHT] Found text component in '{child.name}'");
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
            Debug.Log($"[FIGHT] Found button: '{btn.name}'");

            if (btnName.Contains("accept") || btnName.Contains("yes"))
            {
                acceptButton = btn;
                Debug.Log($"[FIGHT] Assigned Accept button: '{btn.name}'");
            }
            else if (btnName.Contains("decline") || btnName.Contains("no") || btnName.Contains("cancel"))
            {
                declineButton = btn;
                Debug.Log($"[FIGHT] Assigned Decline button: '{btn.name}'");
            }
        }

        // 버튼이 여전히 없다면 Transform으로 찾기
        if (acceptButton == null || declineButton == null)
        {
            Debug.LogWarning("[FIGHT] Some buttons not found, searching by Transform names...");
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
                        Debug.Log($"[FIGHT] Found Accept button by Transform: '{child.name}'");
                    }
                    else if (declineButton == null && (childName.Contains("decline") || childName.Contains("no") || childName.Contains("cancel")))
                    {
                        declineButton = btn;
                        Debug.Log($"[FIGHT] Found Decline button by Transform: '{child.name}'");
                    }
                }
            }
        }

        // 결과 확인
        if (acceptButton == null)
            Debug.LogError("[FIGHT] AcceptButton still not found! Please check button names.");
        if (declineButton == null)
            Debug.LogError("[FIGHT] DeclineButton still not found! Please check button names.");
        if (fightRequestText == null)
            Debug.LogError("[FIGHT] TextMeshProUGUI still not found! Please check text component.");

        Debug.Log($"[FIGHT] UI search complete - Accept: {(acceptButton ? "✓" : "✗")}, Decline: {(declineButton ? "✓" : "✗")}, Text: {(fightRequestText ? "✓" : "✗")}");
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
        Debug.Log($"[FIGHT] ShowFightRequest called: {fromUser} wants {gameMode}");

        currentOpponent = fromUser;
        currentGameMode = gameMode;

        // UI 참조가 없으면 다시 찾기 (씬 전환 등으로 인해)
        if (fightRequestPanel == null || fightRequestText == null)
        {
            Debug.Log("[FIGHT] UI references missing, searching again...");
            FindExistingUI();
            SetupUI();
        }

        // 패널 활성화
        if (fightRequestPanel != null)
        {
            fightRequestPanel.SetActive(true);
            Debug.Log($"[FIGHT] Fight request panel activated");
        }
        else
        {
            Debug.LogError("[FIGHT] fightRequestPanel is still null after search!");
            return;
        }

        // 텍스트 설정: "친구이름 wants to fight you in rapid/blitz."
        if (fightRequestText != null)
        {
            fightRequestText.text = $"{fromUser} wants to fight you in {gameMode}.";
            Debug.Log($"[FIGHT] Updated fight request text");
        }
        else
        {
            Debug.LogError("[FIGHT] fightRequestText is still null after search!");
        }
    }

    public void OnAcceptButtonClicked()
    {
        Debug.Log($"Accepted fight request from {currentOpponent} for {currentGameMode}");

        // FightSocketManager를 통해 accept 메시지 전송
        if (FightSocketManager.Instance != null)
        {
            FightSocketManager.Instance.SendAcceptMessage(currentOpponent, currentGameMode);
        }
        else
        {
            Debug.LogError("FightSocketManager instance not found!");
        }

        // 패널 닫기
        HideFightRequestPanel();
    }

    public void OnDeclineButtonClicked()
    {
        Debug.Log($"Declined fight request from {currentOpponent} for {currentGameMode}");

        // FightSocketManager를 통해 decline 메시지 전송
        if (FightSocketManager.Instance != null)
        {
            FightSocketManager.Instance.SendDeclineMessage(currentOpponent, currentGameMode);
        }
        else
        {
            Debug.LogError("FightSocketManager instance not found!");
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