using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A utility script to provide SALSA-style Play/Stop buttons in the Inspector.
/// Perfect for repeated testing of the SALSA/uLipSync bridge.
/// </summary>
public class SalsaTestPlayer : MonoBehaviour
{
    [Header("Audio to Test")]
    public AudioClip testClip;
    
    [Header("References")]
    public AudioSource audioSource;
    public SalsaAvatarController controller;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (controller == null) controller = FindFirstObjectByType<SalsaAvatarController>();
    }

    public void Play()
    {
        if (testClip == null) 
        { 
            Debug.LogWarning("[SalsaTestPlayer] No test clip assigned! Please drag an AudioClip into the slot."); 
            return; 
        }
        
        if (controller != null)
        {
            // This ensures SALSA and uLipSync both react to the audio
            controller.PlayAudioClip(testClip);
        }
        else if (audioSource != null)
        {
            audioSource.clip = testClip;
            audioSource.Play();
        }
        else
        {
            Debug.LogError("[SalsaTestPlayer] No AudioSource or SalsaAvatarController found to play audio.");
        }
    }

    public void Stop()
    {
        if (controller != null)
        {
            // Triggers the standard bridge stop logic
            controller.ReceiveMessage("{\"type\":\"STOP_AUDIO\", \"payload\":\"\"}");
        }
        else if (audioSource != null)
        {
            audioSource.Stop();
        }
    }
}

// ── Custom Editor (Inspector UI) ─────────────────────────────────────
#if UNITY_EDITOR
[CustomEditor(typeof(SalsaTestPlayer))]
public class SalsaTestPlayerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the standard variables (Clip slot, etc.)
        DrawDefaultInspector();

        SalsaTestPlayer player = (SalsaTestPlayer)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("TESTING CONTROLS", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        
        // Green Play Button
        GUI.color = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("► PLAY CLIP", GUILayout.Height(45)))
        {
            if (Application.isPlaying)
                player.Play();
            else
                Debug.LogWarning("[SalsaTestPlayer] You must be in PLAY MODE to test the bridge.");
        }

        // Red Stop Button
        GUI.color = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("■ STOP", GUILayout.Height(45)))
        {
            if (Application.isPlaying)
                player.Stop();
        }

        EditorGUILayout.EndHorizontal();
        
        GUI.color = Color.white;
        EditorGUILayout.HelpBox("Use these buttons to repeatedly test the SALSA/uLipSync hybrid system during runtime.", MessageType.Info);
    }
}
#endif
