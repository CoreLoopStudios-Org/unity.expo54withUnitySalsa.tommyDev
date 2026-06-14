using UnityEngine;
using Convai.Runtime.Components;

public class SpeakingAnimationController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Animator component controlling the character.")]
    public Animator animator;

    [Tooltip("The Convai Character script to listen to for speech events. If empty, it will try to find one on this object.")]
    public ConvaiCharacter convaiCharacter;

    [Header("Settings")]
    [Tooltip("The index of the Animator Layer that contains your hands-only Avatar Mask and speaking animation. (Base Layer is 0, so the first added layer is usually 1)")]
    public int animationLayerIndex = 1;

    [Tooltip("How fast to smoothly blend into and out of the animation.")]
    public float lerpSpeed = 5f;

    [Header("State")]
    [Tooltip("Toggle this boolean to true when speaking starts, and false when it ends.")]
    public bool isSpeaking = false;

    private float currentWeight = 0f;

    private void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning("SpeakingAnimationController: No Animator found on this object or assigned in the inspector.");
        }

        if (convaiCharacter == null)
        {
            convaiCharacter = GetComponent<ConvaiCharacter>();
        }

        if (convaiCharacter != null)
        {
            convaiCharacter.OnSpeechStarted += HandleSpeechStarted;
            convaiCharacter.OnTurnCompleted += HandleTurnCompleted;
        }
        else
        {
            Debug.Log("SpeakingAnimationController: No ConvaiCharacter found. You will need to set 'isSpeaking' manually or assign the component.");
        }
    }

    private void OnDestroy()
    {
        if (convaiCharacter != null)
        {
            convaiCharacter.OnSpeechStarted -= HandleSpeechStarted;
            convaiCharacter.OnTurnCompleted -= HandleTurnCompleted;
        }
    }

    private void HandleSpeechStarted()
    {
        SetSpeakingState(true);
    }

    private void HandleTurnCompleted(bool _)
    {
        SetSpeakingState(false);
    }

    private void Update()
    {
        if (animator == null) return;

        // The target layer weight: 1.0 (fully active) if speaking, 0.0 (inactive) if not.
        float targetWeight = isSpeaking ? 1f : 0f;

        // Smoothly interpolate the current weight towards the target weight
        currentWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * lerpSpeed);

        // Apply the weight to the specific layer in the Animator
        animator.SetLayerWeight(animationLayerIndex, currentWeight);
    }

    /// <summary>
    /// Call this method from your audio, lip-sync, or Convai scripts to trigger the animation manually.
    /// </summary>
    /// <param name="speaking">True if the character is speaking, false otherwise.</param>
    public void SetSpeakingState(bool speaking)
    {
        isSpeaking = speaking;
    }
}
