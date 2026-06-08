#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Simple helper to test ConvaiNPCBridge in the Unity Editor.
/// </summary>
public class ConvaiBridgeTester : MonoBehaviour
{
    [SerializeField] private ConvaiNPCBridge bridge;
    [SerializeField] private string testMessage = "Hello, how are you?";

    private void Start()
    {
        if (bridge == null)
            bridge = FindObjectOfType<ConvaiNPCBridge>();
            
        if (bridge == null)
            Debug.LogError("[ConvaiBridgeTester] ConvaiNPCBridge not found in scene.");
    }

    [ContextMenu("Test: ReceiveTextFromApp")]
    public void TestReceiveText()
    {
        if (bridge != null)
        {
            Debug.Log($"[ConvaiBridgeTester] Simulating RN call: ReceiveTextFromApp('{testMessage}')");
            bridge.ReceiveTextFromApp(testMessage);
        }
    }
}
#endif
