using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A simple script to bridge a UI Button to the TestSTTBypass script.
/// Supports both Tap-to-Toggle and Hold-to-Talk (Push-to-Talk).
/// </summary>
public class MobileTalkButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private TestSTTBypass sttBypass;
    [Tooltip("If true, the button acts as Push-to-Talk. If false, it acts as a toggle.")]
    public bool pushToTalk = true;

    private void Start()
    {
        if (sttBypass == null)
        {
            sttBypass = FindObjectOfType<TestSTTBypass>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pushToTalk && sttBypass != null)
        {
            sttBypass.StartSTT();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pushToTalk && sttBypass != null)
        {
            sttBypass.StopSTT();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!pushToTalk && sttBypass != null)
        {
            sttBypass.ToggleSTT();
        }
    }
}
