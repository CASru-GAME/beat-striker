using UnityEngine;
using UnityEditor;

public class RemoveMissingScripts : EditorWindow
{
    [MenuItem("Window/Remove Missing Scripts")]
    public static void ShowWindow()
    {
        GetWindow<RemoveMissingScripts>("Remove Missing Scripts");
    }

    private void OnGUI()
    {
        GUILayout.Label("選択中のGameObjectと全子孫（非アクティブ含む）からMissing Scriptを削除", EditorStyles.boldLabel);
        if (GUILayout.Button("削除実行"))
        {
            RemoveMissing();
        }
    }

    private void RemoveMissing()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        int removedCount = 0;
        foreach (GameObject go in selectedObjects)
        {
            // 自身を含む全ての子オブジェクト（非アクティブも含む）を取得
            Transform[] allTransforms = go.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                if (count > 0)
                {
                    removedCount += count;
                    // 変更を反映するためにオブジェクトをDirty状態にする
                    EditorUtility.SetDirty(t.gameObject);
                }
            }
        }
        Debug.Log("削除されたMissing Scriptの数: " + removedCount);
    }
}
