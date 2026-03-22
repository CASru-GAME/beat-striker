import bpy
import math

def setup_bone_rotation_limits():
    """
    アクティブなアーマチュアオブジェクト内の全ボーンに対して、
    ボーン名に応じた回転可動域（Limit Rotationコンストレイント）を自動設定します。
    """
    obj = bpy.context.object
    if not obj or obj.type != 'ARMATURE':
        print("エラー: アーマチュア（ボーン構造）オブジェクトを選択した状態で実行してください。")
        return

    bpy.ops.object.mode_set(mode='POSE')

    # === リセット処理: 過去にこのスクリプトで付与したコンストレイントを全ボーンから一旦削除 ===
    reset_count = 0
    for pb in obj.pose.bones:
        # リストをコピーして回す（削除中のインデックスずれを防ぐため）
        for c in list(pb.constraints):
            # このスクリプトで付けられた名前のLimit Rotationを削除
            if c.type == 'LIMIT_ROTATION' and c.name.startswith("Auto_Limit_Rotation"):
                pb.constraints.remove(c)
                reset_count += 1
    print(f"リセット処理完了: {reset_count} 個の古い可動域制限を完全に削除しました。")

    # 各部位ごとの回転制限（度数法）: [最小X, 最大X, 最小Y, 最大Y, 最小Z, 最大Z]
    # ※ リグの軸設定（Local Spaceの軸の向き）によって適切な角度は異なります。
    # ここでは一般的なVRM等における標準的なXYZ可動域を想定しています。
    limits_dict = {
        # === 体幹 (Spine, Chest, Neck, Head) ===
        "Hips": [-30, 30, -30, 30, -30, 30],
        "Spine": [-20, 20, -20, 20, -20, 20],
        "Chest": [-20, 20, -20, 20, -20, 20],
        "UpperChest": [-20, 20, -20, 20, -20, 20],
        "Neck": [-45, 45, -45, 45, -45, 45],
        "Head": [-45, 45, -45, 45, -60, 60],
        
        # === 腕 (Arms) ===
        "Shoulder": [-30, 30, -20, 20, -20, 20],
        "UpperArm": [-90, 90, -45, 90, -90, 90],
        "LowerArm": [0, 150, 0, 0, 0, 0], # 肘の曲がる方向を逆に変更
        "Hand": [-90, 90, -45, 45, -45, 45],
        
        # === 脚 (Legs) ===
        "UpperLeg": [-45, 120, -45, 45, -45, 45], # 前後逆に変更
        "LowerLeg": [-150, 0, 0, 0, 0, 0], # 膝の曲がる方向を逆に変更
        "Foot": [-45, 45, -20, 20, -20, 20],
        "ToeBase": [-45, 45, 0, 0, 0, 0],
        
        # === 指 (Fingers) ===
        "Thumb": [-45, 45, -45, 45, -45, 45],
        "Index": [0, 0, 0, 0, -10, 90], # 指の曲がる方向を上下逆に変更
        "Middle": [0, 0, 0, 0, -10, 90],
        "Ring": [0, 0, 0, 0, -10, 90],
        "Little": [0, 0, 0, 0, -10, 90],
    }

    applied_count = 0

    # すべてのポーズボーンをループしてコンストレイントを設定
    for pb in obj.pose.bones:
        # J_Bip_ (体用基準ボーン) 以外のボーン（J_Sec_などの揺れモノ・補助ボーン、Root等）は完全に処理から除外
        if "J_Bip_" not in pb.name:
            continue

        limit = None
        
        # ボーン名から該当する部位の制限を検索
        for key, val in limits_dict.items():
            if key in pb.name:
                limit = val
                break
        
        # マッチする部位があった場合、制限を適用
        if limit:
            # 毎回リセットですべて消しているため、常に新規作成する
            constraint = pb.constraints.new(type='LIMIT_ROTATION')
            constraint.name = "Auto_Limit_Rotation"
            
            # 関節のローカル座標(Local Space)で回転を制限する設定
            constraint.owner_space = 'LOCAL'
            
            # X軸の制限
            constraint.use_limit_x = True
            constraint.min_x = math.radians(limit[0])
            constraint.max_x = math.radians(limit[1])
            
            # Y軸の制限
            constraint.use_limit_y = True
            constraint.min_y = math.radians(limit[2])
            constraint.max_y = math.radians(limit[3])
            
            # Z軸の制限
            constraint.use_limit_z = True
            constraint.min_z = math.radians(limit[4])
            constraint.max_z = math.radians(limit[5])

            applied_count += 1
            # print(f"[{pb.name}] に可動域制限を適用しました: {limit}")

    print(f"完了: 合計 {applied_count} 個のボーンに可動域制限を設定しました。")

if __name__ == "__main__":
    setup_bone_rotation_limits()
