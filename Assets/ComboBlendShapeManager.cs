using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

// This script allows you to create custom "Combo" blendshapes that control multiple base blendshapes.
[ExecuteAlways]
public class ComboBlendShapeManager : MonoBehaviour
{
    [System.Serializable]
    public class BlendShapeTarget
    {
        public string blendShapeName;
        // The weight this base blendshape should reach when the combo slider is at 1.0.
        // Standard Unity blendshapes use a 0-100 range.
        [Range(-100f, 100f)]
        public float targetWeight = 100f; 
    }

    [System.Serializable]
    public class ComboBlendShape
    {
        public string comboName = "New Combo";
        
        // The value of this combo blendshape (0 to 1 as requested)
        [Range(0f, 1f)]
        public float value = 0f; 
        
        public List<BlendShapeTarget> targets = new List<BlendShapeTarget>();
    }

    public SkinnedMeshRenderer targetRenderer;
    public List<ComboBlendShape> comboBlendShapes = new List<ComboBlendShape>();

    // To keep track of which blendshapes we are controlling this frame
    private HashSet<int> affectedIndices = new HashSet<int>();

    void Start()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SkinnedMeshRenderer>();
    }

    void Update()
    {
        ApplyBlendShapes();
    }

    public void ApplyBlendShapes()
    {
        if (targetRenderer == null || targetRenderer.sharedMesh == null)
            return;

        int blendShapeCount = targetRenderer.sharedMesh.blendShapeCount;
        float[] finalWeights = new float[blendShapeCount];

        affectedIndices.Clear();

        // Calculate the contribution of each combo blendshape
        foreach (var combo in comboBlendShapes)
        {
            foreach (var target in combo.targets)
            {
                int index = targetRenderer.sharedMesh.GetBlendShapeIndex(target.blendShapeName);
                if (index != -1)
                {
                    affectedIndices.Add(index);
                    // Add to the final weight. (combo value is 0-1, targetWeight is typically 0-100)
                    finalWeights[index] += combo.value * target.targetWeight;
                }
            }
        }

        // Apply calculated weights to the actual SkinnedMeshRenderer
        foreach (int index in affectedIndices)
        {
            // Clamp between 0 and 100, which is the standard Unity blendshape range
            targetRenderer.SetBlendShapeWeight(index, Mathf.Clamp(finalWeights[index], 0f, 100f));
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ComboBlendShapeManager))]
public class ComboBlendShapeManagerEditor : Editor
{
    SerializedProperty targetRendererProp;
    SerializedProperty comboBlendShapesProp;

    private string[] availableBlendShapes;

    void OnEnable()
    {
        targetRendererProp = serializedObject.FindProperty("targetRenderer");
        comboBlendShapesProp = serializedObject.FindProperty("comboBlendShapes");
        RefreshBlendShapeList();
    }

    void RefreshBlendShapeList()
    {
        ComboBlendShapeManager manager = (ComboBlendShapeManager)target;
        if (manager.targetRenderer != null && manager.targetRenderer.sharedMesh != null)
        {
            Mesh mesh = manager.targetRenderer.sharedMesh;
            availableBlendShapes = new string[mesh.blendShapeCount];
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                availableBlendShapes[i] = mesh.GetBlendShapeName(i);
            }
        }
        else
        {
            availableBlendShapes = new string[0];
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(targetRendererProp, new GUIContent("Skinned Mesh Renderer"));
        if (EditorGUI.EndChangeCheck())
        {
            RefreshBlendShapeList();
        }

        if (availableBlendShapes == null || availableBlendShapes.Length == 0)
        {
            EditorGUILayout.HelpBox("Assign a SkinnedMeshRenderer with BlendShapes to start creating combos.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Combo Blend Shapes", EditorStyles.boldLabel);

        for (int i = 0; i < comboBlendShapesProp.arraySize; i++)
        {
            SerializedProperty comboProp = comboBlendShapesProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = comboProp.FindPropertyRelative("comboName");
            SerializedProperty valueProp = comboProp.FindPropertyRelative("value");
            SerializedProperty targetsProp = comboProp.FindPropertyRelative("targets");

            EditorGUILayout.BeginVertical("helpbox");
            
            // Header for Combo
            EditorGUILayout.BeginHorizontal();
            nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);
            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                comboBlendShapesProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            // The main value slider (0 to 1)
            EditorGUILayout.PropertyField(valueProp, new GUIContent("Combo Value (0 to 1)"));

            // Sub-blendshapes
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Base BlendShapes & Target Weights", EditorStyles.miniBoldLabel);
            
            for (int j = 0; j < targetsProp.arraySize; j++)
            {
                SerializedProperty targetProp = targetsProp.GetArrayElementAtIndex(j);
                SerializedProperty bsNameProp = targetProp.FindPropertyRelative("blendShapeName");
                SerializedProperty weightProp = targetProp.FindPropertyRelative("targetWeight");

                EditorGUILayout.BeginHorizontal();
                
                int currentIndex = Mathf.Max(0, System.Array.IndexOf(availableBlendShapes, bsNameProp.stringValue));
                int newIndex = EditorGUILayout.Popup(currentIndex, availableBlendShapes);
                if (newIndex >= 0 && newIndex < availableBlendShapes.Length)
                {
                    bsNameProp.stringValue = availableBlendShapes[newIndex];
                }

                weightProp.floatValue = EditorGUILayout.FloatField(weightProp.floatValue, GUILayout.Width(50));

                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    targetsProp.DeleteArrayElementAtIndex(j);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15);
            if (GUILayout.Button("Add Base BlendShape", EditorStyles.miniButton))
            {
                targetsProp.arraySize++;
                SerializedProperty newTarget = targetsProp.GetArrayElementAtIndex(targetsProp.arraySize - 1);
                newTarget.FindPropertyRelative("blendShapeName").stringValue = availableBlendShapes[0];
                newTarget.FindPropertyRelative("targetWeight").floatValue = 100f;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        if (GUILayout.Button("Create New Combo BlendShape", GUILayout.Height(30)))
        {
            comboBlendShapesProp.arraySize++;
            SerializedProperty newCombo = comboBlendShapesProp.GetArrayElementAtIndex(comboBlendShapesProp.arraySize - 1);
            newCombo.FindPropertyRelative("comboName").stringValue = "New Combo " + comboBlendShapesProp.arraySize;
            newCombo.FindPropertyRelative("value").floatValue = 0f;
            newCombo.FindPropertyRelative("targets").ClearArray();
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            // Update immediately in the editor so dragging the slider works in Edit mode
            ComboBlendShapeManager manager = (ComboBlendShapeManager)target;
            manager.ApplyBlendShapes();
        }
    }
}
#endif