using UnityEngine;
using CrazyMinnow.SALSA;
using System.Reflection;

public class SalsaDiscovery : MonoBehaviour
{
    void Start()
    {
        Debug.Log("--- DISCOVERY: Salsa ---");
        System.Type type = typeof(Salsa);
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Debug.Log($"PROP: {prop.PropertyType} {prop.Name}");
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Debug.Log($"FIELD: {field.FieldType} {field.Name}");

        Debug.Log("--- DISCOVERY: LipsyncExpression ---");
        System.Type vType = typeof(LipsyncExpression);
        foreach (var prop in vType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Debug.Log($"V-PROP: {prop.PropertyType} {prop.Name}");
        foreach (var field in vType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Debug.Log($"V-FIELD: {field.FieldType} {field.Name}");
    }
}
