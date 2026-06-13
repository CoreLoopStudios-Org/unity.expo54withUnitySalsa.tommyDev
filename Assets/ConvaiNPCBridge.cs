using System.Reflection;
using System.Runtime.InteropServices;
using Convai.Domain.DomainEvents.Session;
using Convai.Runtime.Components;
using UnityEngine;
using Azesmway.Unity; 
/// <summary>
/// RN → Unity bridge. Attach this to a GameObject named exactly "ConvaiNPC" in the live scene.
/// The RN app calls: unityInstance.SendMessage("ConvaiNPC", method, payload)
///
/// Inbound from RN:
///   ReceiveTextFromApp(text)  — relays reply text to Convai TTS pipeline
///   SetEmotion(emotion)       — stub; wire to avatar expression if needed
///   StopLipsync("")           — stub; Convai v4.1.0 has no public interrupt API
///
/// Outbound to RN (via window.OnConvaiEvent):
///   {"type":"UNITY_READY"}
///   {"type":"TURN_STARTED"}
///   {"type":"TURN_DONE"}
///   {"type":"SESSION_ERROR","code":"...","message":"..."}
/// </summary>
public class ConvaiNPCBridge : MonoBehaviour
{
    [Header("Speech Animation Settings")]
    [Tooltip("If not assigned, the script will automatically try to find the Animator on the ConvaiCharacter GameObject or its children.")]
    [SerializeField] private Animator characterAnimator;
    [Tooltip("The Animator boolean parameter that will be set to true when speech starts and false when it ends.")]
    [SerializeField] private string speakingParameterName = "IsSpeaking";

    private ConvaiPlayer _convaiPlayer;
    private ConvaiCharacter _convaiCharacter;
    private bool _audioUnlockAttempted;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendToRN(string json);
#endif

    private void Start()
    {
        _convaiPlayer = FindObjectOfType<ConvaiPlayer>();
        _convaiCharacter = FindObjectOfType<ConvaiCharacter>();

        if (_convaiPlayer == null)
            Debug.LogError("[ConvaiNPCBridge] ConvaiPlayer not found. Check scene setup.");

        if (_convaiCharacter == null)
        {
            Debug.LogError("[ConvaiNPCBridge] ConvaiCharacter not found. Check scene setup.");
            return;
        }

        if (characterAnimator == null)
        {
            characterAnimator = _convaiCharacter.GetComponent<Animator>();
            if (characterAnimator == null)
            {
                characterAnimator = _convaiCharacter.GetComponentInChildren<Animator>();
            }
        }

        _convaiCharacter.OnCharacterReady += HandleCharacterReady;
        _convaiCharacter.OnSpeechStarted += HandleSpeechStarted;
        _convaiCharacter.OnTurnCompleted += HandleTurnCompleted;
        _convaiCharacter.OnSessionStateChanged += HandleSessionStateChanged;

        // Fire immediately if the character is already ready (re-enable / hot-reload case).
        if (_convaiCharacter.IsCharacterReady)
            HandleCharacterReady();
    }

    private void OnDestroy()
    {
        if (_convaiCharacter == null) return;
        _convaiCharacter.OnCharacterReady -= HandleCharacterReady;
        _convaiCharacter.OnSpeechStarted -= HandleSpeechStarted;
        _convaiCharacter.OnTurnCompleted -= HandleTurnCompleted;
        _convaiCharacter.OnSessionStateChanged -= HandleSessionStateChanged;
    }

    // ── RN → Unity ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by RN: unityInstance.SendMessage("ConvaiNPC", "ReceiveTextFromApp", text)
    /// Unlocks browser audio on first call (satisfies WebGL autoplay policy when the JS
    /// call originates from a user gesture in the RN layer), then relays text to Convai.
    /// </summary>
    public void ReceiveTextFromApp(string text)
    {
        TryUnlockAudio();

        if (_convaiPlayer == null)
        {
            Debug.LogError("[ConvaiNPCBridge] ReceiveTextFromApp: ConvaiPlayer is null.");
            return;
        }

        _convaiPlayer.SendTextMessage(text);
    }

    /// <summary>
    /// Called by RN: unityInstance.SendMessage("ConvaiNPC", "SetEmotion", emotion)
    /// Stub — wire to the avatar's expression/blend system when that is available.
    /// </summary>
    public void SetEmotion(string emotion) { }

    /// <summary>
    /// Called by RN: unityInstance.SendMessage("ConvaiNPC", "StopLipsync", "")
    /// Stub — Convai SDK v4.1.0 does not expose a public playback-interrupt method.
    /// </summary>
    public void StopLipsync(string ignored) { }

    // ── Convai event handlers → RN ────────────────────────────────────────────

    private void HandleCharacterReady()
        => Emit("{\"type\":\"UNITY_READY\"}");

    private void HandleSpeechStarted()
    {
        Emit("{\"type\":\"TURN_STARTED\"}");
        if (characterAnimator != null && !string.IsNullOrEmpty(speakingParameterName))
        {
            characterAnimator.SetBool(speakingParameterName, true);
        }
    }

    private void HandleTurnCompleted(bool wasInterrupted)
    {
        Emit("{\"type\":\"TURN_DONE\"}");
        if (characterAnimator != null && !string.IsNullOrEmpty(speakingParameterName))
        {
            characterAnimator.SetBool(speakingParameterName, false);
        }
    }

    private void HandleSessionStateChanged(SessionState state)
    {
        if (state == SessionState.Error)
            Emit("{\"type\":\"SESSION_ERROR\",\"code\":\"SESSION_ERROR\",\"message\":\"Convai session entered error state\"}");
    }

    // ── WebGL → page JS ───────────────────────────────────────────────────────
    
    private void Emit(string json)
    {
        if (UnityMessageManager.Instance != null)
        {
            UnityMessageManager.Instance.SendMessageToRN(json);
        }
        else
        {
            Debug.LogWarning($"[ConvaiNPCBridge] UnityMessageManager instance missing. Local log: {json}");
        }
    }
    // ── Audio unlock ──────────────────────────────────────────────────────────

    /// <summary>
    /// Enables WebRTC audio playback without requesting microphone capture.
    /// Called once on the first ReceiveTextFromApp, when the originating JS call
    /// comes from a user gesture (RN button / voice tap) — satisfying the browser's
    /// AudioContext unlock requirement.
    ///
    /// We invoke ConvaiRoomManager.EnableAudioPlayback() via reflection so that mic
    /// capture (EnableAudioAndStartListening) is NOT triggered. This keeps Unity
    /// in receive-only (TTS playback) mode as required by the architecture.
    /// </summary>
    private void TryUnlockAudio()
    {
        if (_audioUnlockAttempted) return;
        _audioUnlockAttempted = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        var manager = ConvaiManager.ActiveManager;
        if (manager == null) return;

        // Reach into ConvaiManager._roomManager (private field, stable since v4.x).
        var rmField = typeof(ConvaiManager).GetField(
            "_roomManager",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var roomManager = rmField?.GetValue(manager);
        if (roomManager == null) return;

        // EnableAudioPlayback() calls transport.EnableAudio() — audio only, no mic.
        var enableAudioMethod = roomManager.GetType().GetMethod(
            "EnableAudioPlayback",
            BindingFlags.NonPublic | BindingFlags.Instance);
        enableAudioMethod?.Invoke(roomManager, null);
#endif
    }
}
