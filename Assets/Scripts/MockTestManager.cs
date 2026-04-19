using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Handles the "Mock Test" flow:
/// 1. Call Gemini API for Hebrew text.
/// 2. Call ElevenLabs API for audio generation.
/// 3. Play the audio via SalsaAvatarController.
/// </summary>
public class MockTestManager : MonoBehaviour
{
    [Header("API Configuration")]
    public string geminiApiKey = "YOUR_GEMINI_API_KEY";
    public string elevenLabsApiKey = "YOUR_ELEVENLABS_API_KEY";
    public string voiceId = "21m00Tcm4TlvDq8ikWAM"; // Rachel voice ID as default

    [Header("References")]
    public SalsaAvatarController controller;

    private bool _isProcessing = false;

    void Start()
    {
        if (controller == null)
            controller = FindFirstObjectByType<SalsaAvatarController>();
    }

    public void RunMockTest(string prompt)
    {
        if (_isProcessing) return;
        StartCoroutine(MockTestCoroutine(prompt));
    }

    private IEnumerator MockTestCoroutine(string prompt)
    {
        _isProcessing = true;
        Debug.Log($"[MockTest] Starting flow for prompt: {prompt}");

        // 1. Get Hebrew text from Gemini
        string hebrewText = "";
        yield return GetGeminiText(prompt, (result) => hebrewText = result);

        if (string.IsNullOrEmpty(hebrewText))
        {
            Debug.LogError("[MockTest] Failed to get Hebrew text from Gemini");
            _isProcessing = false;
            yield break;
        }

        Debug.Log($"[MockTest] Gemini Response: {hebrewText}");

        // 2. Get Audio from ElevenLabs
        yield return GetElevenLabsAudio(hebrewText);

        _isProcessing = false;
    }

    private IEnumerator GetGeminiText(string prompt, Action<string> callback)
    {
        // Using the specific URL provided by the user
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent?key={geminiApiKey}";
        
        Debug.Log($"[MockTest] Calling Gemini API: https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent?key=HIDDEN");

        // System instruction to ensure Hebrew output
        string jsonBody = "{\"contents\": [{\"parts\": [{\"text\": \"Translate or explain the following in Hebrew. Return ONLY the Hebrew text: " + prompt + "\"}]}]}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MockTest] Gemini Error: {request.error}\n{request.downloadHandler.text}");
                callback?.Invoke(null);
            }
            else
            {
                // Simple parsing for Gemini nested structure
                var response = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                if (response != null && response.candidates != null && response.candidates.Length > 0)
                {
                    callback?.Invoke(response.candidates[0].content.parts[0].text);
                }
                else
                {
                    callback?.Invoke(null);
                }
            }
        }
    }

    private IEnumerator GetElevenLabsAudio(string text)
    {
        string url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";
        
        string jsonBody = "{\"text\": \"" + text.Replace("\"", "\\\"") + "\", \"model_id\": \"eleven_multilingual_v2\"}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            
            // We need to save to a file because UnityWebRequestMultimedia.GetAudioClip needs a URL or file path for MP3
            string tempPath = Path.Combine(Application.temporaryCachePath, "mock_audio.mp3");
            request.downloadHandler = new DownloadHandlerFile(tempPath);

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", elevenLabsApiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MockTest] ElevenLabs Error: {request.error}");
            }
            else
            {
                Debug.Log($"[MockTest] Audio saved to: {tempPath}");
                
                // Now load and play via SalsaAvatarController
                // SalsaAvatarController already has a method to load from path
                controller.ReceiveMessage("{\"type\": \"PLAY_AUDIO\", \"payload\": \"" + tempPath.Replace("\\", "/") + "\"}");
            }
        }
    }

    // ── Gemini JSON Classes ──────────────────────────────────────────────

    [Serializable]
    public class GeminiResponse
    {
        public Candidate[] candidates;
    }

    [Serializable]
    public class Candidate
    {
        public Content content;
    }

    [Serializable]
    public class Content
    {
        public Part[] parts;
    }

    [Serializable]
    public class Part
    {
        public string text;
    }
}
