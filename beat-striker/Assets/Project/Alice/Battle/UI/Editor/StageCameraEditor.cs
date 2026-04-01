using UnityEditor;
using UnityEngine;

namespace Alice {
    [UnityEditor.CustomEditor(typeof(StageCamera))]
    public class StageCameraEditor : UnityEditor.Editor {
        private static readonly Rect CurveRange = new Rect(0f, 0f, 1f, 1f);
        private const string CurvePropertyName = "normalizedDistanceToDiagonalRatio";

        public override void OnInspectorGUI() {
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren)) {
                enterChildren = false;

                if (iterator.propertyPath == "m_Script") {
                    using (new EditorGUI.DisabledScope(true)) {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                    continue;
                }

                if (iterator.name == CurvePropertyName) {
                    DrawCurvePropertyWithFixedRange(iterator);
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawCurvePropertyWithFixedRange(SerializedProperty curveProperty) {
            curveProperty.animationCurveValue = EditorGUILayout.CurveField(
                new GUIContent(curveProperty.displayName),
                curveProperty.animationCurveValue,
                Color.green,
                CurveRange);
        }
    }
}
