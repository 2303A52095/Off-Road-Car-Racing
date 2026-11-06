using System.Collections;
using UnityEngine;

public enum OffRoadGameMode
{
    FreeRoam,
    OffRoadRace,
    TimeTrial,
    CheckpointChallenge
}

public enum OffRoadDifficulty
{
    Easy,
    Normal,
    Hard
}

public sealed class OffRoadRaceCheckpoint : MonoBehaviour
{
    public int Index { get; private set; }
    private ParticleSystem[] finishFireworks;
    private Transform finishFlag;
    private bool celebratingFinish;

    public void Initialize(int index, ParticleSystem[] fireworks = null, Transform animatedFlag = null)
    {
        Index = index;
        finishFireworks = fireworks;
        finishFlag = animatedFlag;
    }

    private void Update()
    {
        if (celebratingFinish && finishFlag != null)
            finishFlag.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 8f) * 12f);
    }

    public void PlayFinishCelebration()
    {
        celebratingFinish = true;
        if (finishFireworks == null)
            return;

        for (int i = 0; i < finishFireworks.Length; i++)
            finishFireworks[i].Play();
    }

    private void OnTriggerEnter(Collider other)
    {
        OffRoadVehicleController vehicle = other.GetComponentInParent<OffRoadVehicleController>();
        if (vehicle != null && (Index != 0 || vehicle.IsAIControlled))
            OffRoadRaceDirector.Instance?.OnCheckpoint(vehicle, Index);
    }
}

public sealed class OffRoadRaceDirector : MonoBehaviour
{
    private const float RaceTimeLimitSeconds = 75f;

    public static OffRoadRaceDirector Instance { get; private set; }

    public OffRoadGameMode Mode { get; private set; }
    public OffRoadDifficulty Difficulty { get; private set; }
    public float ElapsedTime { get; private set; }
    public int CurrentCheckpoint => nextCheckpoint;
    public int CheckpointCount => checkpoints == null ? 0 : checkpoints.Length;
    public int RacerCount => opponents == null ? 1 : opponents.Length + 1;
    public int CurrentLap => lap;
    public int TotalLaps => totalLaps;
    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    public bool PlayerWon { get; private set; }
    public int FinalPosition { get; private set; }
    public int FinalScore { get; private set; }
    public string StatusMessage { get; private set; }

    private OffRoadVehicleController player;
    private OffRoadVehicleController[] opponents;
    private Vector3[] route;
    private OffRoadRaceCheckpoint[] checkpoints;
    private int nextCheckpoint;
    private int lap = 1;
    private int totalLaps = 1;
    private float challengeTime = 75f;
    private int[] opponentNextCheckpoint;
    private int[] opponentLap;
    private float[] opponentFinishTime;

    public void Initialize(OffRoadVehicleController playerVehicle, OffRoadVehicleController[] rivalVehicles,
        Vector3[] routePoints,
        OffRoadRaceCheckpoint[] raceCheckpoints)
    {
        Instance = this;
        player = playerVehicle;
        opponents = rivalVehicles;
        route = routePoints;
        checkpoints = raceCheckpoints;
        opponentNextCheckpoint = new int[opponents.Length];
        opponentLap = new int[opponents.Length];
        opponentFinishTime = new float[opponents.Length];
        StatusMessage = "Choose a mode to begin";
    }

    private void Update()
    {
        if (!IsRunning || IsFinished || Time.timeScale == 0f)
            return;

        ElapsedTime += Time.deltaTime;
        if (Mode == OffRoadGameMode.CheckpointChallenge)
        {
            challengeTime -= Time.deltaTime;
            if (challengeTime <= 0f)
            {
                CompleteRace(false, "TIME UP // CHALLENGE FAILED");
                return;
            }
        }

        if (Mode != OffRoadGameMode.FreeRoam && IsPlayerInsideFinishGate())
        {
            OnFinishLine(player);
            if (IsFinished)
                return;
        }

        if (Mode != OffRoadGameMode.FreeRoam && ElapsedTime >= RaceTimeLimitSeconds)
            CompleteRace(false, "TIME UP // RACE FAILED");
    }

    public void StartGame(OffRoadGameMode mode, OffRoadDifficulty difficulty)
    {
        StopAllCoroutines();
        Mode = mode;
        Difficulty = difficulty;
        ElapsedTime = 0f;
        IsRunning = false;
        IsFinished = false;
        PlayerWon = false;
        FinalPosition = 0;
        FinalScore = 0;
        lap = 1;
        nextCheckpoint = 1;
        totalLaps = 1;
        challengeTime = 75f;
        player.ResetVehicle();
        for (int i = 0; i < opponents.Length; i++)
        {
            opponentNextCheckpoint[i] = 1;
            opponentLap[i] = 1;
            opponentFinishTime[i] = -1f;
            opponents[i].ResetVehicle();
        }
        SetCheckpointVisibility(mode != OffRoadGameMode.FreeRoam);
        if (mode == OffRoadGameMode.FreeRoam)
        {
            IsRunning = true;
            StatusMessage = "FREE ROAM // EXPLORE THE WILDS";
        }
        else
        {
            StatusMessage = "GET READY";
            StartCoroutine(Countdown());
        }
    }

    public void OnCheckpoint(OffRoadVehicleController vehicle, int index)
    {
        if (!IsRunning || IsFinished || Mode == OffRoadGameMode.FreeRoam)
            return;

        if (index == 0)
            return;

        if (vehicle != player)
        {
            int opponentIndex = System.Array.IndexOf(opponents, vehicle);
            if (opponentIndex >= 0)
                OnOpponentCheckpoint(opponentIndex, index);
            return;
        }

        if (index != nextCheckpoint)
            return;

        if (index == checkpoints.Length - 1)
        {
            CompleteRace(true, "FINISH // " + FormatTime(ElapsedTime));
            return;
        }

        nextCheckpoint = (index + 1) % checkpoints.Length;
        if (Mode == OffRoadGameMode.CheckpointChallenge)
            challengeTime += 6f;
        StatusMessage = "CHECKPOINT " + index.ToString("00") + " // +" +
            (Mode == OffRoadGameMode.CheckpointChallenge ? "6 SEC" : "SPLIT");
        OffRoadAudioManager.PlayCheckpoint(false);
    }

    public void OnFinishLine(OffRoadVehicleController vehicle)
    {
        if (!IsRunning || IsFinished || Mode == OffRoadGameMode.FreeRoam)
            return;

        if (vehicle != player)
        {
            int opponentIndex = System.Array.IndexOf(opponents, vehicle);
            int finishIndex = checkpoints.Length - 1;
            if (opponentIndex >= 0 && opponentNextCheckpoint[opponentIndex] == finishIndex)
                OnOpponentCheckpoint(opponentIndex, finishIndex);
            return;
        }

        if (nextCheckpoint != checkpoints.Length - 1)
            return;

        CompleteRace(true, "FINISH // " + FormatTime(ElapsedTime));
    }

    private bool IsPlayerInsideFinishGate()
    {
        if (player == null || checkpoints == null || checkpoints.Length == 0 ||
            nextCheckpoint != checkpoints.Length - 1)
            return false;

        OffRoadRaceCheckpoint finish = checkpoints[checkpoints.Length - 1];
        BoxCollider trigger = finish.GetComponent<BoxCollider>();
        if (trigger == null || !trigger.enabled || !finish.gameObject.activeInHierarchy)
            return false;

        Vector3 localPosition = finish.transform.InverseTransformPoint(player.transform.position) -
            trigger.center;
        Vector3 halfSize = trigger.size * 0.5f;
        const float carClearance = 2f;
        return Mathf.Abs(localPosition.x) <= halfSize.x + carClearance &&
            Mathf.Abs(localPosition.y) <= halfSize.y + carClearance &&
            Mathf.Abs(localPosition.z) <= halfSize.z + carClearance;
    }

    private void OnOpponentCheckpoint(int opponentIndex, int index)
    {
        if (opponentFinishTime[opponentIndex] >= 0f || index != opponentNextCheckpoint[opponentIndex])
            return;

        if (index == checkpoints.Length - 1)
        {
            opponentFinishTime[opponentIndex] = ElapsedTime;
            opponents[opponentIndex].StopForRaceEnd();
        }
        else
        {
            opponentNextCheckpoint[opponentIndex] = (index + 1) % checkpoints.Length;
        }
    }

    private void CompleteRace(bool won, string message)
    {
        if (IsFinished)
            return;

        IsRunning = false;
        IsFinished = true;
        PlayerWon = won;
        StatusMessage = message;
        FinalPosition = GetPlayerPosition();
        if (won)
            FinalScore = Mathf.Max(0, 1000 - (FinalPosition - 1) * 150
                - Mathf.FloorToInt(ElapsedTime * 2f));

        player.StopForRaceEnd();
        for (int i = 0; i < opponents.Length; i++)
            opponents[i].StopForRaceEnd();
        if (won && checkpoints.Length > 0)
            checkpoints[checkpoints.Length - 1].PlayFinishCelebration();
        OffRoadAudioManager.PlayFinish(won);
    }

    public string GetCheckpointLabel()
    {
        if (Mode == OffRoadGameMode.FreeRoam)
            return "ROAM";
        if (IsFinished)
            return "FINISH";
        return nextCheckpoint.ToString("00") + " / " + (checkpoints.Length - 1).ToString("00");
    }

    public string GetTimerLabel()
    {
        if (Mode == OffRoadGameMode.CheckpointChallenge && IsRunning)
            return FormatTime(challengeTime);
        if (Mode != OffRoadGameMode.FreeRoam && IsRunning)
            return FormatTime(Mathf.Max(0f, RaceTimeLimitSeconds - ElapsedTime));
        return FormatTime(ElapsedTime);
    }

    public string GetTimerHeading()
    {
        if (Mode == OffRoadGameMode.CheckpointChallenge)
            return "TIME LEFT";
        return Mode == OffRoadGameMode.FreeRoam ? "DRIVE TIME" : "1:15 RACE // TIME LEFT";
    }

    public static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainder = Mathf.Max(0f, seconds - minutes * 60f);
        return minutes.ToString("00") + ":" + remainder.ToString("00.00");
    }

    public void SetDifficulty(OffRoadDifficulty difficulty)
    {
        Difficulty = difficulty;
    }

    public float GetOpponentProgress(int index)
    {
        if (opponentFinishTime[index] >= 0f)
            return totalLaps * checkpoints.Length;
        int passedCheckpoints = opponentNextCheckpoint[index] == 0
            ? checkpoints.Length - 1
            : opponentNextCheckpoint[index] - 1;
        return (opponentLap[index] - 1) * checkpoints.Length + passedCheckpoints;
    }

    public int GetPlayerPosition()
    {
        float playerProgress = (IsFinished && PlayerWon) || lap > totalLaps
            ? totalLaps * checkpoints.Length
            : (lap - 1) * checkpoints.Length +
                (nextCheckpoint == 0 ? checkpoints.Length - 1 : nextCheckpoint - 1);
        int position = 1;
        for (int i = 0; i < opponents.Length; i++)
        {
            if (opponentFinishTime[i] >= 0f && opponentFinishTime[i] <= ElapsedTime)
                position++;
            else if (GetOpponentProgress(i) > playerProgress)
                position++;
        }
        return position;
    }

    private IEnumerator Countdown()
    {
        string[] labels = { "3", "2", "1", "GO!" };
        for (int i = 0; i < labels.Length; i++)
        {
            StatusMessage = labels[i];
            OffRoadAudioManager.PlayCountdown(i == labels.Length - 1);
            yield return new WaitForSeconds(i == labels.Length - 1 ? 0.7f : 1f);
        }
        StatusMessage = "RACE // GO!";
        IsRunning = true;
    }

    private void SetCheckpointVisibility(bool visible)
    {
        for (int i = 0; i < checkpoints.Length; i++)
        {
            checkpoints[i].gameObject.SetActive(visible);
            Renderer[] renderers = checkpoints[i].GetComponentsInChildren<Renderer>();
            for (int j = 0; j < renderers.Length; j++)
                renderers[j].enabled = visible && (i == nextCheckpoint || i == 0 ||
                    i == checkpoints.Length - 1);
        }
    }
}

public sealed class OffRoadAIOpponent : MonoBehaviour
{
    private OffRoadVehicleController vehicle;
    private Vector3[] route;
    private float laneOffset;
    private float paceOffset;
    private int waypoint = 1;

    public void Initialize(OffRoadVehicleController controller, Vector3[] routePoints,
        float targetLaneOffset, float targetPaceOffset)
    {
        vehicle = controller;
        route = routePoints;
        laneOffset = targetLaneOffset;
        paceOffset = targetPaceOffset;
    }

    private void Update()
    {
        if (vehicle == null || route == null)
            return;
        if (OffRoadRaceDirector.Instance == null || !OffRoadRaceDirector.Instance.IsRunning ||
            OffRoadRaceDirector.Instance.Mode == OffRoadGameMode.FreeRoam)
        {
            waypoint = 1;
            vehicle.SetInput(0f, 0f, 1f, false);
            return;
        }

        Vector3 target = GetLaneTarget(waypoint);
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        float advanceDistance = Mathf.Lerp(9f, 14f,
            Mathf.Clamp01(vehicle.SpeedKmh / 110f));
        if (toTarget.sqrMagnitude < advanceDistance * advanceDistance)
            waypoint = (waypoint + 1) % route.Length;

        target = GetLaneTarget(waypoint);
        Vector3 localTarget = transform.InverseTransformPoint(target);
        float targetAngle = Mathf.Atan2(localTarget.x, Mathf.Max(1f, localTarget.z)) * Mathf.Rad2Deg;
        float steering = Mathf.Clamp(targetAngle / 32f, -1f, 1f);

        OffRoadDifficulty difficulty = OffRoadRaceDirector.Instance.Difficulty;
        float targetSpeed = difficulty == OffRoadDifficulty.Easy ? 72f :
            difficulty == OffRoadDifficulty.Hard ? 104f : 88f;
        Vector3 incoming = route[waypoint] - route[(waypoint - 1 + route.Length) % route.Length];
        Vector3 outgoing = route[(waypoint + 1) % route.Length] - route[waypoint];
        incoming.y = 0f;
        outgoing.y = 0f;
        float corner = 1f - Vector3.Dot(incoming.normalized, outgoing.normalized);
        targetSpeed *= Mathf.Lerp(1f, 0.62f, Mathf.Clamp01(corner));
        targetSpeed += paceOffset * 2f;

        float speedError = targetSpeed - vehicle.SpeedKmh;
        float throttle = Mathf.Clamp01(speedError / 28f);
        float brake = Mathf.Clamp01(-speedError / 22f);
        vehicle.SetInput(throttle, steering, brake, false);
    }

    private Vector3 GetLaneTarget(int routeIndex)
    {
        Vector3 previous = route[(routeIndex - 1 + route.Length) % route.Length];
        Vector3 next = route[(routeIndex + 1) % route.Length];
        Vector3 direction = next - previous;
        direction.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, direction.normalized);
        return route[routeIndex] + side * laneOffset;
    }
}
