import bpy
import math
import bmesh

def setup_bone_rotation_limits():
    """全ボーンに回転制限を設定（コンストレイントのみ追加）"""
    obj = bpy.context.object
    if not obj or obj.type != 'ARMATURE':
        return None

    bpy.ops.object.mode_set(mode='POSE')

    # リセット処理（このスクリプトで付けたものだけ削除）
    for pb in obj.pose.bones:
        for c in list(pb.constraints):
            if c.type == 'LIMIT_ROTATION' and c.name.startswith("Auto_Limit_Rotation"):
                pb.constraints.remove(c)

    limits_dict = {
        "Hips": [-30, 30, -180, 180, -30, 30],
        "Spine": [-20, 20, -20, 20, -20, 20],
        "Chest": [-20, 20, -20, 20, -20, 20],
        "UpperChest": [-20, 20, -20, 20, -20, 20],
        "Neck": [-45, 45, -45, 45, -45, 45],
        "Head": [-45, 45, -45, 45, -60, 60],
        "Shoulder": [-30, 30, -20, 20, -20, 20],
        "UpperArm": [-90, 90, -45, 90, -90, 90],
        "LowerArm": [0, 150, -20, 180, 0, 0],
        "Hand": [-90, 90, -90, 180, -90, 90],
        "UpperLeg": [-45, 120, -45, 45, -45, 45],
        "LowerLeg": [-150, 0, 0, 0, 0, 0],
        "Foot": [-40, 20, -15, 15, -15, 15],
        "ToeBase": [-45, 45, 0, 0, 0, 0],
        "Thumb2": [-10, 90, 0, 0, 0, 0],
        "Thumb3": [-10, 90, 0, 0, 0, 0],
        "Thumb": [-45, 70, -100, 70, -30, 30],
        "Index": [0, 0, 0, 0, -10, 90],
        "Middle": [0, 0, 0, 0, -10, 90],
        "Ring": [0, 0, 0, 0, -10, 90],
        "Little": [0, 0, 0, 0, -10, 90],
    }

    for pb in obj.pose.bones:
        if "J_Bip_" not in pb.name: continue
        limit = next((v for k, v in limits_dict.items() if k in pb.name), None)
        if limit:
            c = pb.constraints.new(type='LIMIT_ROTATION')
            c.name, c.owner_space = "Auto_Limit_Rotation", 'LOCAL'
            l_min_z, l_max_z = (-limit[5], -limit[4]) if "_L_" in pb.name else (limit[4], limit[5])
            c.use_limit_x, c.min_x, c.max_x = True, math.radians(limit[0]), math.radians(limit[1])
            c.use_limit_y, c.min_y, c.max_y = True, math.radians(limit[2]), math.radians(limit[3])
            c.use_limit_z, c.min_z, c.max_z = True, math.radians(l_min_z), math.radians(l_max_z)
            # IK用設定
            pb.use_ik_limit_x, pb.ik_min_x, pb.ik_max_x = True, c.min_x, c.max_x
            pb.use_ik_limit_y, pb.ik_min_y, pb.ik_max_y = True, c.min_y, c.max_y
            pb.use_ik_limit_z, pb.ik_min_z, pb.ik_max_z = True, c.min_z, c.max_z
    return limits_dict

def create_cube_shape(name="WG_Cube"):
    if name in bpy.data.objects: return bpy.data.objects[name]
    mesh = bpy.data.meshes.new(name+"_Mesh")
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bm.to_mesh(mesh)
    bm.free()
    obj.hide_viewport = obj.hide_render = True
    return obj

def setup_ik(limits_dict):
    """ポーズを維持したままIKを設定 (Blender 4.0+ 対応)"""
    obj = bpy.context.object
    bpy.ops.object.mode_set(mode='EDIT')
    amt = obj.data
    
    ik_map = {
        "J_Bip_L_LowerLeg": ("IK_L_Foot", "J_Bip_L_Foot", 2, 'THEME01'), # 赤
        "J_Bip_R_LowerLeg": ("IK_R_Foot", "J_Bip_R_Foot", 2, 'THEME01'),
        "J_Bip_L_LowerArm": ("IK_L_Hand", "J_Bip_L_Hand", 2, 'THEME04'), # 青
        "J_Bip_R_LowerArm": ("IK_R_Hand", "J_Bip_R_Hand", 2, 'THEME04'),
    }
    
    targets_info = []
    for joint_n, (ik_n, end_n, c_len, theme) in ik_map.items():
        if joint_n in amt.edit_bones and end_n in amt.edit_bones:
            end_eb = amt.edit_bones[end_n]
            if ik_n not in amt.edit_bones:
                ik_eb = amt.edit_bones.new(ik_n)
                ik_eb.head, ik_eb.roll = end_eb.head, end_eb.roll
                ik_eb.tail = end_eb.head + (end_eb.tail - end_eb.head) * 1.2
                ik_eb.parent = amt.edit_bones.get("Root")
                ik_eb.use_deform = False
            targets_info.append((joint_n, ik_n, end_n, c_len, theme))

    bpy.ops.object.mode_set(mode='POSE')
    shape_obj = create_cube_shape()
    
    for joint_n, ik_n, end_n, c_len, theme in targets_info:
        joint_pb, ik_pb, end_pb = obj.pose.bones.get(joint_n), obj.pose.bones.get(ik_n), obj.pose.bones.get(end_n)
        if not (joint_pb and ik_pb and end_pb): continue

        # 今のポーズにIKをスナップ
        ik_pb.matrix = end_pb.matrix.copy()
        ik_pb.custom_shape = shape_obj

        # --- カラー設定 (ここがエラーの原因だったよ！) ---
        if hasattr(ik_pb, "color"): 
            # Blender 4.0以降
            ik_pb.color.palette = theme
        else:
            # Blender 3.6以前
            grp_name = f"IK_{theme}"
            grp = obj.pose.bone_groups.get(grp_name) or obj.pose.bone_groups.new(name=grp_name)
            grp.color_set = theme
            ik_pb.bone_group = grp

        # IK適用
        for c in list(joint_pb.constraints):
            if c.type == 'IK' and c.name == "Auto_IK": joint_pb.constraints.remove(c)
        ik_c = joint_pb.constraints.new('IK')
        ik_c.name, ik_c.target, ik_c.subtarget, ik_c.chain_count = "Auto_IK", obj, ik_n, c_len
        
        # 回転コピー
        for c in list(end_pb.constraints):
            if c.type == 'COPY_ROTATION' and c.name == "Auto_IK_Rot": end_pb.constraints.remove(c)
        rot_c = end_pb.constraints.new('COPY_ROTATION')
        rot_c.name, rot_c.target, rot_c.subtarget = "Auto_IK_Rot", obj, ik_n
        rot_c.target_space = rot_c.owner_space = 'WORLD'

        # 制限を最後に
        for i, c in enumerate(end_pb.constraints):
            if c.name == "Auto_Limit_Rotation":
                end_pb.constraints.move(i, len(end_pb.constraints) - 1)
                break

def setup_finger_links():
    obj = bpy.context.object
    bpy.ops.object.mode_set(mode='POSE')
    for s in ["L", "R"]:
        for f in ["Index", "Middle", "Ring", "Little"]:
            pb2, pb3 = obj.pose.bones.get(f"J_Bip_{s}_{f}2"), obj.pose.bones.get(f"J_Bip_{s}_{f}3")
            if pb2 and pb3:
                for c in list(pb3.constraints):
                    if c.name == "Auto_Finger_Link": pb3.constraints.remove(c)
                c = pb3.constraints.new('COPY_ROTATION')
                c.name, c.target, c.subtarget, c.influence = "Auto_Finger_Link", obj, pb2.name, 0.8
                c.target_space = c.owner_space = 'LOCAL'

def hide_extra_bones():
    amt = bpy.context.object.data
    for b in amt.bones:
        b.hide = not ("J_Bip_" in b.name or "IK_" in b.name or b.name == "Root")

def main():
    if hasattr(bpy.context.scene, "transform_orientation_slots"):
        bpy.context.scene.transform_orientation_slots[0].type = 'LOCAL'
    l_dict = setup_bone_rotation_limits()
    setup_ik(l_dict)
    setup_finger_links()
    hide_extra_bones()
    print("だーりん、今度こそ完璧！ポーズも維持してるよ♡")

if __name__ == "__main__":
    main()