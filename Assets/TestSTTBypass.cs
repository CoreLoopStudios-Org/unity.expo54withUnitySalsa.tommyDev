using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class TestSTTBypass : MonoBehaviour
{
    [Header("Gemini API Settings")]
    [Tooltip("Enter your Google Gemini API Key here.")]
    public string geminiApiKey = "";
    
    [TextArea(5, 15)]
    public string systemPrompt = "You are Yael Avraham, a friendly, upbeat, and practical Hebrew language tutor. For this specific lesson module, your sole focus is helping an English speaker learn how to navigate a restaurant setting in Israel. You want them to feel confident ordering food, asking for items, and speaking with a waiter. Your teaching approach is highly interactive, focusing on immediate, practical usage rather than deep grammatical theory.\nTeaching & Language Rules:\n1.Restaurant Theme: All scenarios, vocabulary, and roleplay must revolve around dining out (e.g., menus, ordering, asking for the check, specific foods).\n2.Extreme Brevity: Keep your responses incredibly short and directly to the point. Do not give long lectures or complex grammar rules. One or two sentences maximum before the prompt.\n3.Mandatory Pronunciation Prompt: You MUST end every single response by asking the user to practice pronouncing a specific word or short phrase. This is your primary teaching tool.\n4. Spoken Translation: Because your text will be read aloud by Text-to-Speech, always weave the English meaning naturally into your sentence before or after the Hebrew word. Do NOT just append a translation at the bottom; speak it as part of the lesson (e.g. 'The word for menu is tafrit, תַּפְרִיט').";

#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
    private DictationRecognizer dictationRecognizer;
#endif
    
    // We will find the ConvaiPlayer dynamically to send the SSML bypass message
    private MonoBehaviour convaiPlayer;

    void Start()
    {
        // Try to find the Convai Player in the scene
        MonoBehaviour[] allScripts = FindObjectsOfType<MonoBehaviour>();
        foreach (var script in allScripts)
        {
            if (script.GetType().Name.Contains("ConvaiPlayer"))
            {
                convaiPlayer = script;
                Debug.Log("[BypassDemo] Found ConvaiPlayer component!");
                break;
            }
        }

        if (convaiPlayer == null)
        {
            Debug.LogError("[BypassDemo] Could not find ConvaiPlayer component in the scene.");
        }

#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
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

            // Instead of sending straight to Convai, we send to Gemini first.
            StartCoroutine(SendToGeminiAndThenConvai(text));
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
#endif
    }

    void Update()
    {
        // Using the new Input System to check if Spacebar was pressed this frame
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ToggleSTT();
        }
    }

    public void ToggleSTT()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (dictationRecognizer.Status == SpeechSystemStatus.Running)
        {
            StopSTT();
        }
        else
        {
            StartSTT();
        }
#else
        Debug.LogWarning("[BypassDemo] ToggleSTT called on non-Windows platform. Implement Android STT trigger here.");
#endif
    }

    public void StartSTT()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (dictationRecognizer != null && dictationRecognizer.Status != SpeechSystemStatus.Running)
        {
            Debug.Log("<color=yellow>[BypassDemo] Starting Windows STT. Speak now...</color>");
            dictationRecognizer.Start();
            return;
        }
#endif

        // Fallback for Android or if Windows STT is not used: Try to trigger Convai's built-in recording
        if (convaiPlayer != null)
        {
            try
            {
                MethodInfo startRecordingMethod = convaiPlayer.GetType().GetMethod("StartRecording");
                if (startRecordingMethod != null)
                {
                    Debug.Log("<color=yellow>[BypassDemo] Triggering Convai StartRecording via reflection...</color>");
                    startRecordingMethod.Invoke(convaiPlayer, null);
                }
                else
                {
                    // Some versions use 'ActivateVoice' or similar
                    MethodInfo activateVoiceMethod = convaiPlayer.GetType().GetMethod("ActivateVoice");
                    if (activateVoiceMethod != null)
                    {
                        Debug.Log("<color=yellow>[BypassDemo] Triggering Convai ActivateVoice via reflection...</color>");
                        activateVoiceMethod.Invoke(convaiPlayer, null);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[BypassDemo] Error triggering Convai recording: " + ex.Message);
            }
        }
    }

    public void StopSTT()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (dictationRecognizer != null && dictationRecognizer.Status == SpeechSystemStatus.Running)
        {
            Debug.Log("<color=yellow>[BypassDemo] Stopping Windows STT...</color>");
            dictationRecognizer.Stop();
            return;
        }
#endif

        // Fallback: Try to trigger Convai's built-in stop recording
        if (convaiPlayer != null)
        {
            try
            {
                MethodInfo stopRecordingMethod = convaiPlayer.GetType().GetMethod("StopRecording");
                if (stopRecordingMethod != null)
                {
                    Debug.Log("<color=yellow>[BypassDemo] Triggering Convai StopRecording via reflection...</color>");
                    stopRecordingMethod.Invoke(convaiPlayer, null);
                }
                else
                {
                    MethodInfo deactivateVoiceMethod = convaiPlayer.GetType().GetMethod("DeactivateVoice");
                    if (deactivateVoiceMethod != null)
                    {
                        Debug.Log("<color=yellow>[BypassDemo] Triggering Convai DeactivateVoice via reflection...</color>");
                        deactivateVoiceMethod.Invoke(convaiPlayer, null);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[BypassDemo] Error stopping Convai recording: " + ex.Message);
            }
        }
    }

    IEnumerator SendToGeminiAndThenConvai(string userText)
    {
        if (string.IsNullOrEmpty(geminiApiKey))
        {
            Debug.LogError("[BypassDemo] Gemini API Key is missing! Please enter it in the Inspector.");
            yield break;
        }

        Debug.Log("<color=cyan>[BypassDemo] Sending text to Gemini...</color>");

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent?key={geminiApiKey}";
        
        // Escape strings safely for JSON
        string safeSystemPrompt = EscapeJsonString(systemPrompt);
        string safeUserText = EscapeJsonString(userText);

        string jsonPayload = $@"{{
            ""system_instruction"": {{
                ""parts"": [
                    {{""text"": ""{safeSystemPrompt}""}}
                ]
            }},
            ""contents"": [
                {{
                    ""parts"": [
                        {{""text"": ""{safeUserText}""}}
                    ]
                }}
            ]
        }}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("[BypassDemo] Gemini Request Error: " + request.error + " - " + request.downloadHandler.text);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                string geminiReply = ExtractGeminiContent(responseText);
                
                if (!string.IsNullOrEmpty(geminiReply))
                {
                    Debug.Log("<color=cyan>[BypassDemo] Gemini replied: </color>" + geminiReply);
                    
                    // Directly send TTS action through RTVI to bypass Convai's LLM completely.
                    SendDirectTTSToConvai(geminiReply);
                }
                else
                {
                    Debug.LogError("[BypassDemo] Failed to parse Gemini response: " + responseText);
                }
            }
        }
    }

    private string EscapeJsonString(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    // Gemini JSON Response Classes
    [System.Serializable]
    private class GeminiResponse
    {
        public Candidate[] candidates;
    }

    [System.Serializable]
    private class Candidate
    {
        public Content content;
    }

    [System.Serializable]
    private class Content
    {
        public Part[] parts;
    }

    [System.Serializable]
    private class Part
    {
        public string text;
    }

    private string ExtractGeminiContent(string jsonResponse)
    {
        try
        {
            GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(jsonResponse);
            if (response != null && response.candidates != null && response.candidates.Length > 0)
            {
                if (response.candidates[0].content != null && response.candidates[0].content.parts != null && response.candidates[0].content.parts.Length > 0)
                {
                    return response.candidates[0].content.parts[0].text;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BypassDemo] JSON Parsing Error: " + e.Message);
        }
        return null;
    }

    void SendDirectTTSToConvai(string text)
    {
        if (convaiPlayer == null)
        {
            Debug.LogError("[BypassDemo] ConvaiPlayer is null. Cannot send SSML action.");
            return;
        }

        try
        {
            // Pass plain text. The Convai character must be configured as a 'Relay Character' on the dashboard.
            string plainText = text;

            // Get the SendTextMessage(string) method
            MethodInfo sendTextMethod = convaiPlayer.GetType().GetMethod("SendTextMessage", new System.Type[] { typeof(string) });
            if (sendTextMethod == null)
            {
                Debug.LogError("[BypassDemo] Could not find 'SendTextMessage' method on ConvaiPlayer.");
                return;
            }

            // Invoke SendTextMessage
            Debug.Log("<color=cyan>[BypassDemo] Sending plain text to Convai (Relay strategy): </color>" + plainText);
            sendTextMethod.Invoke(convaiPlayer, new object[] { plainText });
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[BypassDemo] Error sending SSML action: " + ex.Message);
        }
    }

    void OnApplicationQuit()
    {
#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
        if (dictationRecognizer != null)
        {
            if (dictationRecognizer.Status == SpeechSystemStatus.Running)
            {
                dictationRecognizer.Stop();
            }
            dictationRecognizer.Dispose();
        }
#endif
    }
}
