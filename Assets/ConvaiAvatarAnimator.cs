using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Drives the avatar's body/head gesture animations from Convai speech state.
///
/// Parameters expected on the Animator Controller:
///   - bool    "IsSpeaking" : true while the character talks (Idle ⇄ Talking loop)
///   - trigger "Nod"        : a single yes/affirmation head nod
///   - trigger "Shake"      : a single no/negation head shake
///
/// Driven by ConvaiNPCBridge:
///   - QueueGestureForText(replyText) is called when text arrives from the app.
///   - SetSpeaking(true/false) is called on Convai speech start / turn end.
///     The queued nod/shake is fired on speech start so it lands with the voice.
///
/// Setup:
///   1. Attach to the Camila character GameObject (the one with the Animator), or
///      assign the Animator field manually.
///   2. The Animator Controller must define the three parameters above.
/// Note: Convai lipsync uses face blendshapes; these gestures use body/head bones,
/// so they run together without conflict.
/// </summary>
public class ConvaiAvatarAnimator : MonoBehaviour
{
    public enum Gesture { None, Nod, Shake }

    [SerializeField] private Animator _animator;

    [Header("Gestures")]
    [Tooltip("Turn ON later once Nod/Shake clips + triggers exist. OFF = talking/idle only.")]
    [SerializeField] private bool _enableGestures = false;

    [Header("Animator parameter names")]
    [SerializeField] private string _isSpeakingParam = "IsSpeaking";
    [SerializeField] private string _nodTrigger = "Nod";
    [SerializeField] private string _shakeTrigger = "Shake";

    [Header("Keyword detection (whole-word, case-insensitive)")]
    [Tooltip("Spoken words that trigger a NOD (affirmation). Hebrew + English.")]
    [SerializeField]
    private string[] _yesWords = { "yes", "yeah", "yep", "sure", "correct", "right", "כן", "נכון", "בסדר" };

    [Tooltip("Spoken words that trigger a HEAD SHAKE (negation). Hebrew + English.")]
    [SerializeField]
    private string[] _noWords = { "no", "nope", "not", "wrong", "לא", "אסור" };

    private static readonly char[] _separators =
    {
        ' ', '\t', '\n', '\r', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '־',
    };

    private int _isSpeakingHash, _nodHash, _shakeHash;
    private Gesture _pending = Gesture.None;
    private bool _isSpeaking;
    private bool _firedThisTurn;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        _isSpeakingHash = Animator.StringToHash(_isSpeakingParam);
        _nodHash = Animator.StringToHash(_nodTrigger);
        _shakeHash = Animator.StringToHash(_shakeTrigger);
    }

    /// <summary>
    /// Feed text (from the app reply OR the character's own transcript). If it contains a
    /// yes/no word, queue the gesture — and fire it immediately if already speaking.
    /// </summary>
    public void QueueGestureForText(string text)
    {
        if (!_enableGestures) return; // talking-only mode
        var g = DetectGesture(text);
        if (g == Gesture.None) return; // keep any earlier valid gesture; ignore plain text
        _pending = g;
        Debug.Log($"[AvatarAnim] heard text: \"{text}\" -> gesture: {_pending}");
        TryFire();
    }

    /// <summary>Toggle the talking/idle loop. On start, fire any queued nod/shake once.</summary>
    public void SetSpeaking(bool speaking)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[AvatarAnim] no Animator or no Controller assigned.");
            return;
        }

        _isSpeaking = speaking;
        _animator.SetBool(_isSpeakingHash, speaking);

        if (speaking)
        {
            _firedThisTurn = false; // new turn — allow one gesture
            TryFire();
        }
        else
        {
            _pending = Gesture.None; // turn ended — clear leftover
        }
    }

    /// <summary>Fire the queued gesture once per speaking turn, when actually talking.</summary>
    private void TryFire()
    {
        if (!_isSpeaking || _firedThisTurn || _pending == Gesture.None) return;
        _animator.SetTrigger(_pending == Gesture.Nod ? _nodHash : _shakeHash);
        Debug.Log($"[AvatarAnim] FIRED trigger: {_pending}");
        _firedThisTurn = true;
        _pending = Gesture.None;
    }

    private Gesture DetectGesture(string text)
    {
        if (string.IsNullOrEmpty(text)) return Gesture.None;

        var clean = StripHebrewNiqqud(text).ToLowerInvariant();
        var tokens = clean.Split(_separators, StringSplitOptions.RemoveEmptyEntries);

        // First matching word wins, so the gesture matches the lead of the sentence.
        foreach (var token in tokens)
        {
            foreach (var y in _yesWords)
                if (token == y) return Gesture.Nod;
            foreach (var n in _noWords)
                if (token == n) return Gesture.Shake;
        }
        return Gesture.None;
    }

    /// <summary>Remove Hebrew niqqud/cantillation marks so "כֵּן" matches "כן".</summary>
    private static string StripHebrewNiqqud(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            // Hebrew combining marks: niqqud U+0591–U+05C7.
            if (c >= '֑' && c <= 'ׇ') continue;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
