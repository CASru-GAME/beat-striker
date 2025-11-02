using Core.Battle;
using UnityEngine;

// Compatibility wrapper kept at the original path so existing prefabs don't lose their component reference.
// This class simply inherits the implementation that lives in Core.Battle.SlashProjectile.
public class SlashProjectile : Core.Battle.SlashProjectile 
{
    protected override void Start()
    {
        base.Start();
        // 初期化時の向きと位置をログ出力
        Debug.Log($"SlashProjectile spawned at {transform.position}, rotation: {transform.rotation.eulerAngles}, forward: {transform.forward}");
    }

    protected override void Update()
    {
        base.Update();
        // 毎フレームの向きをログ出力（必要に応じてコメントアウト）
        // Debug.Log($"SlashProjectile moving: pos={transform.position}, forward={transform.forward}");
    }
}
