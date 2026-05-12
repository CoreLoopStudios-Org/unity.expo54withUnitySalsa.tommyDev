using UnityEngine;
using UnityEngine.Windows.Speech;
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
    public string systemPrompt = "You are Yana, a friendly conversational Hebrew teacher. The user is a beginner. Do NOT simply repeat or translate what the user says. Instead, engage in a natural conversation. Teach them Hebrew step-by-step. If they say 'hello', introduce yourself, explain the Hebrew word for it ('Shalom'), and ask them to pronounce it. Keep your responses short. Always provide your response in Hebrew, followed by its English translation in a new line.";

    private DictationRecognizer dictationRecognizer;
    
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
    }

    void Update()
    {
        // Using the new Input System to check if Spacebar was pressed this frame
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
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
#endif
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
