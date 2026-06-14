using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ComboBlendShapeTrigger : MonoBehaviour
{
    public enum IntervalMode { Fixed, Random }

    [System.Serializable]
    public class ComboTriggerItem
    {
        [Tooltip("The exact name of the combo blendshape to trigger")]
        public string comboName;
        
        public IntervalMode intervalMode = IntervalMode.Fixed;
        
        [Tooltip("Time to wait before triggering (if Fixed)")]
        public float fixedInterval = 2f;
        
        [Tooltip("Min time to wait before triggering (if Random)")]
        public float randomIntervalMin = 1f;
        
        [Tooltip("Max time to wait before triggering (if Random)")]
        public float randomIntervalMax = 5f;
        
        [Tooltip("How long it takes to smoothly transition to 1")]
        public float transitionDurationIn = 0.2f;

        [Tooltip("How long the blendshape stays at 1 before transitioning back")]
        public float activeDuration = 0.5f;

        [Tooltip("How long it takes to smoothly transition back to 0")]
        public float transitionDurationOut = 0.3f;
    }

    [Header("References")]
    [Tooltip("The manager holding your combo blendshapes.")]
    public ComboBlendShapeManager comboManager;

    [Header("Trigger List")]
    public List<ComboTriggerItem> triggerList = new List<ComboTriggerItem>();

    // We use a single coroutine to handle the active sequence so they don't fight each other
    private Coroutine activeSequence;
    private string currentActiveCombo = "";

    private void Start()
    {
        if (comboManager == null)
            comboManager = GetComponent<ComboBlendShapeManager>();

        if (comboManager == null)
        {
            Debug.LogWarning("ComboBlendShapeTrigger: No ComboBlendShapeManager found on this object.");
            return;
        }

        foreach (var item in triggerList)
        {
            StartCoroutine(WaitAndTriggerRoutine(item));
        }
    }

    private IEnumerator WaitAndTriggerRoutine(ComboTriggerItem item)
    {
        while (true)
        {
            float waitTime = item.intervalMode == IntervalMode.Fixed 
                ? item.fixedInterval 
                : Random.Range(item.randomIntervalMin, item.randomIntervalMax);

            yield return new WaitForSeconds(waitTime);

            // Interrupt any currently playing sequence and start this one
            if (activeSequence != null)
            {
                StopCoroutine(activeSequence);
            }
            activeSequence = StartCoroutine(PlaySequence(item));
        }
    }

    private IEnumerator PlaySequence(ComboTriggerItem item)
    {
        currentActiveCombo = item.comboName;

        // Find all combos we care about from the trigger list
        var combos = new Dictionary<string, ComboBlendShapeManager.ComboBlendShape>();
        foreach (var triggerItem in triggerList)
        {
            var c = comboManager.comboBlendShapes.Find(x => x.comboName == triggerItem.comboName);
            if (c != null && !combos.ContainsKey(triggerItem.comboName))
            {
                combos.Add(triggerItem.comboName, c);
            }
        }

        // Store starting values for smooth lerping (in case they are interrupted mid-transition)
        var startValues = new Dictionary<string, float>();
        foreach (var kvp in combos)
        {
            startValues[kvp.Key] = kvp.Value.value;
        }

        // 1. Lerp IN
        if (item.transitionDurationIn > 0f)
        {
            float t = 0;
            while (t < item.transitionDurationIn)
            {
                t += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(t / item.transitionDurationIn);

                foreach (var kvp in combos)
                {
                    float targetValue = (kvp.Key == item.comboName) ? 1f : 0f;
                    kvp.Value.value = Mathf.Lerp(startValues[kvp.Key], targetValue, normalizedTime);
                }
                yield return null;
            }
        }

        // Ensure values hit their exact target
        foreach (var kvp in combos)
        {
            kvp.Value.value = (kvp.Key == item.comboName) ? 1f : 0f;
        }

        // 2. Wait for active duration
        if (item.activeDuration > 0f)
        {
            yield return new WaitForSeconds(item.activeDuration);
        }

        // Update start values for lerping out
        foreach (var kvp in combos)
        {
            startValues[kvp.Key] = kvp.Value.value;
        }

        // 3. Lerp OUT
        if (item.transitionDurationOut > 0f)
        {
            float t = 0;
            while (t < item.transitionDurationOut)
            {
                t += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(t / item.transitionDurationOut);

                foreach (var kvp in combos)
                {
                    // Everything smoothly transitions back to 0
                    kvp.Value.value = Mathf.Lerp(startValues[kvp.Key], 0f, normalizedTime);
                }
                yield return null;
            }
        }

        // Ensure all are exactly 0 at the end
        foreach (var kvp in combos)
        {
            kvp.Value.value = 0f;
        }

        currentActiveCombo = "";
        activeSequence = null;
    }
}
