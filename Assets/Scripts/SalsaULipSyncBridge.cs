using UnityEngine;
using uLipSync;
using CrazyMinnow.SALSA;
using System.Collections.Generic;

/// <summary>
/// Hybrid bridge that uses uLipSync's detection to drive SALSA's animation engine.
/// Bypasses SALSA's internal volume-based threshold logic.
/// </summary>
public class SalsaULipSyncBridge : MonoBehaviour
{
    public Salsa salsa;
    
    [Tooltip("Adjust this if the mouth movement is too subtle or too strong.")]
    public float weightMultiplier = 1.0f;

    private Dictionary<string, float> _phonemeToTriggerMap = new Dictionary<string, float>();
    private float _currentAnalysisLevel = 0f;

    void Start()
    {
        if (salsa == null) salsa = GetComponent<Salsa>();
        
        if (salsa == null)
        {
            Debug.LogError("[SalsaULipSyncBridge] Salsa reference is missing!");
            return;
        }

        // 1. Initialize mapping of phoneme names to SALSA trigger values
        MapVisemes();
        
        // 2. Point SALSA's delegate to our local function
        // We tell SALSA we are providing the analysis manually.
        salsa.useExternalAnalysis = true;
        salsa.getExternalAnalysis = ProvideValueToSalsa;
        
        Debug.Log($"[SalsaULipSyncBridge] Bridge established. Mapped {_phonemeToTriggerMap.Count} visemes.");
    }

    // This is the function SALSA calls every frame to decide which viseme to show.
    private float ProvideValueToSalsa()
    {
        return _currentAnalysisLevel;
    }

    public void MapVisemes()
    {
        _phonemeToTriggerMap.Clear();
        for (int i = 0; i < salsa.visemes.Count; i++)
        {
            // SALSA v2 stores the name in expData.name
            string vName = salsa.visemes[i].expData.name.ToLower();
            
            // This is the key: get the exact trigger point SALSA uses to activate this viseme.
            float triggerLevel = salsa.visemes[i].trigger;
            
            if (!_phonemeToTriggerMap.ContainsKey(vName))
            {
                _phonemeToTriggerMap.Add(vName, triggerLevel);
            }
        }
    }

    /// <summary>
    /// CALL THIS from the uLipSync "On Lip Sync Update" event.
    /// </summary>
    public void OnLipSyncUpdate(LipSyncInfo info)
    {
        if (salsa == null) return;

        // Find the trigger level for the detected phoneme (e.g., if "aa" is at trigger 0.5)
        if (_phonemeToTriggerMap.TryGetValue(info.phoneme.ToLower(), out float trigger))
        {
            // We set our analysis level to that trigger level, 
            // but we scale it by volume so it doesn't just "snap" fully open.
            // This forces SALSA to choose THIS SPECIFIC viseme.
            _currentAnalysisLevel = trigger * info.volume * weightMultiplier;
            
            // Ensure the value doesn't drop below the trigger threshold if we want it active,
            // but keep it proportional to volume for natural motion.
            if (_currentAnalysisLevel > 0 && _currentAnalysisLevel < trigger)
            {
                // If you want it to trigger MORE easily, you could clamp it here.
                // But usually, returning trigger * volume is perfect.
            }
        }
        else
        {
            _currentAnalysisLevel = 0f;
        }
    }

    // Re-run if visemes are changed in SALSA at runtime
    public void RefreshMapping() => MapVisemes();
}
