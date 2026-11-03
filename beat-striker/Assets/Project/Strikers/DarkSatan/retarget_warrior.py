"""Transfer observed Warrior bone directions to the Demon King.

Warrior.fbx is the animation source referenced by MLSatan.prefab; Warrior.blend
contains the same named authoring actions and provides its undeformed rest rig.
This script samples those actions, then builds new editable actions on the
Demon King rig.  It does not modify or export the source character.
"""
from pathlib import Path
import math
import sys
import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from fbx_export_utils import export_fbx
ROOT = HERE.parents[4]
SOURCE = ROOT / 'beat-striker/Assets/Project/Strikers/LargeSatan/Warrior.blend'
TARGET = HERE / 'DemonKing.blend'
SOURCES = {
    'Idle': ('待機戦士ループ', 26),
    'JumpUp': ('ジャンプ上戦士', 9),
    'Fall': ('落下戦士ループ', 13),
    'Recovery': ('待機臨戦戦士ループ', 26),
}
MAP = [
    ('Hips','J_Bip_C_Hips'), ('Spine','J_Bip_C_Spine'),
    ('Chest','J_Bip_C_UpperChest'), ('Neck','J_Bip_C_Neck'), ('Head','J_Bip_C_Head'),
    ('UpperArm.L','J_Bip_L_UpperArm'), ('LowerArm.L','J_Bip_L_LowerArm'), ('Hand.L','J_Bip_L_Hand'),
    ('UpperArm.R','J_Bip_R_UpperArm'), ('LowerArm.R','J_Bip_R_LowerArm'), ('Hand.R','J_Bip_R_Hand'),
    ('UpperLeg.L','J_Bip_L_UpperLeg'), ('LowerLeg.L','J_Bip_L_LowerLeg'), ('Foot.L','J_Bip_L_Foot'),
    ('UpperLeg.R','J_Bip_R_UpperLeg'), ('LowerLeg.R','J_Bip_R_LowerLeg'), ('Foot.R','J_Bip_R_Foot'),
]

bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
source = bpy.data.objects['S']
source.animation_data.use_nla = False
samples = {}
for output_name, (source_name, end) in SOURCES.items():
    source.animation_data.action = bpy.data.actions[source_name]
    poses=[]
    for frame in range(1,end+1):
        bpy.context.scene.frame_set(frame)
        row={}
        for dst, src in MAP:
            b=source.pose.bones[src]
            row[dst] = tuple((b.tail-b.head).normalized())
        # Warrior's idle intentionally turns side-on. Preserve that heading,
        # which is not recoverable from the near-vertical spine direction alone.
        left=source.pose.bones['J_Bip_L_UpperArm'].head
        right=source.pose.bones['J_Bip_R_UpperArm'].head
        shoulder=left-right
        row['yaw']=math.atan2(shoulder.y,shoulder.x)
        row['hip_z']=source.pose.bones['J_Bip_C_Hips'].head.z
        head=source.pose.bones['J_Bip_C_Head']
        # A bone direction alone loses twist. Warrior turns its head toward the
        # opponent even while its torso is side-on in the idle pose.
        head_delta=head.matrix.to_3x3() @ head.bone.matrix_local.to_3x3().inverted()
        row['head_rotation']=tuple(head_delta.to_quaternion())
        poses.append(row)
    samples[output_name]=poses
    print('SAMPLED',output_name,ascii(source_name),len(poses),flush=True)

bpy.ops.wm.open_mainfile(filepath=str(TARGET))
bpy.context.preferences.filepaths.save_version = 0
scene=bpy.context.scene
# Warrior.blend and Warrior.fbx both declare 24 fps. Preserve source timing.
scene.render.fps=24
rig=bpy.data.objects['DemonKing_Rig']
rig.animation_data_clear()
rig.animation_data_create()

# Smooth the actual elbow and knee boundaries, rather than rotating every
# garment piece as a completely rigid chunk. Details, eyes and mic stay rigid.
def add_joint_blend(obj, primary, secondary, joint, axis, reach):
    if primary not in obj.vertex_groups: return
    a=obj.vertex_groups[primary]
    b=obj.vertex_groups.get(secondary) or obj.vertex_groups.new(name=secondary)
    axis=Vector(axis).normalized(); joint=Vector(joint)
    # Primary weight tends to 1 on its own side and 0.5 at the joint.
    for v in obj.data.vertices:
        point=obj.matrix_world @ v.co
        t=(point-joint).dot(axis)
        w=max(0.0,min(0.5,(t+reach)/(2*reach)*0.5))
        if w>0.00001:
            a.add([v.index],1-w,'REPLACE'); b.add([v.index],w,'REPLACE')

for side,sign in [('L',1),('R',-1)]:
    elbow=(sign*.58,0,1.76)
    arm_axis=(sign*.18,0,-.32)
    for part in ['upper sleeve','shoulder']:
        obj=bpy.data.objects.get(side+' | '+part)
        if obj: add_joint_blend(obj,'UpperArm.'+side,'LowerArm.'+side,elbow,arm_axis,.15)
    for part in ['forearm sleeve','elbow']:
        obj=bpy.data.objects.get(side+' | '+part)
        if obj: add_joint_blend(obj,'LowerArm.'+side,'UpperArm.'+side,elbow,tuple(-v for v in arm_axis),.15)
    knee=(sign*.19,0,.88)
    obj=bpy.data.objects.get(side+' | fitted trousers')
    if obj: add_joint_blend(obj,'UpperLeg.'+side,'LowerLeg.'+side,knee,(0,0,-1),.12)
    obj=bpy.data.objects.get(side+' | boot shaft')
    if obj: add_joint_blend(obj,'LowerLeg.'+side,'UpperLeg.'+side,knee,(0,0,1),.12)

for pb in rig.pose.bones:
    pb.rotation_mode='QUATERNION'

def reset_pose():
    for pb in rig.pose.bones:
        pb.location=(0,0,0)
        pb.rotation_quaternion=(1,0,0,0)
        pb.scale=(1,1,1)

def aim(name, direction):
    pb=rig.pose.bones[name]
    bpy.context.view_layer.update()
    head=pb.head.copy()
    current=(pb.tail-pb.head).normalized()
    target=Vector(direction).normalized()
    delta=current.rotation_difference(target).to_matrix().to_4x4()
    pb.matrix=Matrix.Translation(head) @ delta @ Matrix.Translation(-head) @ pb.matrix
    bpy.context.view_layer.update()

def lowest_foot():
    deps=bpy.context.evaluated_depsgraph_get()
    z=[]
    for side in ('L','R'):
        ob=bpy.data.objects[side+' | boot foot'].evaluated_get(deps)
        z.extend((ob.matrix_world @ Vector(c)).z for c in ob.bound_box)
    return min(z)

created={}
for action_name, rows in samples.items():
    action=bpy.data.actions.new('WarriorRef_'+action_name)
    action.use_fake_user=True
    rig.animation_data.action=action
    rig.animation_data.action_blend_type='REPLACE'
    for frame,row in enumerate(rows,1):
        reset_pose()
        root=rig.pose.bones['Root']
        root.matrix=Matrix.Rotation(row['yaw'],4,'Z') @ root.bone.matrix_local
        bpy.context.view_layer.update()
        for name,_ in MAP:
            aim(name,row[name])
        head=rig.pose.bones['Head']
        head_q=Quaternion(row['head_rotation']) @ head.bone.matrix_local.to_quaternion()
        head.matrix=Matrix.Translation(head.head.copy()) @ head_q.to_matrix().to_4x4()
        bpy.context.view_layer.update()
        # Grounded poses follow the source's subtle vertical motion while
        # keeping shoes at floor height. Air-state travel belongs to physics.
        if action_name in ('Idle','Recovery'):
            root.location.y -= lowest_foot()
        else:
            root.location.y += (row['hip_z']-rows[0]['hip_z'])*1.9
        for pb in rig.pose.bones:
            pb.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=pb.name)
        root.keyframe_insert(data_path='location',frame=frame,group=root.name)
    created[action_name]=action
    print('RETARGETED',action_name,len(rows),flush=True)

# Each FBX contains one clip and the entire separately editable rigged model.
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
for obj in bpy.data.collections['DEMON KING | Character'].objects:
    if obj.type=='MESH': obj.select_set(True)
bpy.context.view_layer.objects.active=rig
for action in list(bpy.data.actions):
    if action not in created.values():
        bpy.data.actions.remove(action)
destination=ROOT/'beat-striker/Assets/Project/Strikers/DarkSatan/DemonKing_WarriorRef_All.fbx'
destination.parent.mkdir(parents=True,exist_ok=True)
export_fbx(filepath=str(destination),
    use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,
    bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,axis_forward='-Z',axis_up='Y')
rig.animation_data.action=created['Idle']
scene.frame_start=1;scene.frame_end=26;scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active=None
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'DemonKing_WarriorRef.blend'))
print('DONE')
