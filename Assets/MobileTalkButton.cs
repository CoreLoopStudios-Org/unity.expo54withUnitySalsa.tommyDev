// Editor / standalone debugging helper — NOT compiled into WebGL builds.
// Input in WebGL comes from the RN app via ConvaiNPCBridge.ReceiveTextFromApp.
#if !UNITY_WEBGL || UNITY_EDITOR

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Simple UI-button bridge to TestSTTBypass for editor / standalone debugging.
/// </summary>
public class MobileTalkButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private TestSTTBypass sttBypass;
    [Tooltip("If true, the button acts as Push-to-Talk. If false, it acts as a toggle.")]
    public bool pushToTalk = true;

    private void Start()
    {
        if (sttBypass == null)
            sttBypass = FindObjectOfType<TestSTTBypass>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pushToTalk) sttBypass?.SendDirectTTSToConvai("[push-to-talk start]");
    }

    public void OnPointerUp(PointerEventData eventData) { }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!pushToTalk) sttBypass?.SendDirectTTSToConvai("[tap-to-toggle]");
    }
}

#endif // !UNITY_WEBGL || UNITY_EDITOR
