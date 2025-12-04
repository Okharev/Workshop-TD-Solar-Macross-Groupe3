using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ReactiveInt))]
[CustomPropertyDrawer(typeof(ReactiveFloat))]
[CustomPropertyDrawer(typeof(ReactiveBool))]
public class ReactivePropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var valueProp = property.FindPropertyRelative("_value");

        if (valueProp == null)
        {
            EditorGUI.LabelField(position, label, new GUIContent("Error: _value not found"));
            return;
        }

        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(position, valueProp, label);

        if (EditorGUI.EndChangeCheck())
        {
            property.serializedObject.ApplyModifiedProperties();
            if (Application.isPlaying) TriggerNotify(property);
        }
    }

    private void TriggerNotify(SerializedProperty property)
    {
        try
        {
            var target = GetTargetObjectOfProperty(property);
            if (target == null) return;

            var method = target.GetType().GetMethod("Notify");
            method?.Invoke(target, null);
        }
        catch
        {
            // Silent fail for complex edge cases
        }
    }


    private object GetTargetObjectOfProperty(SerializedProperty prop)
    {
        if (prop == null) return null;

        var path = prop.propertyPath.Replace(".Array.data[", "[");
        object obj = prop.serializedObject.targetObject;
        var elements = path.Split('.');

        foreach (var element in elements)
            if (element.Contains("["))
            {
                var elementName = element.Substring(0, element.IndexOf("["));
                var index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "")
                    .Replace("]", ""));
                obj = GetValue_Imp(obj, elementName, index);
            }
            else
            {
                obj = GetValue_Imp(obj, element);
            }

        return obj;
    }

    private object GetValue_Imp(object source, string name)
    {
        if (source == null) return null;
        var type = source.GetType();

        while (type != null)
        {
            var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(source);

            var p = type.GetProperty(name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null) return p.GetValue(source, null);

            type = type.BaseType;
        }

        return null;
    }

    private object GetValue_Imp(object source, string name, int index)
    {
        var enumerable = GetValue_Imp(source, name) as IEnumerable;
        if (enumerable == null) return null;
        var enm = enumerable.GetEnumerator();

        for (var i = 0; i <= index; i++)
            if (!enm.MoveNext())
                return null;
        return enm.Current;
    }
}