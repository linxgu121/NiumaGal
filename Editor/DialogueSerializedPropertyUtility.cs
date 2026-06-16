using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    internal static class DialogueSerializedPropertyUtility
    {
        public static void AddRelativeProperty(VisualElement parent, SerializedProperty owner, string propertyName, string label)
        {
            var property = owner?.FindPropertyRelative(propertyName);
            if (property != null)
            {
                parent.Add(new PropertyField(property, label));
            }
        }

        public static string GetString(SerializedProperty parent, string relativeName)
        {
            return parent?.FindPropertyRelative(relativeName)?.stringValue ?? string.Empty;
        }

        public static void SetString(SerializedProperty parent, string relativeName, string value)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        public static void SetObject(SerializedProperty parent, string relativeName, UnityEngine.Object value)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        public static void SetEnumIndex(SerializedProperty parent, string relativeName, int value)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property != null && property.propertyType == SerializedPropertyType.Enum)
            {
                property.enumValueIndex = Mathf.Clamp(value, 0, Math.Max(0, property.enumDisplayNames.Length - 1));
            }
        }

        public static void SetInt(SerializedProperty parent, string relativeName, int value)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        public static void SetFloat(SerializedProperty parent, string relativeName, float value)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        public static void SetBool(SerializedProperty parent, string relativeName, bool value)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        public static void ClearArray(SerializedProperty parent, string relativeName)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property != null && property.isArray)
            {
                property.ClearArray();
            }
        }

        public static void ResetCustomData(SerializedProperty parent)
        {
            var property = parent?.FindPropertyRelative("CustomData");
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.String)
            {
                property.stringValue = string.Empty;
                return;
            }

            if (property.isArray)
            {
                property.ClearArray();
            }
        }

        public static void DeleteArrayElement(SerializedProperty arrayProperty, int index)
        {
            if (arrayProperty == null || index < 0 || index >= arrayProperty.arraySize)
            {
                return;
            }

            var oldSize = arrayProperty.arraySize;
            arrayProperty.DeleteArrayElementAtIndex(index);
            if (arrayProperty.arraySize == oldSize && index >= 0 && index < arrayProperty.arraySize)
            {
                arrayProperty.DeleteArrayElementAtIndex(index);
            }
        }

        public static string GetEnumDisplayName(SerializedProperty parent, string relativeName)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
            {
                return "None";
            }

            var index = property.enumValueIndex;
            return index >= 0 && index < property.enumDisplayNames.Length
                ? property.enumDisplayNames[index]
                : "None";
        }

        public static string GetEnumName(SerializedProperty parent, string relativeName)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
            {
                return "None";
            }

            var index = property.enumValueIndex;
            return index >= 0 && index < property.enumNames.Length
                ? property.enumNames[index]
                : "None";
        }

        public static string Fallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
