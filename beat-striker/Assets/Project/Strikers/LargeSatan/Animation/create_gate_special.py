"""Create the Warrior's editable left-hand summon and release actions.

Run with Blender in background mode. The original Warrior.blend is read only;
the two actions are saved together and exported as an animation-only FBX.
"""
from pathlib import Path
import math
import bpy
from mathutils import Matrix, Quaternion, Vector
from io_scene_fbx import export_fbx_bin

HERE = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE.parent / 'Warrior.blend'))
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.render.fps = 24
rig = bpy.data.objects['S']
if bpy.context.object is not None and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
rig.animation_data.use_nla = False
rig.animation_data.action = bpy.data.actions['待機臨戦戦士ループ']
scene.frame_set(1)
bpy.context.view_layer.update()
bones = sorted(rig.pose.bones, key=lambda b: len(b.parent_recursive))
base_matrices = {b.name: b.matrix.copy() for b in bones}
rig.animation_data.action = bpy.data.actions['落下戦士ループ']
scene.frame_set(1)
bpy.context.view_layer.update()
fall_matrices = {b.name: b.matrix.copy() for b in bones}
rig.animation_data_clear()
for b in bones:
    for constraint in list(b.constraints):
        b.constraints.remove(constraint)
    b.rotation_mode = 'QUATERNION'
for b in bones:
    b.matrix = base_matrices[b.name]
    bpy.context.view_layer.update()
base = {b.name: (b.location.copy(), b.rotation_quaternion.copy(), b.scale.copy()) for b in bones}
for b in bones:
    b.matrix = fall_matrices[b.name]
    bpy.context.view_layer.update()
leg_names = [b.name for b in bones if b.name.startswith(('J_Bip_L_', 'J_Bip_R_'))
             and any(part in b.name for part in ('UpperLeg', 'LowerLeg', 'Foot', 'ToeBase'))]
fall_legs = {name: (rig.pose.bones[name].location.copy(),
                    rig.pose.bones[name].rotation_quaternion.copy(),
                    rig.pose.bones[name].scale.copy()) for name in leg_names}
for obj in list(bpy.data.objects):
    if obj != rig and obj.name not in ('Body', 'Face', 'Hair'):
        bpy.data.objects.remove(obj, do_unlink=True)
for action in list(bpy.data.actions):
    bpy.data.actions.remove(action)
rig.animation_data_create()


def reset():
    for b in bones:
        loc, rot, scale = base[b.name]
        b.location, b.rotation_quaternion, b.scale = loc.copy(), rot.copy(), scale.copy()
    bpy.context.view_layer.update()


def aim(name, direction):
    b = rig.pose.bones[name]
    delta = (b.tail - b.head).normalized().rotation_difference(Vector(direction).normalized())
    pivot = b.head.copy()
    b.matrix = Matrix.Translation(pivot) @ delta.to_matrix().to_4x4() @ Matrix.Translation(-pivot) @ b.matrix
    bpy.context.view_layer.update()


def mix(a, b, t):
    return Vector(a).lerp(Vector(b), t)


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


wind_upper = (0.35, 0.42, 0.85)
wind_lower = (-0.3, 0.05, 0.95)
wind_hand = (-0.1, -0.2, 0.9)
reach_upper = (0.14, -1.0, 0.10)
reach_lower = (-0.10, -1.0, 0.04)
reach_hand = (0.0, -1.0, 0.0)
actions = []
for name, end in [('WarriorGateSummon', 89), ('WarriorGateRelease', 21)]:
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data.action = action
    previous_rotations = {}
    for frame in range(1, end + 1):
        scene.frame_set(frame)
        reset()
        if name.endswith('Summon'):
            t = smooth((frame - 1) / 19)
            elevation = math.radians(-65 + 140 * t)
            sweep = -0.8 + 2.0 * t
            arc = (math.cos(elevation) * math.cos(sweep),
                   math.cos(elevation) * math.sin(sweep), math.sin(elevation))
            settle = smooth((t - 0.7) / 0.3)
            upper = mix(arc, wind_upper, settle)
            lower = mix((0.25, -0.85, 0.35), wind_lower, t)
            hand = mix((0.0, -1.0, 0.0), wind_hand, t)
        else:
            t = smooth((frame - 1) / 3)
            upper = mix(wind_upper, reach_upper, t)
            lower = mix(wind_lower, reach_lower, t)
            hand = mix(wind_hand, reach_hand, t)
        leg_blend = t if name.endswith('Summon') else 1.0
        for bone_name in leg_names:
            b = rig.pose.bones[bone_name]
            loc, rot, scale = base[bone_name]
            fall_loc, fall_rot, fall_scale = fall_legs[bone_name]
            b.location = loc.lerp(fall_loc, leg_blend)
            b.rotation_quaternion = rot.slerp(fall_rot, leg_blend)
            b.scale = scale.lerp(fall_scale, leg_blend)
        bpy.context.view_layer.update()
        aim('J_Bip_L_UpperArm', upper)
        aim('J_Bip_L_LowerArm', lower)
        aim('J_Bip_L_Hand', hand)
        # The right hand keeps the held weapon close to the body throughout.
        aim('J_Bip_R_UpperArm', (-0.3, 0.05, -0.95))
        aim('J_Bip_R_LowerArm', (0.05, -0.8, 0.6))
        aim('J_Bip_R_Hand', (0.0, -0.9, 0.3))
        # Open the left hand instead of retaining the idle weapon-grip fingers.
        for b in bones:
            if b.name.startswith('J_Bip_L_') and any(s in b.name for s in ('Index', 'Middle', 'Ring', 'Little', 'Thumb')):
                b.rotation_quaternion = Quaternion()
        if name.endswith('Summon'):
            # One grounded pirouette around the pelvis, ending in the release heading.
            pivot = rig.pose.bones['J_Bip_C_Hips'].head.copy()
            pivot.z = 0.0
            turn = Matrix.Translation(pivot) @ Matrix.Rotation(2 * math.pi * t, 4, 'Z') @ Matrix.Translation(-pivot)
            for b in bones:
                if b.parent is None:
                    b.matrix = turn @ b.matrix
            bpy.context.view_layer.update()
        for b in bones:
            if b.name in previous_rotations and b.rotation_quaternion.dot(previous_rotations[b.name]) < 0:
                b.rotation_quaternion.negate()
            previous_rotations[b.name] = b.rotation_quaternion.copy()
            b.keyframe_insert(data_path='location', frame=frame, group=b.name)
            b.keyframe_insert(data_path='rotation_quaternion', frame=frame, group=b.name)
            b.keyframe_insert(data_path='scale', frame=frame, group=b.name)
    actions.append(action)
    print('ACTION', name, end, flush=True)

bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
# Do not record a machine-specific native authoring path inside the FBX.
original_compound = export_fbx_bin.elem_props_compound


def compound(element, name, *args, **kwargs):
    setter = original_compound(element, name, *args, **kwargs)
    def property_setter(kind, key, value, *extra, **options):
        if name == b'Original' and key == b'ApplicationNativeFile':
            value = ''
        return setter(kind, key, value, *extra, **options)
    return property_setter


export_fbx_bin.elem_props_compound = compound
try:
    bpy.ops.export_scene.fbx(filepath=str(HERE / 'WarriorGateSpecial.fbx'), use_selection=True,
        object_types={'ARMATURE'}, add_leaf_bones=False, bake_anim=True,
        bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, axis_forward='-Z', axis_up='Y', path_mode='STRIP')
finally:
    export_fbx_bin.elem_props_compound = original_compound
rig.animation_data.action = actions[0]
scene.frame_start = 1
scene.frame_end = 89
scene.frame_set(18)
for text in list(bpy.data.texts):
    bpy.data.texts.remove(text)
bpy.data.orphans_purge(do_recursive=True)
bpy.ops.file.pack_all()
for image in bpy.data.images:
    if image.filepath:
        image.filepath_raw = '//' + image.name + '.png'
bpy.ops.wm.save_as_mainfile(filepath=str(HERE / 'WarriorGateSpecial.blend'), compress=True)
print('SAVED', flush=True)
