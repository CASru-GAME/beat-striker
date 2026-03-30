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
        "Hips": [-360, 360, -360, 360, -360, 360],
        "Spine": [-20, 20, -20, 20, -20, 20],
        "Chest": [-20, 20, -20, 20, -20, 20],
        "UpperChest": [-20, 20, -20, 20, -20, 20],
        "Neck": [-45, 45, -45, 45, -45, 45],
        "Head": [-45, 45, -45, 45, -60, 60],
        "Shoulder": [-30, 30, -20, 20, -20, 20],
        "UpperArm": [-90, 90, -45, 90, -90, 90],
        "LowerArm": [0, 150, -20, 180, 0, 0],
        "Hand": [-90, 90, -90, 180, -90, 90],
        "UpperLeg": [-45, 180, -45, 45, -45, 45],
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

def setup_ik_drivers(obj):
    arm_ik = ["J_Bip_L_LowerArm", "J_Bip_R_LowerArm"]
    arm_rot = ["J_Bip_L_Hand", "J_Bip_R_Hand"]
    leg_ik = ["J_Bip_L_LowerLeg", "J_Bip_R_LowerLeg"]
    leg_rot = ["J_Bip_L_Foot", "J_Bip_R_Foot"]

    def add_driver(b_name, constraint_name, prop_name):
        pb = obj.pose.bones.get(b_name)
        if not pb: return
        c = pb.constraints.get(constraint_name)
        if not c: return
        
        try:
            c.driver_remove("influence")
        except:
            pass
            
        d = c.driver_add("influence").driver
        d.type = 'AVERAGE'
        
        var = d.variables.new()
        var.name = "var"
        var.type = 'SINGLE_PROP'
        target = var.targets[0]
        target.id_type = 'OBJECT'
        target.id = obj
        target.data_path = f'auto_bone_settings.{prop_name}'

    for b_name in arm_ik: add_driver(b_name, "Auto_IK", "ik_influence_arms")
    for b_name in arm_rot: add_driver(b_name, "Auto_IK_Rot", "ik_influence_arms")
    for b_name in leg_ik: add_driver(b_name, "Auto_IK", "ik_influence_legs")
    for b_name in leg_rot: add_driver(b_name, "Auto_IK_Rot", "ik_influence_legs")


class AutoBoneSettings(bpy.types.PropertyGroup):
    ik_influence_arms: bpy.props.FloatProperty(
        name="Arm IK Influence",
        description="腕のIKの影響度",
        default=1.0,
        min=0.0,
        max=1.0,
        subtype='FACTOR'
    )
    ik_influence_legs: bpy.props.FloatProperty(
        name="Leg IK Influence",
        description="脚のIKの影響度",
        default=1.0,
        min=0.0,
        max=1.0,
        subtype='FACTOR'
    )


class AUTOBONE_OT_setup(bpy.types.Operator):
    bl_idname = "autobone.setup"
    bl_label = "Setup Auto Bone (IK, Limits)"
    bl_description = "IKや回転制限などのセットアップを実行します"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        if hasattr(context.scene, "transform_orientation_slots"):
            context.scene.transform_orientation_slots[0].type = 'LOCAL'
        l_dict = setup_bone_rotation_limits()
        setup_ik(l_dict)
        setup_finger_links()
        hide_extra_bones()
        
        obj = context.object
        if obj and obj.type == 'ARMATURE':
            setup_ik_drivers(obj)
            obj.auto_bone_settings.ik_influence_arms = 1.0
            obj.auto_bone_settings.ik_influence_legs = 1.0
        
        self.report({'INFO'}, "だーりん、今度こそ完璧！ポーズも維持してるよ♡")
        return {'FINISHED'}


class AUTOBONE_OT_bake_ik_to_fk(bpy.types.Operator):
    bl_idname = "autobone.bake_ik_to_fk"
    bl_label = "Bake IK to FK"
    bl_description = "現在のIKのポーズをFK（各ボーンのローカル回転）に焼き込み、IK影響度を0にします"
    bl_options = {'REGISTER', 'UNDO'}

    bake_target: bpy.props.StringProperty(default="ALL")

    def execute(self, context):
        obj = context.object
        if not obj or obj.type != 'ARMATURE':
            return {'CANCELLED'}

        arm_bones = [
            "J_Bip_L_UpperArm", "J_Bip_L_LowerArm", "J_Bip_L_Hand",
            "J_Bip_R_UpperArm", "J_Bip_R_LowerArm", "J_Bip_R_Hand"
        ]
        leg_bones = [
            "J_Bip_L_UpperLeg", "J_Bip_L_LowerLeg", "J_Bip_L_Foot",
            "J_Bip_R_UpperLeg", "J_Bip_R_LowerLeg", "J_Bip_R_Foot"
        ]
        
        target_bones = []
        if self.bake_target in {"ALL", "ARMS"}: target_bones.extend(arm_bones)
        if self.bake_target in {"ALL", "LEGS"}: target_bones.extend(leg_bones)

        bpy.context.view_layer.update()

        saved_matrices = {}
        for b_name in target_bones:
            pb = obj.pose.bones.get(b_name)
            if pb:
                saved_matrices[b_name] = pb.matrix.copy()

        settings = obj.auto_bone_settings
        if self.bake_target in {"ALL", "ARMS"}: settings.ik_influence_arms = 0.0
        if self.bake_target in {"ALL", "LEGS"}: settings.ik_influence_legs = 0.0
        
        bpy.context.view_layer.update()

        # 3. 退避した行列を代入し、FKポーズとして復元
        # 親から順に行列を代入し、都度updateを呼ばないと子ボーンのローカル値計算がズレる
        for b_name in target_bones:
            pb = obj.pose.bones.get(b_name)
            if pb and b_name in saved_matrices:
                # 念の為 Limit Rotation を一時的に無効化（逆算時の不要な弾きを防止）
                limit_c = pb.constraints.get("Auto_Limit_Rotation")
                orig_inf = 1.0
                if limit_c:
                    orig_inf = limit_c.influence
                    limit_c.influence = 0.0
                
                pb.matrix = saved_matrices[b_name]
                
                # 追加：位置とスケールのオフセットを強制リセット（ボーンが離れたり伸びたりするのを防ぐ）
                pb.location = (0, 0, 0)
                pb.scale = (1, 1, 1)
                
                bpy.context.view_layer.update()
                
                if limit_c:
                    limit_c.influence = orig_inf
                bpy.context.view_layer.update()

        self.report({'INFO'}, "IKのポーズをFKに焼き込みました！")
        return {'FINISHED'}


class AUTOBONE_OT_key_ik_influence(bpy.types.Operator):
    bl_idname = "autobone.key_ik_influence"
    bl_label = "Key IK Influence"
    bl_description = "IKの強度を指定値に設定し、キーフレームを自動で挿入します"
    bl_options = {'REGISTER', 'UNDO'}

    target_part: bpy.props.StringProperty(default="ALL")
    target_value: bpy.props.FloatProperty(default=1.0)

    def execute(self, context):
        obj = context.object
        if not obj or obj.type != 'ARMATURE':
            return {'CANCELLED'}

        settings = obj.auto_bone_settings
        
        if self.target_part in {"ALL", "ARMS"}:
            settings.ik_influence_arms = self.target_value
            obj.keyframe_insert(data_path='auto_bone_settings.ik_influence_arms', group="Auto Bone Settings")
            
        if self.target_part in {"ALL", "LEGS"}:
            settings.ik_influence_legs = self.target_value
            obj.keyframe_insert(data_path='auto_bone_settings.ik_influence_legs', group="Auto Bone Settings")
            
        self.report({'INFO'}, f"IK Influence {self.target_value} のキーフレームを打ちました！")
        return {'FINISHED'}


class AUTOBONE_PT_panel(bpy.types.Panel):
    bl_label = "Auto Bone Setup"
    bl_idname = "AUTOBONE_PT_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'Auto Bone'

    def draw(self, context):
        layout = self.layout
        obj = context.object
        
        if not obj or obj.type != 'ARMATURE':
            layout.label(text="アーマチュアを選択してね")
            return
            
        settings = obj.auto_bone_settings
        
        layout.operator(AUTOBONE_OT_setup.bl_idname, icon='ARMATURE_DATA')
        
        layout.separator()
        layout.label(text="IK Influence & Tools:")
        box = layout.box()
        
        # --- Arms ---
        row = box.row(align=True)
        row.prop(settings, "ik_influence_arms", text="Arms")
        op = row.operator(AUTOBONE_OT_bake_ik_to_fk.bl_idname, text="", icon='ACTION')
        op.bake_target = "ARMS"
        
        box.separator()
        
        # --- Legs ---
        row = box.row(align=True)
        row.prop(settings, "ik_influence_legs", text="Legs")
        op = row.operator(AUTOBONE_OT_bake_ik_to_fk.bl_idname, text="", icon='ACTION')
        op.bake_target = "LEGS"
        
        layout.separator()
        
        row_keys = layout.row(align=True)
        op_k1 = row_keys.operator("autobone.key_ik_influence", text="All IK=1 (Key)", icon='KEY_HLT')
        op_k1.target_part = "ALL"
        op_k1.target_value = 1.0
        op_k0 = row_keys.operator("autobone.key_ik_influence", text="All IK=0 (Key)", icon='KEY_DEHLT')
        op_k0.target_part = "ALL"
        op_k0.target_value = 0.0


classes = (
    AutoBoneSettings,
    AUTOBONE_OT_setup,
    AUTOBONE_OT_bake_ik_to_fk,
    AUTOBONE_OT_key_ik_influence,
    AUTOBONE_PT_panel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Object.auto_bone_settings = bpy.props.PointerProperty(type=AutoBoneSettings)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
    del bpy.types.Object.auto_bone_settings


if __name__ == "__main__":
    register()