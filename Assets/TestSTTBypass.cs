// Editor / standalone debugging helper — NOT compiled into WebGL builds.
// All runtime bridging is handled by ConvaiNPCBridge in WebGL.
#if !UNITY_WEBGL || UNITY_EDITOR

using UnityEngine;
using System.Reflection;

public class TestSTTBypass : MonoBehaviour
{
    private MonoBehaviour _convaiPlayer;

    void Start()
    {
        foreach (var script in FindObjectsOfType<MonoBehaviour>())
        {
            if (script.GetType().Name.Contains("ConvaiPlayer"))
            {
                _convaiPlayer = script;
                break;
            }
        }

        if (_convaiPlayer == null)
            Debug.LogError("[TestSTTBypass] ConvaiPlayer not found in scene.");
    }

    /// <summary>
    /// Editor helper: relay plain text directly to Convai TTS pipeline.
    /// The Convai character must be configured as a verbatim relay on the dashboard.
    /// </summary>
    public void SendDirectTTSToConvai(string text)
    {
        if (_convaiPlayer == null)
        {
            Debug.LogError("[TestSTTBypass] ConvaiPlayer is null.");
            return;
        }

        MethodInfo method = _convaiPlayer.GetType()
            .GetMethod("SendTextMessage", new[] { typeof(string) });

        if (method == null)
        {
            Debug.LogError("[TestSTTBypass] SendTextMessage not found on ConvaiPlayer.");
            return;
        }

        Debug.Log("[TestSTTBypass] Sending to Convai: " + text);
        method.Invoke(_convaiPlayer, new object[] { text });
    }
}

#endif // !UNITY_WEBGL || UNITY_EDITOR
