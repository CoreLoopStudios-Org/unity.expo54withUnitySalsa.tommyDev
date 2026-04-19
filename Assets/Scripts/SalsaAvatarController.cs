using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using CrazyMinnow.SALSA; // SALSA LipSync Suite namespace

/// <summary>
/// Central bridge between React Native and the Unity avatar.
/// Uses SALSA LipSync Suite for lip sync, expressions, and idle animations.
///
/// REPLACES: AvatarController.cs + AudioLipSync.cs + IdleAnimator.cs + VisemeReceiver.cs
///
/// Architecture:
///   - SALSA handles lip sync (real-time audio analysis)
///   - Eyes handles blink, saccades, head tracking
///   - EmoteR handles expressions/emotions
///   - This script handles: RN bridge, audio loading, body animations
///
/// Setup in Inspector:
///   1. Drag avatar root into "Avatar Root"
///   2. SALSA, Eyes, EmoteR are set up via Inspector on the avatar (see migration guide)
///   3. This script just forwards audio to SALSA's AudioSource
///
/// Attach to an empty GameObject named "AvatarController" in the scene.
/// </summary>
public class SalsaAvatarController : MonoBehaviour
{
    [Header("Avatar Reference")]
    [Tooltip("Root transform of the RPM avatar")]
    public Transform avatarRoot;

    [Header("SALSA References (auto-discovered)")]
    [SerializeField] private Salsa salsa;
    [SerializeField] private Eyes eyes;
    [SerializeField] private Emoter emoter;

    [Header("Body Animation")]
    [SerializeField] private AvatarAnimations avatarAnimations;

    // Audio
    private AudioSource _audioSource;
    private bool _isPlaying;

    // ── Native bridge (iOS) ──────────────────────────────────────────────
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendMessageToMobileApp(string message);
#endif

    private bool _isReady;

    // ── Lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        if (avatarRoot == null)
        {
            var smr = FindFirstObjectByType<SkinnedMeshRenderer>();
            if (smr != null)
                avatarRoot = smr.transform.root;
        }

        if (avatarRoot == null)
        {
            Debug.LogError("[SalsaAvatarController] No avatar root found!");
            return;
        }

        // Auto-discover SALSA components on the avatar
        salsa = avatarRoot.GetComponentInChildren<Salsa>();
        eyes = avatarRoot.GetComponentInChildren<Eyes>();
        emoter = avatarRoot.GetComponentInChildren<Emoter>();

        // Get SALSA's AudioSource (SALSA creates/manages its own)
        if (salsa != null)
        {
            _audioSource = salsa.audioSrc;
            Debug.Log($"[SalsaAvatarController] SALSA found — AudioSource ready");
        }
        else
        {
            Debug.LogWarning("[SalsaAvatarController] SALSA not found on avatar — add it in Inspector");
            // Fallback: create our own AudioSource
            _audioSource = avatarRoot.GetComponentInChildren<AudioSource>();
            if (_audioSource == null)
                _audioSource = avatarRoot.gameObject.AddComponent<AudioSource>();
        }

        if (eyes != null)
            Debug.Log("[SalsaAvatarController] Eyes module found — blink/saccades active");
        if (emoter != null)
            Debug.Log("[SalsaAvatarController] EmoteR module found — expressions active");

        // Body animations (procedural wave/nod/talk)
        avatarAnimations = avatarRoot.GetComponentInChildren<AvatarAnimations>();
        if (avatarAnimations == null)
            avatarAnimations = avatarRoot.gameObject.AddComponent<AvatarAnimations>();

        _isReady = true;
        SendToReactNative("READY", "");
        Debug.Log("[SalsaAvatarController] Ready — all systems initialized");
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            StopAudio();
            Debug.Log("[SalsaAvatarController] App paused — stopped playback");
        }
        else if (_isReady)
        {
            SendToReactNative("READY", "resumed");
        }
    }

    // ── Message handling ─────────────────────────────────────────────────

    public void ReceiveMessage(string jsonMessage)
    {
        try
        {
            var msg = JsonUtility.FromJson<BridgeMessage>(jsonMessage);
            if (msg == null) return;

            switch (msg.type)
            {
                case "PING":
                    SendToReactNative("PONG", "");
                    break;

                case "ANIM":
                    HandleAnimation(msg.payload);
                    break;

                case "PLAY_AUDIO":
                    HandlePlayAudio(msg.payload);
                    break;

                case "STOP_AUDIO":
                    StopAudio();
                    break;

                case "TEST_LIPSYNC":
                    HandleTestLipSync();
                    break;

                case "EMOTION":
                    HandleEmotion(msg.payload);
                    break;

                case "SET_IDLE":
                    HandleSetIdle(msg.payload);
                    break;

                case "TEST":
                    SendToReactNative("TEST_ACK", msg.payload);
                    break;

                default:
                    Debug.LogWarning($"[SalsaAvatarController] Unknown: {msg.type}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SalsaAvatarController] Error: {e.Message}\nJSON: {jsonMessage}");
        }
    }

    // ── Handlers ─────────────────────────────────────────────────────────

    private void HandleAnimation(string animName)
    {
        if (avatarAnimations == null) return;

        switch (animName.ToLower())
        {
            case "idle": avatarAnimations.PlayIdle(); break;
            case "wave": avatarAnimations.PlayWave(); break;
            case "talk": avatarAnimations.PlayTalk(); break;
            case "nod": avatarAnimations.PlayNod(); break;
            default: avatarAnimations.PlayByName(animName); break;
        }

        SendToReactNative("ANIM_STARTED", animName);
    }

    private void HandlePlayAudio(string filePath)
    {
        StopAudio();
        StartCoroutine(LoadAndPlayAudio(filePath));
    }

    /// <summary>
    /// Plays an AudioClip directly (used by Mock Test or local scripts).
    /// </summary>
    public void PlayAudioClip(AudioClip clip)
    {
        if (clip == null) return;
        StopAudio();

        _audioSource.clip = clip;
        _audioSource.Play();
        _isPlaying = true;

        Debug.Log($"[SalsaAvatarController] Playing clip: {clip.name} ({clip.length:F1}s)");
        SendToReactNative("AUDIO_STARTED", clip.length.ToString("F2"));

        StartCoroutine(WaitForAudioEnd());
    }

    private void HandleTestLipSync()
    {
        StopAudio();

        // Generate a simple test tone that SALSA can analyze
        // SALSA works on amplitude, so a vowel-like waveform works great
        var clip = GenerateTestAudio(3f);
        _audioSource.clip = clip;
        _audioSource.Play();
        _isPlaying = true;

        Debug.Log("[SalsaAvatarController] Playing test audio — SALSA will lip sync automatically");
        SendToReactNative("AUDIO_STARTED", "test");

        StartCoroutine(WaitForAudioEnd());
    }

    private void HandleEmotion(string payload)
    {
        // EmoteR handles emotions via its own system
        // For now, log it — EmoteR expressions are configured in the Inspector
        Debug.Log($"[SalsaAvatarController] Emotion: {payload}");

        // If EmoteR is configured with manual triggers, you can call:
        // emoter?.ManualEmote(emoteIndex, percentage);
    }

    private void HandleSetIdle(string payload)
    {
        bool enable = payload != "0" && payload.ToLower() != "false";

        // Toggle Eyes module (blink, saccades, head tracking)
        if (eyes != null)
            eyes.enabled = enable;

        Debug.Log($"[SalsaAvatarController] Idle: {(enable ? "ON" : "OFF")}");
    }

    // ── Audio loading ────────────────────────────────────────────────────

    private IEnumerator LoadAndPlayAudio(string filePath)
    {
        AudioType audioType = AudioType.WAV;
        string lower = filePath.ToLower();
        if (lower.EndsWith(".mp3")) audioType = AudioType.MPEG;
        else if (lower.EndsWith(".ogg")) audioType = AudioType.OGGVORBIS;

        string url = filePath.StartsWith("file://") ? filePath : "file://" + filePath;

        using (var request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SalsaAvatarController] Audio load failed: {request.error}");
                SendToReactNative("AUDIO_ERROR", request.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null || clip.length < 0.01f)
            {
                SendToReactNative("AUDIO_ERROR", "empty_clip");
                yield break;
            }

            // Just assign to SALSA's AudioSource and play — SALSA handles everything
            _audioSource.clip = clip;
            _audioSource.Play();
            _isPlaying = true;

            Debug.Log($"[SalsaAvatarController] Playing: {clip.length:F1}s — SALSA lip sync active");
            SendToReactNative("AUDIO_STARTED", clip.length.ToString("F2"));

            yield return new WaitWhile(() => _audioSource.isPlaying);

            _isPlaying = false;
            SendToReactNative("AUDIO_ENDED", "");
        }
    }

    private void StopAudio()
    {
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
        _isPlaying = false;
    }

    private IEnumerator WaitForAudioEnd()
    {
        yield return new WaitForSeconds(0.1f);
        while (_audioSource != null && _audioSource.isPlaying)
            yield return null;
        _isPlaying = false;
        SendToReactNative("AUDIO_ENDED", "");
    }

    // ── Test audio generation ────────────────────────────────────────────

    /// <summary>
    /// Generates speech-like audio for testing.
    /// SALSA analyzes amplitude, so this creates vowel-like syllables
    /// with varying volume to trigger viseme transitions.
    /// </summary>
    private AudioClip GenerateTestAudio(float duration)
    {
        int sampleRate = 44100;
        int samples = (int)(duration * sampleRate);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;

            // Syllable rhythm — creates speech-like amplitude variation
            float syllable = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 3.5f * Mathf.PI)), 0.5f);
            float wordGap = Mathf.Sin(t * 0.8f * Mathf.PI) > -0.3f ? 1f : 0f;
            float envelope = syllable * wordGap * 0.6f;

            // Fundamental frequency (voice pitch)
            float f0 = 150f + 20f * Mathf.Sin(t * 2f);
            float voice = 0f;
            for (int h = 1; h <= 5; h++)
                voice += Mathf.Sin(2f * Mathf.PI * f0 * h * t) / (h * h);

            data[i] = voice * 0.3f * envelope;
        }

        // Normalize to 0.6 peak (moderate volume for SALSA)
        float maxAmp = 0f;
        for (int i = 0; i < samples; i++)
            maxAmp = Mathf.Max(maxAmp, Mathf.Abs(data[i]));
        if (maxAmp > 0f)
        {
            float scale = 0.6f / maxAmp;
            for (int i = 0; i < samples; i++)
                data[i] *= scale;
        }

        var clip = AudioClip.Create("SALSATest", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ── Send to React Native ─────────────────────────────────────────────

    public void SendToReactNative(string type, string payload)
    {
        var msg = new BridgeMessage { type = type, payload = payload };
        string json = JsonUtility.ToJson(msg);

#if UNITY_IOS && !UNITY_EDITOR
        SendMessageToMobileApp(json);
#elif UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var javaClass = new AndroidJavaClass("com.azesmwayreactnativeunity.ReactNativeUnityViewManager"))
            {
                javaClass.CallStatic("sendMessageToMobileApp", json);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SalsaAvatarController] Android bridge: {e.Message}");
        }
#else
        Debug.Log($"[SalsaAvatarController] → RN: {json}");
#endif
    }

    // ── Data classes ─────────────────────────────────────────────────────

    [Serializable]
    public class BridgeMessage
    {
        public string type;
        public string payload;
    }
}
