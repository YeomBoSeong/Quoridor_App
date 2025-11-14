using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class HistorySceneManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Player Info")]
    [SerializeField] private TextMeshProUGUI myNameText;
    [SerializeField] private TextMeshProUGUI myEloText;
    [SerializeField] private TextMeshProUGUI myTimeText;
    [SerializeField] private TextMeshProUGUI myWallCountText;
    [SerializeField] private Image myProfileImage;
    [SerializeField] private Sprite defaultMyProfileSprite;

    [SerializeField] private TextMeshProUGUI opponentNameText;
    [SerializeField] private TextMeshProUGUI opponentEloText;
    [SerializeField] private TextMeshProUGUI opponentTimeText;
    [SerializeField] private TextMeshProUGUI opponentWallCountText;
    [SerializeField] private Image opponentProfileImage;
    [SerializeField] private Sprite defaultOpponentProfileSprite;

    [Header("Game Board")]
    [SerializeField] private Transform gameBoard;
    [SerializeField] private GameObject redPlayerPawn;
    [SerializeField] private GameObject bluePlayerPawn;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private Canvas gameCanvas; // 벽을 생성할 Canvas

    [Header("Board Settings")]
    [SerializeField] private float cellSize = 99.20454f; // 화면 비율에 따라 조정됨
    [SerializeField] private Vector3 boardCenter = Vector3.zero; // 보드 중앙

    [Header("Sound Effects")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip moveSound; // 수를 둘 때 재생할 소리

    private GameDetailResponse currentGameDetail;
    private List<GameMoveData> moves;
    private int currentMoveIndex = -1; // -1은 게임 시작 상태

    // 게임 상태 추적 (현재 사용자 기준)
    private Vector2 myPosition = new Vector2(4, 0); // 내 말은 항상 하단 중앙에서 시작
    private Vector2 opponentPosition = new Vector2(4, 8); // 상대방 말은 항상 상단 중앙에서 시작
    private int myWallsRemaining = 10;
    private int opponentWallsRemaining = 10;
    private List<GameObject> placedWalls = new List<GameObject>();

    // 현재 사용자가 게임에서 어느 플레이어인지 추적
    private bool isCurrentUserPlayer1 = false;
    private string currentUsername;

    void Start()
    {
        SetupButtons();
        LoadSelectedGame();
        InitializeBoard();
    }

    void SetupButtons()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClicked);

        if (previousButton != null)
            previousButton.onClick.AddListener(OnPreviousButtonClicked);

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextButtonClicked);
    }

    void LoadSelectedGame()
    {
        int gameId = PlayerPrefs.GetInt("SelectedGameId", -1);
        if (gameId == -1)
        {
            Debug.LogError("No game ID found in PlayerPrefs");
            OnBackButtonClicked();
            return;
        }

        Debug.Log($"Loading game detail for ID: {gameId}");

        // GameHistoryManager 초기화 및 이벤트 구독
        if (GameHistoryManager.Instance == null)
        {
            GameObject managerGO = new GameObject("GameHistoryManager");
            managerGO.AddComponent<GameHistoryManager>();
        }

        GameHistoryManager.Instance.OnGameDetailLoaded += OnGameDetailLoaded;
        GameHistoryManager.Instance.OnError += OnGameDetailError;

        // 게임 상세 정보 로드
        GameHistoryManager.Instance.LoadGameDetail(gameId);
    }

    void OnGameDetailLoaded(GameDetailResponse gameDetail)
    {
        currentGameDetail = gameDetail;
        moves = new List<GameMoveData>(gameDetail.moves);

        // 현재 사용자 정보 가져오기
        currentUsername = SessionData.username;

        // 현재 사용자가 player1인지 player2인지 확인
        isCurrentUserPlayer1 = (currentGameDetail.player1_username == currentUsername);

        Debug.Log($"Loaded game detail: {moves.Count} moves");
        Debug.Log($"Current user: {currentUsername}, Is Player1: {isCurrentUserPlayer1}");

        // 플레이어 정보 표시
        DisplayPlayerInfo();

        // 프로필 이미지 로드
        LoadProfileImages();

        // 초기 게임 상태로 설정
        ResetToGameStart();
        UpdateUI();
    }

    void OnGameDetailError(string errorMessage)
    {
        Debug.LogError($"Failed to load game detail: {errorMessage}");
        OnBackButtonClicked();
    }

    void DisplayPlayerInfo()
    {
        if (currentGameDetail == null) return;

        string myName, opponentName;

        // 현재 사용자가 player1인지 player2인지에 따라 정보 설정
        if (isCurrentUserPlayer1)
        {
            myName = currentGameDetail.player1_username;
            opponentName = currentGameDetail.player2_username;
        }
        else
        {
            myName = currentGameDetail.player2_username;
            opponentName = currentGameDetail.player1_username;
        }

        // 내 정보 표시
        if (myNameText != null)
            myNameText.text = myName;

        // 상대방 정보 표시
        if (opponentNameText != null)
            opponentNameText.text = opponentName;

        // TODO: ELO 정보는 별도 API로 가져와야 할 수 있음
        if (myEloText != null)
            myEloText.text = "1500"; // 임시값

        if (opponentEloText != null)
            opponentEloText.text = "1500"; // 임시값

        // 기본 프로필 이미지 설정
        SetDefaultProfileImages();
    }

    void SetDefaultProfileImages()
    {
        if (myProfileImage != null && defaultMyProfileSprite != null)
        {
            myProfileImage.sprite = defaultMyProfileSprite;
        }

        if (opponentProfileImage != null && defaultOpponentProfileSprite != null)
        {
            opponentProfileImage.sprite = defaultOpponentProfileSprite;
        }
    }

    void LoadProfileImages()
    {
        if (currentGameDetail == null) return;

        string myName, opponentName;

        // 현재 사용자가 player1인지 player2인지에 따라 정보 설정
        if (isCurrentUserPlayer1)
        {
            myName = currentGameDetail.player1_username;
            opponentName = currentGameDetail.player2_username;
        }
        else
        {
            myName = currentGameDetail.player2_username;
            opponentName = currentGameDetail.player1_username;
        }

        // 내 프로필 이미지 로드
        StartCoroutine(LoadUserProfileImage(myName, myProfileImage));

        // 상대방 프로필 이미지 로드
        StartCoroutine(LoadUserProfileImage(opponentName, opponentProfileImage));
    }

    IEnumerator LoadUserProfileImage(string username, Image targetImage)
    {
        if (targetImage == null || string.IsNullOrEmpty(username))
            yield break;

        // 1단계: 사용자 ID 가져오기
        string userUrl = $"{ServerConfig.GetHttpUrl()}/user/{username}";
        int userId = -1;

        using (UnityWebRequest request = UnityWebRequest.Get(userUrl))
        {
            request.SetRequestHeader("Authorization", $"Bearer {SessionData.token}");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var userResponse = JsonUtility.FromJson<MeResponse>(request.downloadHandler.text);
                    userId = userResponse.id;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing user info for {username}: {e.Message}");
                    yield break;
                }
            }
            else
            {
                Debug.LogError($"Failed to get user info for {username}: {request.error}");
                yield break;
            }
        }

        // 2단계: 프로필 이미지 로드
        if (userId > 0)
        {
            string imageUrl = $"{ServerConfig.GetHttpUrl()}/profile-image/{userId}";

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
            {
                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D downloadedTexture = DownloadHandlerTexture.GetContent(request);

                    if (downloadedTexture != null)
                    {
                        Sprite newSprite = Sprite.Create(downloadedTexture,
                            new Rect(0, 0, downloadedTexture.width, downloadedTexture.height),
                            new Vector2(0.5f, 0.5f));
                        targetImage.sprite = newSprite;

                        Debug.Log($"Loaded profile image for {username}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to load profile image for {username}: {request.error}");
                    // 실패 시 기본 이미지 유지
                }
            }
        }
    }

    void InitializeBoard()
    {
        // 플레이어 폰들이 이미 씬에 존재하므로 별도 생성하지 않음
        // 폰들의 초기 위치만 설정
        ResetToGameStart();
    }

    void ResetToGameStart()
    {
        // 서버에서 기록된 실제 초기 위치 사용
        // GameManager: Red 초기 위치 (Y=16, X=8), Blue 초기 위치 (Y=0, X=8)
        // Vector2(X, Y) = Vector2(boardX, boardY)
        if (isCurrentUserPlayer1)
        {
            // 현재 사용자가 Player1(Red)이었다면
            myPosition = new Vector2(8, 16); // Red 초기 위치: Vector2(X=8, Y=16)
            opponentPosition = new Vector2(8, 0); // Blue 초기 위치: Vector2(X=8, Y=0)
        }
        else
        {
            // 현재 사용자가 Player2(Blue)였다면
            myPosition = new Vector2(8, 0); // Blue 초기 위치: Vector2(X=8, Y=0)
            opponentPosition = new Vector2(8, 16); // Red 초기 위치: Vector2(X=8, Y=16)
        }

        myWallsRemaining = 10;
        opponentWallsRemaining = 10;
        currentMoveIndex = -1;

        // 모든 벽 제거
        foreach (GameObject wall in placedWalls)
        {
            if (wall != null)
                Destroy(wall);
        }
        placedWalls.Clear();

        // 폰 위치 업데이트
        UpdatePawnPositions();
        Debug.Log($"Reset to game start: Current user is Player{(isCurrentUserPlayer1 ? "1" : "2")}, My position ({myPosition.x}, {myPosition.y}), Opponent position ({opponentPosition.x}, {opponentPosition.y})");
    }

    void UpdatePawnPositions()
    {
        // 원래 플레이어 역할에 따라 올바른 색깔 할당
        if (isCurrentUserPlayer1)
        {
            // 현재 사용자가 원래 Player1(Red)이었다면
            if (redPlayerPawn != null)
            {
                Vector3 pos = BoardPositionToWorldPosition(myPosition, true); // 내 말 (Red)
                redPlayerPawn.transform.localPosition = pos;
            }

            if (bluePlayerPawn != null)
            {
                Vector3 pos = BoardPositionToWorldPosition(opponentPosition, false); // 상대방 말 (Blue)
                bluePlayerPawn.transform.localPosition = pos;
            }
        }
        else
        {
            // 현재 사용자가 원래 Player2(Blue)였다면
            if (bluePlayerPawn != null)
            {
                Vector3 pos = BoardPositionToWorldPosition(myPosition, true); // 내 말 (Blue)
                bluePlayerPawn.transform.localPosition = pos;
            }

            if (redPlayerPawn != null)
            {
                Vector3 pos = BoardPositionToWorldPosition(opponentPosition, false); // 상대방 말 (Red)
                redPlayerPawn.transform.localPosition = pos;
            }
        }
    }

    Vector3 BoardPositionToWorldPosition(Vector2 boardPos, bool isMyPawn)
    {
        // GameManager의 BoardToWorldPosition과 동일한 로직 사용
        // 0~8 그리드 시스템으로 변환 (홀수 좌표도 지원)
        float gridX = boardPos.x / 2.0f;  // boardPos.x는 X축 (boardX)
        float gridY = boardPos.y / 2.0f;  // boardPos.y는 Y축 (boardY)

        float worldX, worldY;

        // 현재 사용자의 원래 색깔에 따라 좌표 변환
        if (isCurrentUserPlayer1)
        {
            // 현재 사용자가 원래 Player1(Red)이었다면: Red 관점 변환
            worldX = (gridX - 4) * cellSize;
            worldY = (4 - gridY) * cellSize;
        }
        else
        {
            // 현재 사용자가 원래 Player2(Blue)였다면: Blue 관점 변환
            // (Blue가 아래쪽으로 오도록 180도 회전)
            worldX = (4 - gridX) * cellSize;
            worldY = (gridY - 4) * cellSize;
        }

        Vector3 worldPos = boardCenter + new Vector3(worldX, worldY, 0);
        Debug.Log($"BoardPos({boardPos.x}, {boardPos.y}) -> Grid({gridX}, {gridY}) -> World({worldX}, {worldY}) [Player{(isCurrentUserPlayer1 ? "1" : "2")}]");
        return worldPos;
    }

    // 좌표 변환 메서드들은 제거됨 - GameManager와 일치하도록 직접 좌표 사용

    void UpdateUI()
    {
        // 시간 정보 업데이트
        if (currentMoveIndex >= 0 && currentMoveIndex < moves.Count)
        {
            var currentMove = moves[currentMoveIndex];

            // 수를 둔 플레이어가 나인지 상대방인지 확인
            bool isMoveByMe = (currentMove.player_username == currentUsername);

            if (isMoveByMe)
            {
                if (myTimeText != null)
                    myTimeText.text = FormatTime(currentMove.remaining_time);
            }
            else
            {
                if (opponentTimeText != null)
                    opponentTimeText.text = FormatTime(currentMove.remaining_time);
            }
        }
        else
        {
            // 게임 시작 상태
            if (myTimeText != null)
                myTimeText.text = "10:00";
            if (opponentTimeText != null)
                opponentTimeText.text = "10:00";
        }

        // 벽 개수 업데이트
        if (myWallCountText != null)
            myWallCountText.text = myWallsRemaining.ToString();

        if (opponentWallCountText != null)
            opponentWallCountText.text = opponentWallsRemaining.ToString();

        // 버튼 활성화/비활성화
        if (previousButton != null)
            previousButton.interactable = currentMoveIndex >= 0;

        if (nextButton != null)
            nextButton.interactable = currentMoveIndex < moves.Count - 1;
    }

    string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60);
        int secs = Mathf.FloorToInt(seconds % 60);
        return $"{minutes:D2}:{secs:D2}";
    }

    void OnPreviousButtonClicked()
    {
        if (currentMoveIndex >= 0)
        {
            UndoMove(currentMoveIndex);
            currentMoveIndex--;
            UpdateUI();

            // 버튼 클릭 시 소리 재생
            PlayMoveSound();
        }
    }

    void OnNextButtonClicked()
    {
        if (currentMoveIndex < moves.Count - 1)
        {
            currentMoveIndex++;
            ApplyMove(currentMoveIndex);
            UpdateUI();

            // 버튼 클릭 시 소리 재생
            PlayMoveSound();
        }
    }

    void ApplyMove(int moveIndex)
    {
        if (moveIndex < 0 || moveIndex >= moves.Count) return;

        var move = moves[moveIndex];
        bool isMoveByMe = move.player_username == currentUsername;

        switch (move.move_type)
        {
            case "move":
                ApplyPlayerMove(move, isMoveByMe);
                break;
            case "wall":
                ApplyWallPlacement(move, isMoveByMe);
                break;
            case "forfeit":
            case "disconnect":
                // 게임 종료 상태 처리
                break;
        }
    }

    void UndoMove(int moveIndex)
    {
        if (moveIndex < 0 || moveIndex >= moves.Count) return;

        var move = moves[moveIndex];
        bool isMoveByMe = move.player_username == currentUsername;

        switch (move.move_type)
        {
            case "move":
                UndoPlayerMove(moveIndex);
                break;
            case "wall":
                UndoWallPlacement(isMoveByMe);
                break;
        }
    }

    void ApplyPlayerMove(GameMoveData move, bool isMoveByMe)
    {
        if (string.IsNullOrEmpty(move.position_from)) return;

        string[] coords = move.position_from.Split(',');
        if (coords.Length == 2)
        {
            if (int.TryParse(coords[0], out int boardY) && int.TryParse(coords[1], out int boardX))
            {
                // 서버에서 오는 좌표: coords[0]=boardY(Y축), coords[1]=boardX(X축)
                // Vector2의 x는 X축, y는 Y축이므로 올바른 순서로 할당
                Vector2 newPos = new Vector2(boardX, boardY); // (X, Y) = (boardX, boardY)

                if (isMoveByMe)
                {
                    myPosition = newPos;
                }
                else
                {
                    opponentPosition = newPos;
                }

                UpdatePawnPositions();
                Debug.Log($"Applied move: {(isMoveByMe ? "My" : "Opponent")} move to boardY={boardY}, boardX={boardX} -> Vector2({newPos.x}, {newPos.y})");
                Debug.Log($"Current user is Player{(isCurrentUserPlayer1 ? "1" : "2")}, isMyMove={isMoveByMe}");
            }
        }
    }

    void UndoPlayerMove(int moveIndex)
    {
        // 이전 위치로 되돌리기 위해 이전 수들을 확인
        var currentMove = moves[moveIndex];
        bool isMoveByMe = currentMove.player_username == currentUsername;

        // 해당 플레이어의 이전 위치를 찾기
        Vector2 previousPos;

        // 초기 위치는 현재 사용자가 원래 어떤 플레이어였는지에 따라 결정
        // Vector2(X, Y) = Vector2(boardX, boardY)
        if (isMoveByMe)
        {
            // 내 초기 위치
            if (isCurrentUserPlayer1)
            {
                previousPos = new Vector2(8, 16); // Player1(Red) 초기 위치: Vector2(X=8, Y=16)
            }
            else
            {
                previousPos = new Vector2(8, 0); // Player2(Blue) 초기 위치: Vector2(X=8, Y=0)
            }
        }
        else
        {
            // 상대방 초기 위치
            if (isCurrentUserPlayer1)
            {
                previousPos = new Vector2(8, 0); // 상대방은 Player2(Blue) 초기 위치: Vector2(X=8, Y=0)
            }
            else
            {
                previousPos = new Vector2(8, 16); // 상대방은 Player1(Red) 초기 위치: Vector2(X=8, Y=16)
            }
        }

        for (int i = moveIndex - 1; i >= 0; i--)
        {
            var prevMove = moves[i];
            if (prevMove.move_type == "move" &&
                (prevMove.player_username == currentMove.player_username))
            {
                string[] coords = prevMove.position_from.Split(',');
                if (coords.Length == 2 &&
                    int.TryParse(coords[0], out int boardY) &&
                    int.TryParse(coords[1], out int boardX))
                {
                    // 서버에서 오는 좌표: coords[0]=boardY(Y축), coords[1]=boardX(X축)
                    // Vector2(X, Y) = Vector2(boardX, boardY)
                    previousPos = new Vector2(boardX, boardY);
                    break;
                }
            }
        }

        if (isMoveByMe)
            myPosition = previousPos;
        else
            opponentPosition = previousPos;

        UpdatePawnPositions();
    }

    void ApplyWallPlacement(GameMoveData move, bool isMoveByMe)
    {
        if (wallPrefab != null && !string.IsNullOrEmpty(move.position_from) && !string.IsNullOrEmpty(move.position_to))
        {
            // 벽 좌표 파싱
            string[] coord1 = move.position_from.Split(',');
            string[] coord2 = move.position_to.Split(',');

            if (coord1.Length == 2 && coord2.Length == 2 &&
                int.TryParse(coord1[0], out int y1) && int.TryParse(coord1[1], out int x1) &&
                int.TryParse(coord2[0], out int y2) && int.TryParse(coord2[1], out int x2))
            {
                // 벽 방향 확인 (가로벽: y1==y2, 세로벽: x1==x2)
                bool isHorizontal = (y1 == y2);

                // 벽 위치 계산 (GameManager 로직 참고)
                Vector2 wallUIPosition = CalculateWallUIPosition(y1, x1, y2, x2, isHorizontal);

                if (wallUIPosition != Vector2.zero)
                {
                    // 벽 생성 (Canvas를 부모로 사용, null 체크 포함)
                    Transform parentTransform = gameCanvas != null ? gameCanvas.transform : gameBoard;
                    GameObject wall = Instantiate(wallPrefab, parentTransform);
                    placedWalls.Add(wall);

                    // Canvas 사용 여부에 따라 다른 처리
                    if (gameCanvas != null)
                    {
                        // Canvas를 사용하는 경우: RectTransform.anchoredPosition 사용
                        RectTransform wallRect = wall.GetComponent<RectTransform>();
                        if (wallRect != null)
                        {
                            // 위치 설정 (GameManager와 동일한 anchoredPosition 사용)
                            wallRect.anchoredPosition = wallUIPosition;

                            // 회전 설정 (GameManager와 동일)
                            wallRect.rotation = isHorizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90);

                            // Z-order 설정으로 다른 오브젝트보다 앞에 표시
                            wallRect.SetAsLastSibling();

                            Debug.Log($"[WALL] Placed {(isHorizontal ? "horizontal" : "vertical")} wall at Canvas UI position {wallUIPosition}, Board coords: ({y1},{x1})-({y2},{x2})");
                        }
                    }
                    else
                    {
                        // gameBoard를 사용하는 경우: Transform.localPosition 사용
                        // (이 경우 좌표를 다르게 계산해야 함)
                        Vector3 worldPos = BoardPositionToWorldPosition(new Vector2((x1 + x2) * 0.5f, (y1 + y2) * 0.5f), false);
                        wall.transform.localPosition = worldPos;
                        wall.transform.rotation = isHorizontal ? Quaternion.identity : Quaternion.Euler(0, 0, 90);

                        Debug.Log($"[WALL] Placed {(isHorizontal ? "horizontal" : "vertical")} wall at World position {worldPos}, Board coords: ({y1},{x1})-({y2},{x2})");
                    }
                }
            }

            // 벽 개수 감소
            if (isMoveByMe)
                myWallsRemaining--;
            else
                opponentWallsRemaining--;
        }
    }

    Vector2 CalculateWallUIPosition(int y1, int x1, int y2, int x2, bool isHorizontal)
    {
        // GameManager와 동일한 벽 위치 계산 로직 사용
        float gridSize = cellSize; // 화면 비율에 따라 조정됨
        float unityX, unityY;

        if (isHorizontal)
        {
            // 가로벽: GameManager의 CheckHorizontalWallDirect 로직 사용
            // unityX = ((boardX + 1) * (gridSize / 2.0f)) - 396.8182;
            // unityY = 396.8182 - (boardY * (gridSize / 2.0f));
            int boardX = x1; // 첫 번째 X 좌표 사용
            int boardY = y1; // Y 좌표 (가로벽이므로 y1==y2)

            unityX = ((boardX + 1) * (gridSize / 2.0f)) - 396.8182f; // 벽 중심 X
            unityY = 396.8182f - (boardY * (gridSize / 2.0f)); // 벽 중심 Y
        }
        else
        {
            // 세로벽: GameManager의 CheckVerticalWallDirect 로직 사용
            // unityX = (boardX * (gridSize / 2.0f)) - 396.8182;
            // unityY = 396.8182 - ((boardY + 1) * (gridSize / 2.0f));
            int boardX = x1; // X 좌표 (세로벽이므로 x1==x2)
            int boardY = y1; // 첫 번째 Y 좌표 사용

            unityX = (boardX * (gridSize / 2.0f)) - 396.8182f; // 벽 중심 X
            unityY = 396.8182f - ((boardY + 1) * (gridSize / 2.0f)); // 벽 중심 Y
        }

        // 현재 사용자 관점에 따른 좌표 변환 (GameManager 방식)
        if (!isCurrentUserPlayer1)
        {
            // Player2(Blue)였다면 180도 회전된 관점 (GameManager의 Blue 플레이어 로직)
            unityX = -unityX;
            unityY = -unityY;
        }

        Debug.Log($"[WALL_CALC] {(isHorizontal ? "Horizontal" : "Vertical")} wall: Board({y1},{x1})-({y2},{x2}) -> UI({unityX},{unityY}) [Player{(isCurrentUserPlayer1 ? "1" : "2")}]");
        return new Vector2(unityX, unityY);
    }

    void UndoWallPlacement(bool isMoveByMe)
    {
        // 마지막에 배치된 벽 제거
        if (placedWalls.Count > 0)
        {
            int lastWallIndex = placedWalls.Count - 1;
            GameObject lastWall = placedWalls[lastWallIndex];
            if (lastWall != null)
                Destroy(lastWall);
            placedWalls.RemoveAt(lastWallIndex);

            // 벽 개수 복구
            if (isMoveByMe)
                myWallsRemaining++;
            else
                opponentWallsRemaining++;
        }
    }

    void OnBackButtonClicked()
    {
        SceneManager.LoadScene("ProfileScene");
    }

    void PlayMoveSound()
    {
        if (audioSource != null && moveSound != null)
        {
            audioSource.PlayOneShot(moveSound);
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (GameHistoryManager.Instance != null)
        {
            GameHistoryManager.Instance.OnGameDetailLoaded -= OnGameDetailLoaded;
            GameHistoryManager.Instance.OnError -= OnGameDetailError;
        }
    }
}