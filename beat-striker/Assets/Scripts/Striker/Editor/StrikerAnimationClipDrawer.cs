using Core.Striker;
using UnityEditor;
using UnityEngine;

namespace Core.Striker.Editor {
    [CustomPropertyDrawer(typeof(StrikerAnimationClip))]
    public class StrikerAnimationClipDrawer : PropertyDrawer {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight * 4 + EditorGUIUtility.standardVerticalSpacing * 3;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            var titleRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(titleRect, label);

            EditorGUI.indentLevel++;

            var clipRect = new Rect(position.x, titleRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
            var fadeRect = new Rect(position.x, clipRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
            var speedRect = new Rect(position.x, fadeRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);

            // 変数の左側に水色の縦線を描画
            // 描画位置は現在のインデントに合わせて少し左にずらします
            float lineX = position.x + EditorGUI.indentLevel * 15f - 12f;
            var lineRect = new Rect(lineX, clipRect.y, 4f, speedRect.yMax - clipRect.y);
            EditorGUI.DrawRect(lineRect, new Color(0.2f, 0.8f, 1f, 0.8f)); // 水色 (RGBA)

            EditorGUI.PropertyField(clipRect, property.FindPropertyRelative("clip"));
            EditorGUI.PropertyField(fadeRect, property.FindPropertyRelative("fadeTime"));
            EditorGUI.PropertyField(speedRect, property.FindPropertyRelative("speed"));

            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }
    }
}
