using UnityEngine;
using CrazyMinnow.SALSA;
using uLipSync;

[RequireComponent(typeof(Salsa))]
public class uLipSync_Salsa_Bridge : MonoBehaviour
{
    private Salsa salsa;

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
        currentTargetVisemeIndex = -1; // Default to silence

        // --- THE SALSA NOISE FILTER ---
        // uLipSync gives us a very low RMS volume. We multiply by a factor (e.g., 5-10) to make it 
        // roughly equivalent to SALSA's 0 to 1 amplitude scale.
        float rawVolumeScaled = info.volume * 5f; 

        // Apply SALSA's math exactly as written in their decompiled code:
        // Mathf.Clamp01((audioValue - loCutoff) / (hiCutoff - loCutoff))
        float salsaFilteredVolume = Mathf.Clamp01((rawVolumeScaled - salsa.loCutoff) / (salsa.hiCutoff - salsa.loCutoff));

        // If the filtered volume survives the low-cutoff threshold (meaning it's actual speech, not noise)
        if (salsaFilteredVolume > 0f)
        {
            currentTargetVisemeIndex = FindSalsaVisemeIndexByName(info.phoneme);
            currentAnalysisValue = salsaFilteredVolume; // Feed the filtered clean volume to SALSA
        }
        else
        {
            // It was just noise (below the loCutoff slider), so stay silent
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

        for (int i = 0; i < salsa.visemes.Count; i++)
        {
            if (salsa.visemes[i].expData.name == phonemeName)
            {
                return i;
            }
        }
        return -1; 
    }
}