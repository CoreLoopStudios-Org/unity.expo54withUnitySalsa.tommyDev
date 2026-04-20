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
/// All references (Salsa, Eyes, Emoter, Animator) must be assigned manually in the Inspector.
/// Body animations are handled via Animator Controller triggers.
/// </summary>
public class SalsaAvatarController : MonoBehaviour
{
    [Header("Avatar Reference")]
    [Tooltip("Root transform of the RPM avatar")]
    public Transform avatarRoot;

    [Header("SALSA References (Manual Assignment)")]
    [SerializeField] private Salsa salsa;
    [SerializeField] private Eyes eyes;
    [SerializeField] private Emoter emoter;

    [Header("Body Animation (Manual Assignment)")]
    [SerializeField] private Animator animator;

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
        // Get SALSA's AudioSource (SALSA creates/manages its own)
        if (salsa != null)
        {
            _audioSource = salsa.audioSrc;
            Debug.Log($"[SalsaAvatarController] SALSA initialized with AudioSource");
        }
        else
        {
            Debug.LogWarning("[SalsaAvatarController] SALSA reference missing! Lip sync will not work.");
            _audioSource = GetComponentInChildren<AudioSource>();
        }

        if (animator == null)
            Debug.LogWarning("[SalsaAvatarController] Animator reference missing! Body animations will not work.");

        _isReady = true;
        SendToReactNative("READY", "");
        Debug.Log("[SalsaAvatarController] Ready — manual assignments assumed complete");
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            StopAudio();
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
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // Triggers: wave, nod, talk, idle
        // Note: Animator state machine should handle returning to 'idle' automatically
        string trigger = animName.ToLower();
        animator.SetTrigger(trigger);

        Debug.Log($"[SalsaAvatarController] Animator trigger: {trigger}");
        SendToReactNative("ANIM_STARTED", animName);
    }

    private void HandlePlayAudio(string filePath)
    {
        StopAudio();
        StartCoroutine(LoadAndPlayAudio(filePath));
    }

    public void PlayAudioClip(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        StopAudio();

        _audioSource.clip = clip;
        _audioSource.Play();
        _isPlaying = true;

        SendToReactNative("AUDIO_STARTED", clip.length.ToString("F2"));
        StartCoroutine(WaitForAudioEnd());
    }

    private void HandleTestLipSync()
    {
        if (_audioSource == null) return;
        StopAudio();

        var clip = GenerateTestAudio(3f);
        _audioSource.clip = clip;
        _audioSource.Play();
        _isPlaying = true;

        SendToReactNative("AUDIO_STARTED", "test");
        StartCoroutine(WaitForAudioEnd());
    }

    private void HandleEmotion(string payload)
    {
        Debug.Log($"[SalsaAvatarController] Emotion: {payload}");
    }

    private void HandleSetIdle(string payload)
    {
        bool enable = payload != "0" && payload.ToLower() != "false";
        if (eyes != null) eyes.enabled = enable;
        Debug.Log($"[SalsaAvatarController] Idle: {(enable ? "ON" : "OFF")}");
    }

    // ── Audio loading ────────────────────────────────────────────────────

    private IEnumerator LoadAndPlayAudio(string filePath)
    {
        if (_audioSource == null) yield break;

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

            _audioSource.clip = clip;
            _audioSource.Play();
            _isPlaying = true;

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

    private AudioClip GenerateTestAudio(float duration)
    {
        int sampleRate = 44100;
        int samples = (int)(duration * sampleRate);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float syllable = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 3.5f * Mathf.PI)), 0.5f);
            float wordGap = Mathf.Sin(t * 0.8f * Mathf.PI) > -0.3f ? 1f : 0f;
            float envelope = syllable * wordGap * 0.6f;
            float f0 = 150f + 20f * Mathf.Sin(t * 2f);
            float voice = 0f;
            for (int h = 1; h <= 5; h++)
                voice += Mathf.Sin(2f * Mathf.PI * f0 * h * t) / (h * h);
            data[i] = voice * 0.3f * envelope;
        }

        float maxAmp = 0f;
        for (int i = 0; i < samples; i++) maxAmp = Mathf.Max(maxAmp, Mathf.Abs(data[i]));
        if (maxAmp > 0f)
        {
            float scale = 0.6f / maxAmp;
            for (int i = 0; i < samples; i++) data[i] *= scale;
        }

        var clip = AudioClip.Create("SALSATest", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

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
        catch (Exception e) { Debug.LogWarning($"[SalsaAvatarController] Android bridge: {e.Message}"); }
#else
        Debug.Log($"[SalsaAvatarController] → RN: {json}");
#endif
    }

    [Serializable]
    public class BridgeMessage
    {
        public string type;
        public string payload;
    }
}
