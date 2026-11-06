using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class OffRoadGameBootstrap : MonoBehaviour
{
    private static bool booted;
    private static bool sceneReloadHandlerRegistered;
    private static bool restartRaceAfterSceneLoad;
    private static OffRoadGameMode restartMode;
    private static OffRoadDifficulty restartDifficulty;

    public static void PrepareRaceRestart(OffRoadGameMode mode, OffRoadDifficulty difficulty)
    {
        restartMode = mode;
        restartDifficulty = difficulty;
        restartRaceAfterSceneLoad = true;
    }

    public static bool ConsumeRaceRestart(out OffRoadGameMode mode, out OffRoadDifficulty difficulty)
    {
        mode = restartMode;
        difficulty = restartDifficulty;
        bool shouldRestart = restartRaceAfterSceneLoad;
        restartRaceAfterSceneLoad = false;
        return shouldRestart;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBootState()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        booted = false;
        sceneReloadHandlerRegistered = false;
        restartRaceAfterSceneLoad = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateGame()
    {
        if (!sceneReloadHandlerRegistered)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneReloadHandlerRegistered = true;
        }

        if (booted)
            return;

        booted = true;
        new GameObject("OFFROAD // Game").AddComponent<OffRoadGameBootstrap>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        booted = false;
        CreateGame();
    }

    private void Start()
    {
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 0;
        ConfigureEnvironment();
        new GameObject("OFFROAD // Audio").AddComponent<OffRoadAudioManager>();

        OffRoadWorldBuilder world = new GameObject("Procedural World").AddComponent<OffRoadWorldBuilder>();
        world.BuildWorld();

        OffRoadVehicleController player = OffRoadVehicleController.Create(
            "PLAYER // Trail SUV", world.RoutePoints[0], world.RoutePoints[1], new Color(0.82f, 0.23f, 0.09f), false);

        OffRoadVehicleController[] rivals = new OffRoadVehicleController[4];
        Color[] rivalColors =
        {
            new Color(0.09f, 0.32f, 0.65f),
            new Color(0.23f, 0.48f, 0.27f),
            new Color(0.68f, 0.55f, 0.15f),
            new Color(0.48f, 0.20f, 0.57f)
        };
        Vector3 routeDirection = world.RoutePoints[1] - world.RoutePoints[0];
        routeDirection.y = 0f;
        routeDirection.Normalize();
        Vector3 laneDirection = Vector3.Cross(Vector3.up, routeDirection).normalized;
        for (int i = 0; i < rivals.Length; i++)
        {
            int row = i / 2;
            float laneOffset = i % 2 == 0 ? -2.3f : 2.3f;
            Vector3 startPosition = world.RoutePoints[0] - routeDirection * (4f + row * 4f)
                + laneDirection * laneOffset;
            rivals[i] = OffRoadVehicleController.Create(
                "RIVAL " + (i + 1).ToString("00") + " // Trail SUV",
                startPosition, startPosition + routeDirection, rivalColors[i], true);
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        OffRoadFollowCamera followCamera = camera.GetComponent<OffRoadFollowCamera>();
        if (followCamera == null)
            followCamera = camera.gameObject.AddComponent<OffRoadFollowCamera>();
        followCamera.SetTarget(player.transform);

        OffRoadRaceDirector raceDirector = new GameObject("Race Director").AddComponent<OffRoadRaceDirector>();
        raceDirector.Initialize(player, rivals, world.RoutePoints, world.Checkpoints);
        for (int i = 0; i < rivals.Length; i++)
            rivals[i].gameObject.AddComponent<OffRoadAIOpponent>().Initialize(
                rivals[i], world.RoutePoints, (i % 2 == 0 ? -2.3f : 2.3f), i - 1.5f);

        new GameObject("OFFROAD // Interface").AddComponent<OffRoadGameUI>().Initialize(
            player, raceDirector, followCamera, world);
    }

    private static void ConfigureEnvironment()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.49f, 0.59f, 0.71f);
        RenderSettings.ambientEquatorColor = new Color(0.43f, 0.42f, 0.36f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.25f, 0.18f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.58f, 0.66f, 0.70f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0018f;

        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            Material sky = new Material(skyShader);
            sky.SetFloat("_AtmosphereThickness", 1.15f);
            sky.SetFloat("_Exposure", 1.25f);
            sky.SetColor("_SkyTint", new Color(0.62f, 0.75f, 0.92f));
            sky.SetColor("_GroundColor", new Color(0.36f, 0.34f, 0.28f));
            RenderSettings.skybox = sky;
        }

        GameObject sunObject = new GameObject("Sun // late afternoon");
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.35f;
        sun.color = new Color(1f, 0.91f, 0.77f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.78f;
        sunObject.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
        RenderSettings.sun = sun;
    }
}
