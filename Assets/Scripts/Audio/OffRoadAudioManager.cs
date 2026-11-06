using UnityEngine;

public sealed class OffRoadAudioManager : MonoBehaviour
{
    private static OffRoadAudioManager instance;
    private static AudioClip uiClickClip;
    private static AudioClip countdownClip;
    private static AudioClip goClip;
    private static AudioClip checkpointClip;
    private static AudioClip lapClip;
    private static AudioClip finishClip;
    private static AudioClip failureClip;
    private static AudioClip impactClip;
    private static AudioClip skidClip;
    private static AudioClip roadClip;

    private AudioSource audioSource;

    public static AudioClip ImpactClip => impactClip;
    public static AudioClip SkidClip => skidClip;
    public static AudioClip RoadClip => roadClip;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
        CreateClips();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void PlayUiClick()
    {
        Play(uiClickClip);
    }

    public static void PlayCountdown(bool go)
    {
        Play(go ? goClip : countdownClip);
    }

    public static void PlayCheckpoint(bool completedLap)
    {
        Play(completedLap ? lapClip : checkpointClip);
    }

    public static void PlayFinish(bool success)
    {
        Play(success ? finishClip : failureClip);
    }

    public static void SmoothLoopEndpoints(float[] samples)
    {
        int fadeSamples = Mathf.Min(Mathf.CeilToInt(22050f * 0.008f), samples.Length / 2);
        for (int i = 0; i < fadeSamples; i++)
        {
            float t = i / (float)(fadeSamples - 1);
            float fadeIn = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t);
            float fadeOut = 0.5f + 0.5f * Mathf.Cos(Mathf.PI * t);
            samples[i] *= fadeIn;
            samples[samples.Length - fadeSamples + i] *= fadeOut;
        }
    }

    private static void Play(AudioClip clip)
    {
        if (instance != null && clip != null)
            instance.audioSource.PlayOneShot(clip);
    }

    private static void CreateClips()
    {
        if (uiClickClip != null)
            return;

        uiClickClip = CreateTone("UI click", 760f, 520f, 0.075f, 0.2f);
        countdownClip = CreateTone("Race countdown", 520f, 420f, 0.18f, 0.24f);
        goClip = CreateTone("Race start", 620f, 980f, 0.34f, 0.26f);
        checkpointClip = CreateTone("Checkpoint", 720f, 980f, 0.22f, 0.22f);
        lapClip = CreateTone("Lap complete", 680f, 1120f, 0.38f, 0.24f);
        finishClip = CreateSequence("Race finish", new[] { 660f, 830f, 990f }, 0.24f);
        failureClip = CreateTone("Challenge failed", 300f, 150f, 0.48f, 0.25f);
        impactClip = CreateImpactClip();
        skidClip = CreateSkidClip();
        roadClip = CreateRoadClip();
    }

    private static AudioClip CreateTone(string clipName, float startFrequency, float endFrequency,
        float duration, float volume)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
            phase += 2f * Mathf.PI * frequency / sampleRate;
            float attack = Mathf.Clamp01(i / (sampleRate * 0.012f));
            float release = Mathf.Clamp01((sampleCount - i) / (sampleRate * 0.045f));
            float envelope = Mathf.Min(attack, release);
            samples[i] = (Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.12f) * volume * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateSequence(string clipName, float[] frequencies, float noteDuration)
    {
        const int sampleRate = 22050;
        int noteSamples = Mathf.CeilToInt(sampleRate * noteDuration);
        int sampleCount = noteSamples * frequencies.Length;
        float[] samples = new float[sampleCount];
        for (int note = 0; note < frequencies.Length; note++)
        {
            for (int i = 0; i < noteSamples; i++)
            {
                float t = i / (float)noteSamples;
                float envelope = Mathf.Min(Mathf.Clamp01(i / (sampleRate * 0.012f)),
                    Mathf.Clamp01((noteSamples - i) / (sampleRate * 0.04f)));
                float phase = 2f * Mathf.PI * frequencies[note] * t;
                samples[note * noteSamples + i] = Mathf.Sin(phase) * 0.24f * envelope;
            }
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateImpactClip()
    {
        const int sampleRate = 22050;
        const int sampleCount = 6615;
        float[] samples = new float[sampleCount];
        uint noiseState = 8675309;
        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            phase += 2f * Mathf.PI * Mathf.Lerp(115f, 42f, t) / sampleRate;
            noiseState = unchecked(noiseState * 1664525u + 1013904223u);
            float noise = ((noiseState >> 8) / 8388607.5f) - 1f;
            float envelope = (1f - t) * (1f - t);
            samples[i] = (Mathf.Sin(phase) * 0.72f + noise * 0.28f) * envelope * 0.7f;
        }

        AudioClip clip = AudioClip.Create("Vehicle impact", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateSkidClip()
    {
        const int sampleRate = 22050;
        const int sampleCount = sampleRate;
        float[] samples = new float[sampleCount];
        uint noiseState = 38191;
        for (int i = 0; i < sampleCount; i++)
        {
            noiseState = unchecked(noiseState * 1664525u + 1013904223u);
            float noise = ((noiseState >> 8) / 8388607.5f) - 1f;
            float t = i / (float)sampleRate;
            float rumble = Mathf.Sin(2f * Mathf.PI * 145f * t) * 0.16f;
            samples[i] = (noise * 0.18f + rumble) * 0.34f;
        }

        SmoothLoopEndpoints(samples);
        AudioClip clip = AudioClip.Create("Tire skid", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateRoadClip()
    {
        const int sampleRate = 22050;
        const int sampleCount = sampleRate;
        float[] samples = new float[sampleCount];
        uint noiseState = 61453;
        float filteredNoise = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            noiseState = unchecked(noiseState * 1664525u + 1013904223u);
            float noise = ((noiseState >> 8) / 8388607.5f) - 1f;
            filteredNoise = Mathf.Lerp(filteredNoise, noise, 0.035f);
            float t = i / (float)sampleRate;
            float rolling = Mathf.Sin(2f * Mathf.PI * 48f * t) * 0.32f
                + Mathf.Sin(2f * Mathf.PI * 73f * t) * 0.22f
                + Mathf.Sin(2f * Mathf.PI * 97f * t) * 0.16f
                + Mathf.Sin(2f * Mathf.PI * 137f * t) * 0.11f
                + Mathf.Sin(2f * Mathf.PI * 201f * t) * 0.07f;
            samples[i] = rolling + filteredNoise * 0.12f;
        }

        SmoothLoopEndpoints(samples);
        AudioClip clip = AudioClip.Create("Tires rolling on dirt", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
