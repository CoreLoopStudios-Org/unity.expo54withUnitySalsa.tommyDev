using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Reflection;
using UnityEngine.InputSystem; // Added for the new Input System

public class TestSTTBypass : MonoBehaviour
{
    private DictationRecognizer dictationRecognizer;
    
    // We will find the ConvaiPlayer dynamically to avoid namespace issues
    private MonoBehaviour convaiPlayer;

    void Start()
    {
        // Try to find the Convai Player in the scene
        GameObject playerObj = GameObject.Find("Convai Player");
        if (playerObj != null)
        {
            MonoBehaviour[] scripts = playerObj.GetComponents<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script.GetType().Name.Contains("ConvaiPlayer"))
                {
                    convaiPlayer = script;
                    Debug.Log("[BypassDemo] Found ConvaiPlayer component!");
                    break;
                }
            }
        }

        if (convaiPlayer == null)
        {
            Debug.LogError("[BypassDemo] Could not find 'Convai Player' object or component in the scene.");
        }

        // Initialize Windows Dictation
        dictationRecognizer = new DictationRecognizer();

        dictationRecognizer.DictationResult += (text, confidence) =>
        {
            Debug.LogFormat("<color=green>[BypassDemo] Windows STT Heard: {0} (Confidence: {1})</color>", text, confidence);
            
            // Immediately stop listening after getting a result to prevent loops
            if (dictationRecognizer.Status == SpeechSystemStatus.Running)
            {
                Debug.Log("<color=yellow>[BypassDemo] Auto-stopping STT to prevent feedback loop.</color>");
                dictationRecognizer.Stop();
            }

            SendToConvai(text);
        };

        dictationRecognizer.DictationComplete += (completionCause) =>
        {
            if (completionCause != DictationCompletionCause.Complete)
                Debug.LogErrorFormat("[BypassDemo] Dictation completed unsuccessfully: {0}.", completionCause);
        };

        dictationRecognizer.DictationError += (error, hresult) =>
        {
            Debug.LogErrorFormat("[BypassDemo] Dictation error: {0}; HResult = {1}.", error, hresult);
        };
    }

    void Update()
    {
        // Using the new Input System to check if Spacebar was pressed this frame
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (dictationRecognizer.Status == SpeechSystemStatus.Running)
            {
                Debug.Log("<color=yellow>[BypassDemo] Stopping STT...</color>");
                dictationRecognizer.Stop();
            }
            else
            {
                Debug.Log("<color=yellow>[BypassDemo] Starting Windows STT. Speak now...</color>");
                dictationRecognizer.Start();
            }
        }
    }

    void SendToConvai(string text)
    {
        if (convaiPlayer != null)
        {
            // Call SendTextMessage on the ConvaiPlayer
            // Using reflection since we don't have the exact namespace reference compiled in this script
            MethodInfo sendMethod = convaiPlayer.GetType().GetMethod("SendTextMessage", BindingFlags.Public | BindingFlags.Instance);
            if (sendMethod != null)
            {
                Debug.Log("<color=cyan>[BypassDemo] Sending text to Convai LLM: </color>" + text);
                sendMethod.Invoke(convaiPlayer, new object[] { text });
            }
            else
            {
                Debug.LogError("[BypassDemo] Could not find 'SendTextMessage(string)' method on ConvaiPlayer.");
            }
        }
    }

    void OnApplicationQuit()
    {
        if (dictationRecognizer != null)
        {
            if (dictationRecognizer.Status == SpeechSystemStatus.Running)
            {
                dictationRecognizer.Stop();
            }
            dictationRecognizer.Dispose();
        }
    }
}