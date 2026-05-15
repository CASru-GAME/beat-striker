using UnityEditor;
using UnityEngine;

namespace Alice.Editor {
    [CustomPropertyDrawer(typeof(AiActionSequenceItem))]
    public class AiActionSequenceItemDrawer : PropertyDrawer {
        const float VerticalSpacing = 2f;
        const float ButtonSpacing = 4f;
        const float ControlButtonWidth = 24f;
        const float DeleteButtonWidth = 28f;

        static readonly Color GroupColor = new(0.16f, 0.38f, 0.22f, 0.20f);
        static readonly Color ActionColor = new(0.16f, 0.27f, 0.44f, 0.20f);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            // 折りたたみを廃止し、常に展開状態として高さを計算
            property.isExpanded = true;

            var isRoot = IsRootNode(property);
            var sequenceItemsProperty = property.FindPropertyRelative("SequenceItems");
            var isGroupNode = isRoot || (sequenceItemsProperty != null && sequenceItemsProperty.arraySize > 0);

            var line = EditorGUIUtility.singleLineHeight;
            var height = 0f;

            if (isGroupNode) {
                height += line + VerticalSpacing; // Is Random 行
                height += line + VerticalSpacing; // 追加ボタン 兼 コントロール(並び替え/削除) 行

                if (sequenceItemsProperty != null) {
                    for (var i = 0; i < sequenceItemsProperty.arraySize; i++) {
                        var childProperty = sequenceItemsProperty.GetArrayElementAtIndex(i);
                        height += EditorGUI.GetPropertyHeight(childProperty, true) + VerticalSpacing;
                    }
                }
            }
            else {
                height += line + VerticalSpacing; // Direction & Button 横並び行

                bool hasParent = GetParentArray(property, out _, out _);
                if (hasParent) {
                    height += line + VerticalSpacing; // コントロール(並び替え/削除) 行
                }
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            property.isExpanded = true;

            var isRandomProperty = property.FindPropertyRelative("IsRandomSequence");
            var sequenceItemsProperty = property.FindPropertyRelative("SequenceItems");
            var buttonProperty = property.FindPropertyRelative("Button");
            var directionProperty = property.FindPropertyRelative("Direction");

            var isRoot = IsRootNode(property);
            var isGroupNode = isRoot || (sequenceItemsProperty != null && sequenceItemsProperty.arraySize > 0);

            bool hasParent = GetParentArray(property, out var parentArray, out var index);

            // 背景の描画 (階層が深くなるごとに自動的にインデントされて色が重なります)
            var contentRect = new Rect(position.x, position.y, position.width, position.height - VerticalSpacing);
            var bgRect = EditorGUI.IndentedRect(contentRect);
            EditorGUI.DrawRect(bgRect, isGroupNode ? GroupColor : ActionColor);

            // 内部のレイアウト計算のために一時的にインデントをリセット
            var originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var y = position.y;
            var startX = bgRect.x + 2f;
            var width = bgRect.width - 4f;
            var line = EditorGUIUtility.singleLineHeight;

            if (isGroupNode) {
                // 1行目: Is Random
                var randomRect = new Rect(startX, y, width, line);
                EditorGUI.PropertyField(randomRect, isRandomProperty, new GUIContent(" Is Random"));
                y += line + VerticalSpacing;

                // 2行目: 追加ボタン + (右端に) 並び替え/削除ボタン
                var line2Rect = new Rect(startX, y, width, line);
                float controlsWidth = hasParent ? (ControlButtonWidth * 2 + DeleteButtonWidth + ButtonSpacing * 2) : 0;
                float addButtonsWidth = width - controlsWidth - (hasParent ? ButtonSpacing * 4 : 0);

                var addGroupRect = new Rect(line2Rect.x, y, addButtonsWidth / 2 - ButtonSpacing / 2, line);
                var addActionRect = new Rect(line2Rect.x + addButtonsWidth / 2 + ButtonSpacing / 2, y,
                    addButtonsWidth / 2 - ButtonSpacing / 2, line);

                var addGroupLabel =
                    new GUIContent(EditorGUIUtility.IconContent("d_Folder Icon").image, "Add child group");
                var addActionLabel =
                    new GUIContent(EditorGUIUtility.IconContent("d_Toolbar Plus").image, "Add action item");

                if (GUI.Button(addGroupRect, addGroupLabel)) {
                    AddGroupItem(sequenceItemsProperty);
                }

                if (GUI.Button(addActionRect, addActionLabel)) {
                    AddActionItem(sequenceItemsProperty);
                }

                if (hasParent) {
                    var controlsRect = new Rect(line2Rect.xMax - controlsWidth, y, controlsWidth, line);
                    DrawSelfControls(controlsRect, parentArray, index);
                }

                y += line + VerticalSpacing;

                // 子要素の描画
                if (sequenceItemsProperty != null) {
                    EditorGUI.indentLevel = originalIndent + 1;
                    for (var i = 0; i < sequenceItemsProperty.arraySize; i++) {
                        var childProperty = sequenceItemsProperty.GetArrayElementAtIndex(i);
                        var childHeight = EditorGUI.GetPropertyHeight(childProperty, true);
                        var childRect = new Rect(position.x, y, position.width, childHeight);

                        EditorGUI.PropertyField(childRect, childProperty, GUIContent.none, true);
                        y += childHeight + VerticalSpacing;
                    }
                }
            }
            else {
                // 1行目: Direction と Button を横並びに (ラベルなし)
                var line1Rect = new Rect(startX, y, width, line);
                var halfWidth = (width - ButtonSpacing) / 2f;
                var dirRect = new Rect(line1Rect.x, y, halfWidth, line);
                var btnRect = new Rect(line1Rect.x + halfWidth + ButtonSpacing, y, halfWidth, line);

                EditorGUI.PropertyField(dirRect, directionProperty, GUIContent.none);
                EditorGUI.PropertyField(btnRect, buttonProperty, GUIContent.none);
                y += line + VerticalSpacing;

                // 2行目: (右端に) 並び替え/削除ボタン
                if (hasParent) {
                    var line2Rect = new Rect(startX, y, width, line);
                    float controlsWidth = ControlButtonWidth * 2 + DeleteButtonWidth + ButtonSpacing * 2;
                    var controlsRect = new Rect(line2Rect.xMax - controlsWidth, y, controlsWidth, line);
                    DrawSelfControls(controlsRect, parentArray, index);
                }
            }

            EditorGUI.indentLevel = originalIndent;
            EditorGUI.EndProperty();
        }

        // --- 内部ヘルパー関数 ---

        static bool GetParentArray(SerializedProperty property, out SerializedProperty parentArray, out int index) {
            parentArray = null;
            index = -1;
            var path = property.propertyPath;
            var lastBracket = path.LastIndexOf('[');
            if (lastBracket < 0) return false;

            var arrayPathEnd = path.LastIndexOf(".Array.data[", System.StringComparison.Ordinal);
            if (arrayPathEnd < 0) return false;

            var arrayPath = path.Substring(0, arrayPathEnd);
            parentArray = property.serializedObject.FindProperty(arrayPath);

            var indexStr = path.Substring(lastBracket + 1, path.Length - lastBracket - 2);
            if (int.TryParse(indexStr, out index)) {
                return parentArray != null && parentArray.isArray;
            }

            return false;
        }

        static bool IsRootNode(SerializedProperty property) {
            if (property == null) return false;
            // 親配列が存在しない場合はルートと判定
            return property.propertyPath.LastIndexOf(".Array.data[", System.StringComparison.Ordinal) < 0;
        }

        static void DrawSelfControls(Rect rect, SerializedProperty parentArray, int index) {
            var moveUpRect = new Rect(rect.x, rect.y, ControlButtonWidth, rect.height);
            var moveDownRect = new Rect(rect.x + ControlButtonWidth + ButtonSpacing, rect.y, ControlButtonWidth,
                rect.height);
            var deleteRect = new Rect(rect.xMax - DeleteButtonWidth, rect.y, DeleteButtonWidth, rect.height);

            using (new EditorGUI.DisabledScope(index <= 0)) {
                if (GUI.Button(moveUpRect, EditorGUIUtility.IconContent("d_scrollup", "Move up"),
                        EditorStyles.miniButtonLeft)) {
                    parentArray.MoveArrayElement(index, index - 1);
                    parentArray.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI(); // GUIの更新エラーを防ぐため即座にレイアウトを終了
                }
            }

            using (new EditorGUI.DisabledScope(index >= parentArray.arraySize - 1)) {
                if (GUI.Button(moveDownRect, EditorGUIUtility.IconContent("d_scrolldown", "Move down"),
                        EditorStyles.miniButtonMid)) {
                    parentArray.MoveArrayElement(index, index + 1);
                    parentArray.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUI.Button(deleteRect, EditorGUIUtility.IconContent("TreeEditor.Trash", "Delete item"),
                    EditorStyles.miniButtonRight)) {
                parentArray.DeleteArrayElementAtIndex(index);
                parentArray.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        static void AddGroupItem(SerializedProperty sequenceItemsProperty) {
            if (sequenceItemsProperty == null) return;

            var index = sequenceItemsProperty.arraySize;
            sequenceItemsProperty.InsertArrayElementAtIndex(index);
            var itemProperty = sequenceItemsProperty.GetArrayElementAtIndex(index);
            ResetItem(itemProperty);

            // グループとして認識させるため、空のアクションを1つ含める
            var childSequenceProperty = itemProperty.FindPropertyRelative("SequenceItems");
            if (childSequenceProperty != null) {
                childSequenceProperty.arraySize = 1;
                ResetItem(childSequenceProperty.GetArrayElementAtIndex(0));
            }
        }

        static void AddActionItem(SerializedProperty sequenceItemsProperty) {
            if (sequenceItemsProperty == null) return;

            var index = sequenceItemsProperty.arraySize;
            sequenceItemsProperty.InsertArrayElementAtIndex(index);
            var itemProperty = sequenceItemsProperty.GetArrayElementAtIndex(index);
            ResetItem(itemProperty);
        }

        static void ResetItem(SerializedProperty itemProperty) {
            if (itemProperty == null) return;

            var isRandomProperty = itemProperty.FindPropertyRelative("IsRandomSequence");
            var sequenceItemsProperty = itemProperty.FindPropertyRelative("SequenceItems");
            var buttonProperty = itemProperty.FindPropertyRelative("Button");
            var directionProperty = itemProperty.FindPropertyRelative("Direction");

            if (isRandomProperty != null) isRandomProperty.boolValue = false;
            if (sequenceItemsProperty != null) sequenceItemsProperty.arraySize = 0;
            if (buttonProperty != null) buttonProperty.enumValueIndex = 0;
            if (directionProperty != null) directionProperty.enumValueIndex = 0;
        }
    }
}