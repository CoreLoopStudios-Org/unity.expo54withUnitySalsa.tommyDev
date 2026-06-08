using TMPro;
using UnityEngine;

/// <summary>
/// A simple FPS tracker that updates a TextMesh Pro text element.
/// Provides configurable target FPS and color-coded feedback.
/// </summary>
public class FPSTracker : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("The TextMesh Pro component used to display the FPS.")]
    [SerializeField] private TMP_Text fpsText;

    [Header("Timing")]
    [Tooltip("How often the FPS display should update (in seconds).")]
    [SerializeField] private float updateInterval = 0.5f;

    [Header("Targeting")]
    [Tooltip("The desired frame rate. Used for color coding and optional frame rate limiting.")]
    [SerializeField] private int targetFPS = 60;
    
    [Tooltip("If true, Application.targetFrameRate will be set to Target FPS on Start.")]
    [SerializeField] private bool setTargetFrameRate = true;

    [Header("Colors")]
    [SerializeField] private Color goodColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;

    private float _accumulatedTime;
    private int _frameCount;
    private float _timeLeft;

    private void Start()
    {
        if (setTargetFrameRate)
        {
            Application.targetFrameRate = targetFPS;
        }

        if (fpsText == null)
        {
            fpsText = GetComponent<TMP_Text>();
        }

        if (fpsText == null)
        {
            Debug.LogWarning("[FPSTracker] No TMP_Text component assigned or found on this GameObject.");
        }

        _timeLeft = updateInterval;
    }

    private void Update()
    {
        // Use unscaledDeltaTime so the tracker remains accurate even if Time.timeScale is modified.
        float deltaTime = Time.unscaledDeltaTime;
        _timeLeft -= deltaTime;
        _accumulatedTime += deltaTime;
        _frameCount++;

        if (_timeLeft <= 0.0)
        {
            float fps = _frameCount / _accumulatedTime;
            UpdateDisplay(fps);

            _timeLeft = updateInterval;
            _accumulatedTime = 0.0f;
            _frameCount = 0;
        }
    }

    private void UpdateDisplay(float fps)
    {
        if (fpsText == null) return;

        fpsText.text = $"FPS: {fps:F1}";

        // Color coding based on performance relative to target
        if (fps >= targetFPS * 0.95f)
        {
            fpsText.color = goodColor;
        }
        else if (fps >= targetFPS * 0.7f)
        {
            fpsText.color = warningColor;
        }
        else
        {
            fpsText.color = criticalColor;
        }
    }

    /// <summary>
    /// Allows external scripts to update the target FPS at runtime.
    /// </summary>
    public void SetTargetFPS(int newTarget)
    {
        targetFPS = newTarget;
        if (setTargetFrameRate)
        {
            Application.targetFrameRate = targetFPS;
        }
    }
}
