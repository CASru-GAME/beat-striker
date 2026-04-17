using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Alice;

namespace Alice.Editor
{
    [CustomEditor(typeof(StrikerState), true)]
    public class StrikerStateEditor : UnityEditor.Editor
    {
        private SerializedProperty parentsProp;
        private ReorderableList reorderableList;

        private void OnEnable()
        {
            // ターゲットが無効な場合は処理をスキップ
            if (target == null)
                return;

            parentsProp = serializedObject.FindProperty("parents");
            if (parentsProp != null)
            {
                // Unity標準のリストUI (ドラッグ並び替え、追加・削除ボタン付き) を作成
                reorderableList = new ReorderableList(serializedObject, parentsProp, true, true, true, true);

                // ヘッダーの描画（プルダウンの三角形を無くし、ラベルだけにする）
                reorderableList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "Parents");
                };

                // 各要素の描画（標準UIと同じ描画）
                reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var element = parentsProp.GetArrayElementAtIndex(index);
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.PropertyField(rect, element, GUIContent.none);
                };
            }
        }

        public override void OnInspectorGUI()
        {
            // ターゲットが無効な場合は処理をスキップ
            if (target == null)
                return;

            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (iterator.propertyPath != "m_Script" && iterator.propertyPath != "parents")
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
                enterChildren = false;
            }

            if (parentsProp != null && reorderableList != null)
            {
                EditorGUILayout.Space();
                // プルダウンなしで、常に展開された状態の標準リストUIを描画
                reorderableList.DoLayoutList();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
