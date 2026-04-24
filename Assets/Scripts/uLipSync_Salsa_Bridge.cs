using UnityEngine;
using CrazyMinnow.SALSA;
using uLipSync;

[RequireComponent(typeof(Salsa))]
public class uLipSync_Salsa_Bridge : MonoBehaviour
{
    private Salsa salsa;

    [Header("Settings")][Tooltip("Minimum volume needed to trigger a mouth shape. Otherwise, goes to silence.")]
    public float silenceThreshold = 0.01f;

    // Variables we will feed into SALSA
    private int currentTargetVisemeIndex = -1;
    private float currentAnalysisValue = 0f;

    private void Awake()
    {
        salsa = GetComponent<Salsa>();

        // 1. Tell SALSA to stop analyzing the audio itself
        salsa.useExternalAnalysis = true;

        // 2. Hijack SALSA's value analyzer
        salsa.getExternalAnalysis = GetuLipSyncAnalysisValue;

        // 3. Hijack SALSA's logic that chooses WHICH viseme to play
        salsa.getTriggerIndex = GetuLipSyncTargetViseme;
    }

    // --- STEP 4: Link this to uLipSync's Event ---
    public void OnLipSyncUpdate(LipSyncInfo info)
    {
        currentTargetVisemeIndex = -1; // Default to silence (-1)

        // If the character is speaking loud enough
        if (info.volume > silenceThreshold)
        {
            // 'info.phoneme' automatically holds the name of the winning sound (e.g., "E", "aa")
            currentTargetVisemeIndex = FindSalsaVisemeIndexByName(info.phoneme);
            
            // Pass the volume to SALSA so its "EmoteR" still triggers on loud words
            currentAnalysisValue = info.volume * 5f; 
        }
        else
        {
            currentAnalysisValue = 0f;
        }
    }

    // --- SALSA DELEGATES ---
    private int GetuLipSyncTargetViseme()
    {
        return currentTargetVisemeIndex;
    }

    private float GetuLipSyncAnalysisValue()
    {
        return currentAnalysisValue;
    }

    // --- HELPER FUNCTION ---
    private int FindSalsaVisemeIndexByName(string phonemeName)
    {
        if (string.IsNullOrEmpty(phonemeName)) return -1;

        // Loop through SALSA's configured visemes
        for (int i = 0; i < salsa.visemes.Count; i++)
        {
            if (salsa.visemes[i].expData.name == phonemeName)
            {
                return i;
            }
        }
        return -1; // Return -1 (Silence) if no match is found
    }
}