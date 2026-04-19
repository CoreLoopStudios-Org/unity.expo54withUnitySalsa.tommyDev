using UnityEngine;

/// <summary>
/// Editor test panel for the SALSA-based avatar system.
/// OnGUI buttons for testing lip sync, body animations, and idle control.
/// Safe to leave in project — OnGUI doesn't render in UaaL on device.
/// </summary>
public class SalsaLipSyncTester : MonoBehaviour
{
    [Header("Test Audio (drag an AudioClip here)")]
    [Tooltip("Drag an MP3/WAV from Assets/Audio here to test with real speech")]
    public AudioClip testAudioClip;

    [Header("Mock Test")]
    public string mockPrompt = "tell me about gemini in details";
    private MockTestManager _mockManager;

    private SalsaAvatarController _controller;
    private string _status = "Ready";
    private float _statusTime;

    void Start()
    {
        _controller = FindFirstObjectByType<SalsaAvatarController>();
        _mockManager = FindFirstObjectByType<MockTestManager>();
        
        // Auto-add MockTestManager if missing (for convenience in Editor)
        if (_mockManager == null)
        {
            _mockManager = gameObject.AddComponent<MockTestManager>();
            Debug.Log("[SalsaLipSyncTester] Added MockTestManager component");
        }

        if (_controller == null)
            Debug.LogError("[SalsaLipSyncTester] SalsaAvatarController not found!");
        else
            Debug.Log("[SalsaLipSyncTester] Test panel ready");
    }

    void OnGUI()
    {
        if (_controller == null) return;

        float w = 200f, h = 28f, x = 10f, y = 10f, sp = 32f;

        var title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 30), "SALSA Avatar Test", title);
        y += 35;

        GUI.color = Color.yellow;
        GUI.Label(new Rect(x, y, 400, 24), _status);
        y += 28;
        GUI.color = Color.white;

        // ── Lip Sync ────────────────────────────────────
        GUI.Label(new Rect(x, y, 200, 20), "── Lip Sync ──");
        y += 22;

        if (GUI.Button(new Rect(x, y, w, h), "Test Lip Sync (tone)"))
        {
            Send("TEST_LIPSYNC", "");
            SetStatus("SALSA lip sync test playing...");
        }
        y += sp;

        // Mock Test Button
        GUI.color = new Color(0.7f, 1f, 0.7f); // Light green for highlight
        if (GUI.Button(new Rect(x, y, w, h), "Mock Test (Gemini + 11Labs)"))
        {
            if (_mockManager != null)
            {
                _mockManager.RunMockTest(mockPrompt);
                SetStatus("Starting Mock Test flow...");
            }
        }
        GUI.color = Color.white;
        y += sp;

        // Play custom audio clip button
        if (testAudioClip != null)
        {
            if (GUI.Button(new Rect(x, y, w, h), "Play: " + testAudioClip.name))
            {
                PlayCustomClip();
                SetStatus("Playing: " + testAudioClip.name);
            }
        }
        else
        {
            GUI.color = Color.gray;
            GUI.Button(new Rect(x, y, w, h), "(Drag AudioClip to Inspector)");
            GUI.color = Color.white;
        }
        y += sp;

        if (GUI.Button(new Rect(x, y, w, h), "Stop Audio"))
        {
            Send("STOP_AUDIO", "");
            SetStatus("Stopped");
        }
        y += sp + 5;

        // ── Body ────────────────────────────────────────
        GUI.Label(new Rect(x, y, 200, 20), "── Body ──");
        y += 22;

        if (GUI.Button(new Rect(x, y, 95, h), "Wave")) { Send("ANIM", "wave"); SetStatus("Wave"); }
        if (GUI.Button(new Rect(x + 100, y, 95, h), "Nod")) { Send("ANIM", "nod"); SetStatus("Nod"); }
        y += sp;
        if (GUI.Button(new Rect(x, y, 95, h), "Talk")) { Send("ANIM", "talk"); SetStatus("Talk gesture"); }
        if (GUI.Button(new Rect(x + 100, y, 95, h), "Idle")) { Send("ANIM", "idle"); SetStatus("Idle"); }
        y += sp + 5;

        // ── Idle Control ────────────────────────────────
        GUI.Label(new Rect(x, y, 200, 20), "── Eyes/Idle ──");
        y += 22;

        if (GUI.Button(new Rect(x, y, 95, h), "Eyes ON")) { Send("SET_IDLE", "1"); SetStatus("Eyes: ON"); }
        if (GUI.Button(new Rect(x + 100, y, 95, h), "Eyes OFF")) { Send("SET_IDLE", "0"); SetStatus("Eyes: OFF"); }
        y += sp + 5;

        // ── Debug ───────────────────────────────────────
        GUI.Label(new Rect(x, y, 200, 20), "── Debug ──");
        y += 22;

        if (GUI.Button(new Rect(x, y, 95, h), "Ping")) { Send("PING", ""); SetStatus("PING"); }
        if (GUI.Button(new Rect(x + 100, y, 95, h), "Test")) { Send("TEST", "hello"); SetStatus("TEST"); }

        if (Time.time - _statusTime > 5f) _status = "Ready";
    }

    /// <summary>
    /// Plays the custom AudioClip through SALSA's AudioSource.
    /// SALSA will automatically lip sync to whatever audio is playing.
    /// </summary>
    private void PlayCustomClip()
    {
        // Access SALSA's AudioSource through the controller
        var salsa = FindFirstObjectByType<CrazyMinnow.SALSA.Salsa>();
        if (salsa != null && salsa.audioSrc != null)
        {
            salsa.audioSrc.Stop();
            salsa.audioSrc.clip = testAudioClip;
            salsa.audioSrc.Play();
            Debug.Log($"[SalsaLipSyncTester] Playing custom clip: {testAudioClip.name} ({testAudioClip.length:F1}s)");
        }
        else
        {
            Debug.LogError("[SalsaLipSyncTester] SALSA AudioSource not found!");
        }
    }

    private void Send(string type, string payload)
    {
        var msg = new SalsaAvatarController.BridgeMessage { type = type, payload = payload };
        _controller.ReceiveMessage(JsonUtility.ToJson(msg));
    }

    private void SetStatus(string t) { _status = t; _statusTime = Time.time; }
}
