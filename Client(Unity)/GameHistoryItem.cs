using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameHistoryItem : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI opponentNameText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI eloChangeText;
    [SerializeField] private Button playButton;

    private GameHistoryData gameData;

    public void SetGameData(GameHistoryData data)
    {
        gameData = data;
        UpdateUI();
        SetupPlayButton();
    }

    void UpdateUI()
    {
        if (gameData == null) return;

        // 상대방 이름 설정
        if (opponentNameText != null)
        {
            opponentNameText.text = $"vs {gameData.opponent_username}";
        }

        // 결과 설정 (Win/Lose)
        if (resultText != null)
        {
            resultText.text = gameData.result;
            // 결과에 따른 색상 설정
            if (gameData.result == "Win")
            {
                resultText.color = Color.green;
            }
            else if (gameData.result == "Lose")
            {
                resultText.color = Color.red;
            }
        }

        // ELO 변화 설정
        if (eloChangeText != null)
        {
            string eloChangeText_str = "";
            if (gameData.my_elo_change > 0)
            {
                eloChangeText_str = $"+{gameData.my_elo_change}";
                eloChangeText.color = Color.green;
            }
            else
            {
                eloChangeText_str = gameData.my_elo_change.ToString();
                eloChangeText.color = Color.red;
            }
            eloChangeText.text = eloChangeText_str;
        }
    }

    void SetupPlayButton()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayButtonClicked);
        }
    }

    void OnPlayButtonClicked()
    {
        if (gameData != null)
        {
            // 게임 ID를 PlayerPrefs에 저장하여 HistoryScene에서 사용
            PlayerPrefs.SetInt("SelectedGameId", gameData.id);
            PlayerPrefs.Save();

            Debug.Log($"Loading HistoryScene for game ID: {gameData.id}");

            // HistoryScene으로 이동
            SceneManager.LoadScene("HistoryScene");
        }
    }
}