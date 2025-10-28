
using UnityEngine;
using UnityEditor;


[CustomPropertyDrawer(typeof(InfoBoxAttribute))]
public class InfoBoxDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {

        InfoBoxAttribute infoAttr = attribute as InfoBoxAttribute;

        float helpBoxHeight = EditorGUIUtility.singleLineHeight * 2;

        Rect helpBoxRect = new Rect(position.x, position.y, position.width, helpBoxHeight);
        EditorGUI.HelpBox(helpBoxRect, infoAttr.message, MessageType.None);

        Rect fieldRect = new Rect(position.x, position.y + helpBoxHeight + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(fieldRect, property, label);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {

        float helpBoxHeight = EditorGUIUtility.singleLineHeight * 2;
        return helpBoxHeight + EditorGUI.GetPropertyHeight(property, label) + EditorGUIUtility.standardVerticalSpacing;
    }
}