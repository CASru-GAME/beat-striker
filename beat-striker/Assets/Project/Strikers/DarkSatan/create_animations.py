"""Create separate editable idle and jump actions for the Demon King rig."""
from pathlib import Path
import math
import sys
import bpy

here = Path(__file__).resolve().parent
sys.path.insert(0, str(here))
from fbx_export_utils import export_fbx
bpy.ops.wm.open_mainfile(filepath=str(here / 'DemonKing.blend'))
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.render.fps = 30
rig = bpy.data.objects['DemonKing_Rig']
rig.animation_data_clear()

def reset():
    for b in rig.pose.bones:
        b.rotation_mode = 'XYZ'
        b.rotation_euler = (0, 0, 0)
        b.location = (0, 0, 0)

def key(frame, rotations, root_z=0):
    reset()
    # Root's local Y points up along the vertical rest bone.
    rig.pose.bones['Root'].location.y = root_z
    for name, angles in rotations.items():
        rig.pose.bones[name].rotation_euler = tuple(math.radians(a) for a in angles)
    for b in rig.pose.bones:
        b.keyframe_insert(data_path='rotation_euler', frame=frame, group=b.name)
    rig.pose.bones['Root'].keyframe_insert(data_path='location', frame=frame, group='Root')

def start(name):
    rig.animation_data_create()
    rig.animation_data.action = bpy.data.actions.new(name)

start('Idle')
key(1, {'UpperArm.R':(0,0,-8), 'LowerArm.R':(10,0,0)})
key(16, {'Spine':(2,0,1), 'Chest':(-2,0,-1), 'Head':(0,0,-2), 'UpperArm.R':(0,0,-10), 'LowerArm.R':(13,0,0), 'UpperArm.L':(0,0,-2)}, .018)
key(31, {'UpperArm.R':(0,0,-8), 'LowerArm.R':(10,0,0)})
key(46, {'Spine':(-2,0,-1), 'Chest':(2,0,1), 'Head':(0,0,2), 'UpperArm.R':(0,0,-6), 'LowerArm.R':(8,0,0), 'UpperArm.L':(0,0,2)}, .009)
key(61, {'UpperArm.R':(0,0,-8), 'LowerArm.R':(10,0,0)})
idle=rig.animation_data.action
idle.use_fake_user=True

start('Jump')
key(1,{})
key(7,{'Spine':(5,0,0),'UpperLeg.L':(-12,0,0),'UpperLeg.R':(-12,0,0),'LowerLeg.L':(20,0,0),'LowerLeg.R':(20,0,0),'UpperArm.L':(0,0,8),'UpperArm.R':(0,0,-8)},-.08)
key(13,{'Spine':(-5,0,0),'UpperArm.L':(0,0,-20),'UpperArm.R':(0,0,20),'LowerArm.L':(15,0,0),'LowerArm.R':(15,0,0)},.34)
key(22,{'Spine':(-3,0,0),'UpperLeg.L':(18,0,0),'UpperLeg.R':(12,0,0),'LowerLeg.L':(-25,0,0),'LowerLeg.R':(-18,0,0),'UpperArm.L':(0,0,-16),'UpperArm.R':(0,0,16)},.68)
key(32,{'UpperLeg.L':(10,0,0),'UpperLeg.R':(8,0,0),'LowerLeg.L':(-12,0,0),'LowerLeg.R':(-10,0,0),'UpperArm.L':(0,0,-8),'UpperArm.R':(0,0,8)},.32)
key(39,{'Spine':(5,0,0),'UpperLeg.L':(-10,0,0),'UpperLeg.R':(-10,0,0),'LowerLeg.L':(16,0,0),'LowerLeg.R':(16,0,0)},-.06)
key(48,{})
jump=rig.animation_data.action
jump.use_fake_user=True

# Export one action per FBX, with baked per-frame bone transforms.
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
for obj in bpy.data.collections['DEMON KING | Character'].objects:
    if obj.type=='MESH': obj.select_set(True)
bpy.context.view_layer.objects.active=rig
for action, end in [(idle,61),(jump,48)]:
    rig.animation_data.action=action
    scene.frame_start=1; scene.frame_end=end; scene.frame_set(1)
    export_fbx(filepath=str(here/('DemonKing_'+action.name+'.fbx')),
        use_selection=True, object_types={'ARMATURE','MESH'}, add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, axis_forward='-Z', axis_up='Y')

rig.animation_data.action=idle
scene.frame_start=1; scene.frame_end=61; scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(here/'DemonKing_animated.blend'))
print('CREATED', idle.name, jump.name)
