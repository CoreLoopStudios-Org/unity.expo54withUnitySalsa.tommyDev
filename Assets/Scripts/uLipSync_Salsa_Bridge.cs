using UnityEngine;
using CrazyMinnow.SALSA;
using uLipSync;[RequireComponent(typeof(Salsa))]
public class uLipSync_Salsa_Bridge : MonoBehaviour
{
    private Salsa salsa;

    [Header("uLipSync to SALSA Calibration")][Tooltip("HARD NOISE GATE: Any raw volume below this number is completely ignored. Increase this slightly until background noise stops making the mouth twitch.")]
    [Range(0f, 0.05f)]
    public float noiseGateThreshold = 0.005f;[Tooltip("VOLUME BOOSTER: uLipSync volume is very low. Multiply it here to match SALSA's 0 to 1 scale. If the mouth doesn't open wide enough when actually speaking, increase this.")]
    [Range(1f, 20f)]
    public float volumeMultiplier = 5f;

    // Variables we will feed into SALSA
    private int currentTargetVisemeIndex = -1;
    private float currentAnalysisValue = 0f;

    private void Awake()
    {
        salsa = GetComponent<Salsa>();
        
        // Take over SALSA's logic
        salsa.useExternalAnalysis = true;
        salsa.getExternalAnalysis = GetuLipSyncAnalysisValue;
        salsa.getTriggerIndex = GetuLipSyncTargetViseme;
    }

    public void OnLipSyncUpdate(LipSyncInfo info)
    {
        // --- 1. THE HARD NOISE GATE ---
        // If the uLipSync volume is lower than our threshold, it is just background noise. 
        // We force silence and STOP running the code immediately.
        if (info.volume <= noiseGateThreshold)
        {
            currentTargetVisemeIndex = -1;
            currentAnalysisValue = 0f;
            return; 
        }

        // --- 2. SCALE THE VOLUME ---
        // Since we survived the noise gate, it's real speech! 
        // We multiply it by your slider value so SALSA can read it properly.
        float rawVolumeScaled = info.volume * volumeMultiplier; 

        // --- 3. APPLY SALSA'S SMOOTHING CUTOFFS ---
        float salsaFilteredVolume = Mathf.Clamp01((rawVolumeScaled - salsa.loCutoff) / (salsa.hiCutoff - salsa.loCutoff));

        // --- 4. TRIGGER SALSA ---
        if (salsaFilteredVolume > 0f)
        {
            currentTargetVisemeIndex = FindSalsaVisemeIndexByName(info.phoneme);
            currentAnalysisValue = salsaFilteredVolume; 
        }
        else
        {
            currentTargetVisemeIndex = -1;
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