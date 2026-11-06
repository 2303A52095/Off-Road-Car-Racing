using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class OffRoadGameUI : MonoBehaviour
{
    private OffRoadVehicleController player;
    private OffRoadRaceDirector race;
    private OffRoadFollowCamera followCamera;
    private OffRoadWorldBuilder world;
    private Canvas canvas;
    private GameObject menuBackdrop;
    private GameObject menuPanel;
    private GameObject pausePanel;
    private GameObject hudPanel;
    private GameObject resultsPanel;
    private Text speedText;
    private Text rpmText;
    private Text gearText;
    private Text timerText;
    private Text timerHeading;
    private Text checkpointText;
    private Text lapText;
    private Text statusText;
    private Text vehicleChoice;
    private Text trackChoice;
    private Text modeChoice;
    private Text difficultyChoice;
    private Text graphicsChoice;
    private Text cameraChoice;
    private Text resultsTitle;
    private Text resultsSubtitle;
    private Text resultsTime;
    private Text resultsPosition;
    private Text resultsScore;
    private OffRoadGameMode selectedMode = OffRoadGameMode.OffRoadRace;
    private OffRoadDifficulty difficulty = OffRoadDifficulty.Normal;
    private int vehicleChoiceIndex;
    private bool isPaused;
    private RectTransform minimapCar;

    public void Initialize(OffRoadVehicleController playerVehicle, OffRoadRaceDirector raceDirector,
        OffRoadFollowCamera cameraController, OffRoadWorldBuilder worldBuilder)
    {
        player = playerVehicle;
        race = raceDirector;
        followCamera = cameraController;
        world = worldBuilder;
        int savedQuality = Mathf.Clamp(PlayerPrefs.GetInt("offroad_quality", QualitySettings.GetQualityLevel()),
            0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(savedQuality, true);
        BuildCanvas();
        BuildMenu();
        BuildHud();
        BuildPauseMenu();
        BuildResultsPanel();
        Time.timeScale = 0f;
        if (OffRoadGameBootstrap.ConsumeRaceRestart(out OffRoadGameMode restartMode,
            out OffRoadDifficulty restartDifficulty))
        {
            selectedMode = restartMode;
            difficulty = restartDifficulty;
            StartSelectedGame();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && !isPaused &&
            hudPanel.activeSelf && !race.IsFinished)
        {
            player.ResetVehicle();
            race.StartGame(selectedMode, difficulty);
        }

        speedText.text = Mathf.RoundToInt(player.SpeedKmh).ToString("000");
        rpmText.text = "RPM  " + Mathf.RoundToInt(player.EngineRpm / 100f).ToString("00") + " x100";
        gearText.text = player.CurrentGear < 0 ? "R" : player.CurrentGear == 0 ? "N" : player.CurrentGear.ToString();
        timerText.text = race.GetTimerLabel();
        timerHeading.text = race.GetTimerHeading();
        checkpointText.text = race.GetCheckpointLabel();
        lapText.text = race.Mode == OffRoadGameMode.FreeRoam
            ? "FREE DRIVE"
            : "LAP " + Mathf.Min(race.CurrentLap, race.TotalLaps).ToString("00") + " / " + race.TotalLaps.ToString("00");
        statusText.text = race.StatusMessage;
        cameraChoice.text = followCamera.ViewName;
        if (race.IsFinished)
        {
            if (!resultsPanel.activeSelf)
            {
                pausePanel.SetActive(false);
                isPaused = false;
                hudPanel.SetActive(false);
                resultsPanel.SetActive(true);
                resultsPanel.transform.SetAsLastSibling();
                if (race.PlayerWon)
                    followCamera.BeginFinishCinematic();
                Time.timeScale = 0f;
            }

            resultsTitle.text = race.PlayerWon ? "FINISH  //  YOU WIN!" : "YOU LOSE";
            resultsSubtitle.text = race.PlayerWon ? "Race Completed!" : "Time limit reached";
            resultsTime.text = "TOTAL TIME  //  "
                + OffRoadRaceDirector.FormatTime(race.ElapsedTime);
            resultsPosition.text = "FINAL POSITION  //  " + race.FinalPosition + " / " + race.RacerCount;
            resultsScore.text = "SCORE  //  " + race.FinalScore.ToString("0000");
        }
        UpdateMinimap();
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("OFFROAD // U I");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600f, 900f);
        canvasObject.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        UnityEngine.InputSystem.UI.InputSystemUIInputModule inputModule =
            eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (inputModule == null)
            inputModule = eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        inputModule.enabled = true;
        inputModule.AssignDefaultActions();
        BaseInputModule[] inputModules = eventSystem.GetComponents<BaseInputModule>();
        for (int i = 0; i < inputModules.Length; i++)
        {
            if (inputModules[i] != inputModule)
                inputModules[i].enabled = false;
        }
    }

    private void BuildMenu()
    {
        menuBackdrop = CreateFullscreenOverlay(canvas.transform, new Color(0.015f, 0.025f, 0.03f, 0.34f)).gameObject;
        menuPanel = CreatePanel("Main menu", canvas.transform, new Vector2(620f, 730f),
            new Vector2(0f, 0f), new Color(0.025f, 0.045f, 0.055f, 0.94f));
        CreatePanel("Menu accent", menuPanel.transform, new Vector2(5f, 560f),
            new Vector2(-302f, -4f), new Color(1f, 0.48f, 0.14f, 0.95f));
        AddText(menuPanel.transform, "OVERLAND", 48, new Vector2(0f, 300f), new Vector2(540f, 64f),
            new Color(1f, 0.57f, 0.19f), FontStyle.Bold);
        AddText(menuPanel.transform, "OFF-ROAD MOTORSPORT  /  EXPEDITION SERIES", 14, new Vector2(0f, 260f),
            new Vector2(500f, 28f), new Color(0.68f, 0.77f, 0.76f), FontStyle.Bold);
        CreatePanel("Header divider", menuPanel.transform, new Vector2(440f, 1f),
            new Vector2(0f, 232f), new Color(0.52f, 0.61f, 0.58f, 0.42f));

        modeChoice = AddText(menuPanel.transform, "", 18, new Vector2(0f, 198f), new Vector2(520f, 36f),
            Color.white, FontStyle.Bold);
        AddButton(menuPanel.transform, "CHANGE EVENT", new Vector2(0f, 158f),
            new Vector2(440f, 46f), CycleMode);
        vehicleChoice = AddText(menuPanel.transform, "", 16, new Vector2(0f, 108f), new Vector2(500f, 30f),
            Color.white, FontStyle.Bold);
        AddButton(menuPanel.transform, "CHANGE VEHICLE", new Vector2(0f, 70f), new Vector2(440f, 42f),
            CycleVehicle);
        trackChoice = AddText(menuPanel.transform, "TRACK  //  HIGHLAND CIRCUIT", 16,
            new Vector2(0f, 20f), new Vector2(500f, 30f),
            Color.white, FontStyle.Bold);
        difficultyChoice = AddText(menuPanel.transform, "", 16, new Vector2(0f, -68f), new Vector2(500f, 30f),
            Color.white, FontStyle.Bold);
        AddButton(menuPanel.transform, "CHANGE DIFFICULTY", new Vector2(0f, -106f), new Vector2(440f, 42f),
            CycleDifficulty);
        AddButton(menuPanel.transform, "START EXPEDITION", new Vector2(0f, -168f), new Vector2(440f, 54f),
            StartSelectedGame, true);
        graphicsChoice = AddText(menuPanel.transform, "", 13, new Vector2(0f, -226f),
            new Vector2(500f, 26f), new Color(0.75f, 0.82f, 0.79f), FontStyle.Bold);
        AddButton(menuPanel.transform, "CYCLE GRAPHICS QUALITY", new Vector2(0f, -263f),
            new Vector2(440f, 38f), ShowSettings);
        AddButton(menuPanel.transform, "EXIT GAME", new Vector2(0f, -316f), new Vector2(440f, 38f), ExitGame);

        AddText(canvas.transform, "WASD / ARROWS  DRIVE    •    SPACE  HANDBRAKE    •    C  CAMERA    •    R  RESET    •    ESC  PAUSE",
            12, new Vector2(0f, -430f), new Vector2(1200f, 28f), new Color(0.82f, 0.88f, 0.83f), FontStyle.Bold);
        RefreshSelections();
    }

    private void BuildHud()
    {
        hudPanel = new GameObject("Race HUD");
        hudPanel.transform.SetParent(canvas.transform, false);
        hudPanel.AddComponent<RectTransform>().anchorMin = Vector2.zero;
        hudPanel.GetComponent<RectTransform>().anchorMax = Vector2.one;
        hudPanel.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        hudPanel.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        CreatePanel("Telemetry", hudPanel.transform, new Vector2(400f, 146f),
            new Vector2(-570f, -345f), new Color(0.02f, 0.04f, 0.05f, 0.82f));
        CreatePanel("Race timing", hudPanel.transform, new Vector2(320f, 154f),
            new Vector2(0f, 373f), new Color(0.02f, 0.04f, 0.05f, 0.76f));
        speedText = AddText(hudPanel.transform, "000", 58, new Vector2(-640f, -334f), new Vector2(190f, 76f),
            Color.white, FontStyle.Bold);
        AddText(hudPanel.transform, "KM/H", 16, new Vector2(-630f, -383f), new Vector2(140f, 27f),
            new Color(1f, 0.57f, 0.19f), FontStyle.Bold);
        rpmText = AddText(hudPanel.transform, "RPM  00 x100", 14, new Vector2(-475f, -324f),
            new Vector2(175f, 25f), Color.white, FontStyle.Bold);
        gearText = AddText(hudPanel.transform, "1", 40, new Vector2(-475f, -369f), new Vector2(80f, 58f),
            new Color(1f, 0.57f, 0.19f), FontStyle.Bold);

        timerText = AddText(hudPanel.transform, "00:00.00", 30, new Vector2(0f, 405f), new Vector2(260f, 44f),
            Color.white, FontStyle.Bold);
        timerHeading = AddText(hudPanel.transform, "RACE TIME LEFT", 11,
            new Vector2(0f, 434f), new Vector2(260f, 20f),
            new Color(0.74f, 0.82f, 0.79f), FontStyle.Bold);
        checkpointText = AddText(hudPanel.transform, "00 / 10", 16, new Vector2(0f, 370f),
            new Vector2(260f, 26f), new Color(1f, 0.57f, 0.19f), FontStyle.Bold);
        lapText = AddText(hudPanel.transform, "LAP 01 / 02", 14, new Vector2(0f, 345f),
            new Vector2(260f, 24f), Color.white, FontStyle.Bold);
        statusText = AddText(hudPanel.transform, "", 20, new Vector2(0f, 315f), new Vector2(560f, 40f),
            Color.white, FontStyle.Bold);
        cameraChoice = AddText(hudPanel.transform, "CHASE CAM", 12, new Vector2(650f, -405f),
            new Vector2(190f, 28f), Color.white, FontStyle.Bold);

        CreateMinimap();
        CreateDrivingControls();
        AddButton(hudPanel.transform, "PAUSE", new Vector2(700f, 405f), new Vector2(132f, 42f), TogglePause);
        hudPanel.SetActive(false);
    }

    private void CreateDrivingControls()
    {
        GameObject pad = CreatePanel("Driving controls // keyboard or hold buttons", hudPanel.transform,
            new Vector2(300f, 150f), new Vector2(580f, -300f), new Color(0.02f, 0.04f, 0.05f, 0.36f));
        CreateHoldButton(pad.transform, "◀", new Vector2(-88f, 25f), new Vector2(74f, 68f),
            OffRoadVehicleControl.SteerLeft);
        CreateHoldButton(pad.transform, "▶", new Vector2(-8f, 25f), new Vector2(74f, 68f),
            OffRoadVehicleControl.SteerRight);
        CreateHoldButton(pad.transform, "BRAKE\nS", new Vector2(75f, 25f), new Vector2(80f, 68f),
            OffRoadVehicleControl.Brake);
        CreateHoldButton(pad.transform, "GAS\nW", new Vector2(166f, 25f), new Vector2(80f, 68f),
            OffRoadVehicleControl.Accelerate, true);
        CreateHoldButton(pad.transform, "HANDBRAKE  //  SPACE", new Vector2(70f, -43f), new Vector2(255f, 34f),
            OffRoadVehicleControl.Handbrake);
    }

    private void CreateHoldButton(Transform parent, string label, Vector2 position, Vector2 size,
        OffRoadVehicleControl control, bool highlighted = false)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = buttonObject.AddComponent<Image>();
        image.color = highlighted ? new Color(0.83f, 0.37f, 0.1f, 0.88f) : new Color(0.13f, 0.19f, 0.2f, 0.82f);

        Button button = buttonObject.AddComponent<Button>();
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        button.transition = Selectable.Transition.None;
        OffRoadHoldControl holdControl = buttonObject.AddComponent<OffRoadHoldControl>();
        holdControl.Initialize(player, control);
        AddText(buttonObject.transform, label, 15, Vector2.zero, size, Color.white, FontStyle.Bold);
    }

    private void BuildPauseMenu()
    {
        pausePanel = CreatePanel("Pause overlay", canvas.transform, new Vector2(470f, 390f),
            Vector2.zero, new Color(0.02f, 0.035f, 0.045f, 0.94f));
        AddText(pausePanel.transform, "PAUSED", 38, new Vector2(0f, 125f), new Vector2(400f, 58f),
            Color.white, FontStyle.Bold);
        AddButton(pausePanel.transform, "RESUME  //  ESC", new Vector2(0f, 55f), new Vector2(350f, 52f),
            TogglePause, true);
        AddButton(pausePanel.transform, "RESTART STAGE", new Vector2(0f, -15f), new Vector2(350f, 48f),
            RestartStage);
        AddButton(pausePanel.transform, "MAIN MENU", new Vector2(0f, -80f), new Vector2(350f, 48f),
            ReturnToMenu);
        pausePanel.SetActive(false);
    }

    private void BuildResultsPanel()
    {
        resultsPanel = CreatePanel("Race results", canvas.transform, new Vector2(600f, 500f),
            Vector2.zero, new Color(0.02f, 0.035f, 0.045f, 0.97f));
        CreatePanel("Results accent", resultsPanel.transform, new Vector2(5f, 420f),
            new Vector2(-270f, 0f), new Color(1f, 0.48f, 0.14f, 0.95f));
        resultsTitle = AddText(resultsPanel.transform, "YOU WIN!", 42,
            new Vector2(0f, 175f), new Vector2(500f, 64f),
            new Color(1f, 0.7f, 0.24f), FontStyle.Bold);
        resultsSubtitle = AddText(resultsPanel.transform, "Race Completed!", 18,
            new Vector2(0f, 140f), new Vector2(500f, 30f),
            new Color(0.78f, 0.86f, 0.82f), FontStyle.Bold);
        resultsTime = AddText(resultsPanel.transform, "TOTAL TIME  //  00:00.00", 18,
            new Vector2(0f, 100f), new Vector2(500f, 32f), Color.white, FontStyle.Bold);
        resultsPosition = AddText(resultsPanel.transform, "FINAL POSITION  //  1 / 5", 18,
            new Vector2(0f, 62f), new Vector2(500f, 32f), Color.white, FontStyle.Bold);
        resultsScore = AddText(resultsPanel.transform, "SCORE  //  1000", 18,
            new Vector2(0f, 24f), new Vector2(500f, 32f), new Color(1f, 0.7f, 0.24f), FontStyle.Bold);
        AddButton(resultsPanel.transform, "RESTART RACE  //  RETRY", new Vector2(0f, -40f),
            new Vector2(390f, 48f), RestartStage, true);
        AddButton(resultsPanel.transform, "MAIN MENU", new Vector2(0f, -100f),
            new Vector2(390f, 44f), ReturnToMenu);
        AddButton(resultsPanel.transform, "EXIT GAME", new Vector2(0f, -156f),
            new Vector2(390f, 42f), ExitGame);
        resultsPanel.SetActive(false);
    }

    private void CreateMinimap()
    {
        GameObject map = CreatePanel("Route map", hudPanel.transform, new Vector2(180f, 180f),
            new Vector2(680f, 250f), new Color(0.03f, 0.075f, 0.08f, 0.82f));
        AddText(map.transform, "N", 12, new Vector2(0f, 68f), new Vector2(30f, 25f),
            new Color(0.74f, 0.82f, 0.75f), FontStyle.Bold);
        for (int i = 0; i < world.RoutePoints.Length; i++)
        {
            Vector3 point = world.RoutePoints[i];
            Image dot = CreateImage(map.transform, "Route checkpoint", new Color(1f, 0.55f, 0.17f, 0.85f),
                new Vector2(point.x / 70f * 54f, point.z / 55f * 48f), new Vector2(7f, 7f));
            dot.raycastTarget = false;
        }
        minimapCar = CreateImage(map.transform, "Player marker", Color.white, Vector2.zero,
            new Vector2(12f, 12f)).rectTransform;
        AddText(map.transform, "ROUTE", 11, new Vector2(0f, -76f), new Vector2(130f, 20f),
            new Color(0.74f, 0.82f, 0.75f), FontStyle.Bold);
    }

    private void UpdateMinimap()
    {
        Vector3 p = player.transform.position;
        minimapCar.anchoredPosition = new Vector2(Mathf.Clamp(p.x / 70f, -1.08f, 1.08f) * 54f,
            Mathf.Clamp(p.z / 55f, -1.08f, 1.08f) * 48f);
        minimapCar.localRotation = Quaternion.Euler(0f, 0f, -player.transform.eulerAngles.y);
    }

    private void StartSelectedGame()
    {
        resultsPanel.SetActive(false);
        menuPanel.SetActive(false);
        menuBackdrop.SetActive(false);
        hudPanel.SetActive(true);
        race.StartGame(selectedMode, difficulty);
        Time.timeScale = 1f;
        isPaused = false;
        pausePanel.SetActive(false);
    }

    private void CycleMode()
    {
        selectedMode = (OffRoadGameMode)(((int)selectedMode + 1) % 4);
        RefreshSelections();
    }

    private void CycleVehicle()
    {
        vehicleChoiceIndex = (vehicleChoiceIndex + 1) % 2;
        player.SetStyle(vehicleChoiceIndex == 1);
        RefreshSelections();
    }

    private void CycleDifficulty()
    {
        difficulty = (OffRoadDifficulty)(((int)difficulty + 1) % 3);
        race.SetDifficulty(difficulty);
        RefreshSelections();
    }

    private void RefreshSelections()
    {
        if (modeChoice == null)
            return;
        modeChoice.text = "SELECTED EVENT  //  " + selectedMode.ToString().ToUpperInvariant().Replace("OFFROAD", "OFF-ROAD");
        vehicleChoice.text = "VEHICLE  //  " + (vehicleChoiceIndex == 0 ? "TRAIL SUV" : "RALLY BUGGY");
        trackChoice.text = "TRACK  //  HIGHLAND CIRCUIT";
        difficultyChoice.text = "AI DIFFICULTY  //  " + difficulty.ToString().ToUpperInvariant();
        graphicsChoice.text = "GRAPHICS QUALITY  //  " +
            QualitySettings.names[QualitySettings.GetQualityLevel()].ToUpperInvariant();
    }

    private void TogglePause()
    {
        if (!hudPanel.activeSelf || race.IsFinished)
            return;
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        pausePanel.SetActive(isPaused);
    }

    private void RestartStage()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0 &&
            activeScene.buildIndex < SceneManager.sceneCountInBuildSettings)
        {
            OffRoadGameBootstrap.PrepareRaceRestart(selectedMode, difficulty);
            Time.timeScale = 1f;
            SceneManager.LoadScene(activeScene.buildIndex);
            return;
        }

        resultsPanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        pausePanel.SetActive(false);
        hudPanel.SetActive(true);
        race.StartGame(selectedMode, difficulty);
    }

    private void ReturnToMenu()
    {
        resultsPanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        pausePanel.SetActive(false);
        hudPanel.SetActive(false);
        menuPanel.SetActive(true);
        menuBackdrop.SetActive(true);
        race.StartGame(OffRoadGameMode.FreeRoam, difficulty);
        Time.timeScale = 0f;
    }

    private void ShowSettings()
    {
        int qualityCount = QualitySettings.names.Length;
        int nextQuality = (QualitySettings.GetQualityLevel() + 1) % qualityCount;
        QualitySettings.SetQualityLevel(nextQuality, true);
        PlayerPrefs.SetInt("offroad_quality", nextQuality);
        PlayerPrefs.Save();
        graphicsChoice.text = "GRAPHICS QUALITY  //  " + QualitySettings.names[nextQuality].ToUpperInvariant();
    }

    private static void ExitGame()
    {
        Application.Quit();
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private Image CreateFullscreenOverlay(Transform parent, Color color)
    {
        GameObject overlay = new GameObject("Menu backdrop");
        overlay.transform.SetParent(parent, false);
        RectTransform rect = overlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = overlay.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text AddText(Transform parent, string content, int fontSize, Vector2 position, Vector2 size,
        Color color, FontStyle style = FontStyle.Normal)
    {
        GameObject textObject = new GameObject("Text // " + content);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }

    private Button AddButton(Transform parent, string label, Vector2 position, Vector2 size,
        UnityEngine.Events.UnityAction action, bool highlighted = false)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = buttonObject.AddComponent<Image>();
        image.color = highlighted ? new Color(0.83f, 0.37f, 0.1f, 0.96f) : new Color(0.13f, 0.19f, 0.2f, 0.96f);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(1f, 0.58f, 0.22f, 1f);
        colors.pressedColor = new Color(0.72f, 0.27f, 0.08f, 1f);
        button.colors = colors;
        button.onClick.AddListener(action);
        button.onClick.AddListener(OffRoadAudioManager.PlayUiClick);
        AddText(buttonObject.transform, label, 15, Vector2.zero, size, Color.white, FontStyle.Bold);
        return button;
    }

    private Image CreateImage(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }
}
