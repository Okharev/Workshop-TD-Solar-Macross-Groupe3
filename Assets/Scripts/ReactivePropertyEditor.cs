using UnityEditor;
using UnityEngine;

namespace Economy.Reactive.Editor
{
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
                // 1. Let Unity update the C# object with the new value
                property.serializedObject.ApplyModifiedProperties();

                // 2. Force the event to fire if the game is running
                if (Application.isPlaying) TriggerNotify(property);
            }
        }

        private void TriggerNotify(SerializedProperty property)
        {
            try
            {
                // Reflection to find the object instance
                object target = property.serializedObject.targetObject;
                var reactiveInstance = fieldInfo.GetValue(target);

                // Call the .Notify() method we just added
                var method = reactiveInstance.GetType().GetMethod("Notify");
                method?.Invoke(reactiveInstance, null);
            }
            catch
            {
                // Fail silently if complex nesting prevents reflection
            }
        }
    }
}