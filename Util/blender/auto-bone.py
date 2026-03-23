import bpy
import math
import bmesh

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
        "LowerArm": [0, 150, -20, 180, 0, 0], # 肘の曲がる方向を逆に変更
        "Hand": [-30, 30, -70, 180, -70, 70],
        
        # === 脚 (Legs) ===
        "UpperLeg": [-45, 120, -45, 45, -45, 45], # 前後逆に変更
        "LowerLeg": [-150, 0, 0, 0, 0, 0], # 膝の曲がる方向を逆に変更
        "Foot": [-40, 20, -15, 15, -15, 15], # X-はつま先下げ、X+はつま先上げ。Y/Zはひねり。
        "ToeBase": [-45, 45, 0, 0, 0, 0],
        
        # === 指 (Fingers) ===
        "Thumb2": [-10, 90, 0, 0, 0, 0], # X軸は左右で共通向きなので元に戻す
        "Thumb3": [-10, 90, 0, 0, 0, 0],
        "Thumb": [-45, 70, -70, 30, -30, 30], # 根本(Thumb1など)は自由に動く
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
            
            # 基準となっているボーン（プラスマイナス）が実は右側（_R_）の向きで書かれていた可能性があるため、
            # 左側（_L_）のボーンのX・Z軸を反転させるように修正します。
            l_min_x, l_max_x = limit[0], limit[1]
            l_min_y, l_max_y = limit[2], limit[3]
            l_min_z, l_max_z = limit[4], limit[5]
            
            if "_L_" in pb.name:
                # 指のZ軸などは左右対称（反転）が必要ですが、
                # 腕や脚のX軸は左右で曲がる方向（プラスマイナス）が同じ設定になっているようです
                l_min_z, l_max_z = -limit[5], -limit[4]
                
                # X軸（腕、脚、そして親指）はモデリングの仕様で左右とも共通しているため反転させません
            
            constraint.use_limit_x = True
            constraint.min_x = math.radians(l_min_x)
            constraint.max_x = math.radians(l_max_x)

            constraint.use_limit_y = True
            constraint.min_y = math.radians(l_min_y)
            constraint.max_y = math.radians(l_max_y)

            constraint.use_limit_z = True
            constraint.min_z = math.radians(l_min_z)
            constraint.max_z = math.radians(l_max_z)

            # --- IKソルバー用の制限 (Inverse Kinematicsパネルの設定) を有効化 ---
            pb.use_ik_limit_x = True
            pb.ik_min_x = math.radians(l_min_x)
            pb.ik_max_x = math.radians(l_max_x)

            pb.use_ik_limit_y = True
            pb.ik_min_y = math.radians(l_min_y)
            pb.ik_max_y = math.radians(l_max_y)

            pb.use_ik_limit_z = True
            pb.ik_min_z = math.radians(l_min_z)
            pb.ik_max_z = math.radians(l_max_z)

            applied_count += 1
            # print(f"[{pb.name}] に可動域制限を適用しました: {limit}")

    print(f"完了: 合計 {applied_count} 個のボーンに可動域制限を設定しました。")
    return limits_dict

def create_cube_shape(name="WG_Cube_Red"):
    # 既に同名の形状オブジェクトがあれば再利用
    if name in bpy.data.objects:
        return bpy.data.objects[name]
    
    mesh = bpy.data.meshes.new(name+"_Mesh")
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bm.to_mesh(mesh)
    bm.free()
    
    # ビューポートやレンダリングには表示させない（ボーンの形としてのみ使うため）
    obj.hide_viewport = True
    obj.hide_render = True
    return obj

def setup_ik(limits_dict):
    """
    足首にIKボーンを生成し、赤い立方体のシェイプを割り当ててIKコンストレイントを設定します。
    """
    obj = bpy.context.object
    if not obj or obj.type != 'ARMATURE':
        print("エラー: アーマチュア（ボーン構造）オブジェクトを選択した状態で実行してください。")
        return
        
    bpy.ops.object.mode_set(mode='EDIT')
    amt = obj.data
    
    # 対象ボーン: (追従させる先ボーン, IKボーンの名前, IKチェーンの長さ)
    ik_map = {
        "J_Bip_L_LowerLeg": ("J_Bip_L_Foot", "IK_L_Foot", 2),
        "J_Bip_R_LowerLeg": ("J_Bip_R_Foot", "IK_R_Foot", 2),
    }
    
    ik_added = {}
    
    # IK用ボーンを生成
    for bone_name, (target_name, ik_name, chain_len) in ik_map.items():
        if bone_name in amt.edit_bones and target_name in amt.edit_bones:
            target_bone = amt.edit_bones[target_name]
            
            if ik_name not in amt.edit_bones:
                ik_bone = amt.edit_bones.new(ik_name)
                ik_bone.head = target_bone.head
                # ボーンサイズを少し長めにして見やすく
                ik_bone.tail = target_bone.head + (target_bone.tail - target_bone.head) * 1.5 
                # ボーンのRoll(軸の傾き)をターゲットと同じにしてXYZ軸の向きを一致させる
                ik_bone.roll = target_bone.roll
                
                # 腕や足の動きに引きずられないようにRootにペアレント（存在すれば）
                if "Root" in amt.edit_bones:
                    ik_bone.parent = amt.edit_bones["Root"]
                else:
                    ik_bone.parent = None
                    
                ik_bone.use_deform = False # メッシュを変形させない
            ik_added[bone_name] = ik_name

    bpy.ops.object.mode_set(mode='POSE')
    
    # 赤い立方体オブジェクトを作成または取得
    shape_obj = create_cube_shape()
    
    for bone_name, ik_name in ik_added.items():
        pb = obj.pose.bones.get(bone_name)
        ik_pb = obj.pose.bones.get(ik_name)
        
        if pb and ik_pb:
            # --- 形状と色を設定 ---
            ik_pb.custom_shape = shape_obj
            # THEME01 が標準の「赤色」カラーコード
            if hasattr(ik_pb, "color"):
                ik_pb.color.palette = 'THEME01'
            else:
                # Blender 3.x 等の互換用処理
                grp_name = "IK_Bones_Red"
                if grp_name not in obj.pose.bone_groups:
                    grp = obj.pose.bone_groups.new(name=grp_name)
                    grp.color_set = 'THEME01'
                ik_pb.bone_group = obj.pose.bone_groups[grp_name]
            
            # --- 古いコンストレイントをクリーンアップ ---
            for c in list(pb.constraints):
                if c.type == 'IK' and c.name == "Auto_IK":
                    pb.constraints.remove(c)
            
            # --- IKコンストレイントを追加 (LowerArm / LowerLeg に適用) ---
            ik_c = pb.constraints.new(type='IK')
            ik_c.name = "Auto_IK"
            ik_c.target = obj
            ik_c.subtarget = ik_name
            ik_c.chain_count = ik_map[bone_name][2]
            
            # --- 手・足先をIKコントローラーの回転に追従 (Copy Rotation) ---
            target_name = ik_map[bone_name][0]
            target_pb = obj.pose.bones.get(target_name)
            if target_pb:
                for c in list(target_pb.constraints):
                    if c.type == 'COPY_ROTATION' and c.name == "Auto_IK_Rot":
                        target_pb.constraints.remove(c)
                
                rot_c = target_pb.constraints.new('COPY_ROTATION')
                rot_c.name = "Auto_IK_Rot"
                rot_c.target = obj
                rot_c.subtarget = ik_name
                # 見たままの角度に追従するようにワールド空間を使用
                rot_c.target_space = 'WORLD'
                rot_c.owner_space = 'WORLD'

                # COPY_ROTATION の後に LIMIT_ROTATION が適用されるように、既存の制限を一番下に移動
                for i, c in enumerate(target_pb.constraints):
                    if c.name == "Auto_Limit_Rotation":
                        target_pb.constraints.move(i, len(target_pb.constraints) - 1)
                        break

            # --- IKコントローラー自体の回転制限（不要になったためクリーンアップのみ実行） ---
            for c in list(ik_pb.constraints):
                if c.type == 'LIMIT_ROTATION' and c.name == "Auto_IK_Limit":
                    ik_pb.constraints.remove(c)
                
    print(f"完了: IKコントローラーを {len(ik_added)} 箇所に設定しました。")

def hide_extra_bones():
    """
    体の基準ボーン (J_Bip_系)、IKコントローラー、および Root 以外の
    揺れモノや補助ボーンなどを非表示にして見た目をすっきりさせます。
    """
    obj = bpy.context.object
    if not obj or obj.type != 'ARMATURE':
        return
        
    amt = obj.data
    hidden_count = 0
    
    for bone in amt.bones:
        # 体のボーン、IKボーン、Rootボーンは残す
        is_body_bone = "J_Bip_" in bone.name or "IK_" in bone.name or bone.name == "Root"
        
        if not is_body_bone:
            bone.hide = True
            hidden_count += 1
        else:
            bone.hide = False
            
    print(f"完了: 余計なボーン {hidden_count} 個を非表示に設定しました。")

def setup_finger_links():
    """
    指の第3関節が第2関節の回転に自動的に連動するように（Copy Rotation）設定します。
    """
    obj = bpy.context.object
    if not obj or obj.type != 'ARMATURE':
        return
        
    bpy.ops.object.mode_set(mode='POSE')
    
    fingers = ["Index", "Middle", "Ring", "Little"]
    sides = ["L", "R"]
    
    linked_count = 0
    
    for side in sides:
        for finger in fingers:
            bone2_name = f"J_Bip_{side}_{finger}2"
            bone3_name = f"J_Bip_{side}_{finger}3"
            
            pb2 = obj.pose.bones.get(bone2_name)
            pb3 = obj.pose.bones.get(bone3_name)
            
            if pb2 and pb3:
                # 既存のリンク設定をクリーンアップ
                for c in list(pb3.constraints):
                    if c.type == 'COPY_ROTATION' and c.name == "Auto_Finger_Link":
                        pb3.constraints.remove(c)
                
                # Copy Rotationを追加して第2関節の曲がりに追従させる
                c = pb3.constraints.new('COPY_ROTATION')
                c.name = "Auto_Finger_Link"
                c.target = obj
                c.subtarget = bone2_name
                
                # 自分自身の軸（Local Space）でそのままコピー
                c.target_space = 'LOCAL'
                c.owner_space = 'LOCAL'
                
                # 第3関節は少しだけ曲がりを浅くする（自然な指の曲がり味付け）
                c.influence = 0.8
                
                linked_count += 1
                
    print(f"完了: 指の第3関節の連動を {linked_count} 箇所に設定しました。")


def set_transform_orientation_to_local():
    """
    トランスフォーム座標系を自動で「ローカル」に設定します。
    """
    if hasattr(bpy.context.scene, "transform_orientation_slots"):
        bpy.context.scene.transform_orientation_slots[0].type = 'LOCAL'
    print("完了: トランスフォーム座標系を「ローカル」に設定しました。")

def main():
    set_transform_orientation_to_local()
    limits_dict = setup_bone_rotation_limits()
    setup_ik(limits_dict)
    setup_finger_links()
    hide_extra_bones()

if __name__ == "__main__":
    main()
