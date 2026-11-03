"""Compare sampled bone directions to the Warrior source and FBX round trips."""
from pathlib import Path
import math
import bpy
from mathutils import Vector

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
source=ROOT/'beat-striker/Assets/Project/Strikers/LargeSatan/Warrior.fbx'
target=HERE/'DemonKing_WarriorRef.blend'
mapping={
 'Hips':'J_Bip_C_Hips','Spine':'J_Bip_C_Spine','Chest':'J_Bip_C_UpperChest',
 'Neck':'J_Bip_C_Neck','Head':'J_Bip_C_Head',
 **{a+'.'+s:'J_Bip_'+s+'_'+b for s in ('L','R') for a,b in [
     ('UpperArm','UpperArm'),('LowerArm','LowerArm'),('Hand','Hand'),
     ('UpperLeg','UpperLeg'),('LowerLeg','LowerLeg'),('Foot','Foot')]}
}
actions={'Idle':('待機戦士ループ',[1,13,26]),'JumpUp':('ジャンプ上戦士',[1,5,9]),
         'Fall':('落下戦士ループ',[1,7,13]),'Recovery':('待機臨戦戦士ループ',[1,13,26])}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps=24
bpy.ops.import_scene.fbx(filepath=str(source),use_anim=True)
rig=bpy.data.objects['S'];rig.animation_data.use_nla=False
expected={}
for name,(original,frames) in actions.items():
    rig.animation_data.action=bpy.data.actions['S|'+original]
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        expected[name,frame]={dst:(rig.pose.bones[src].tail-rig.pose.bones[src].head).normalized().copy() for dst,src in mapping.items()}
bpy.ops.wm.open_mainfile(filepath=str(target))
rig=bpy.data.objects['DemonKing_Rig']
for name,(_,frames) in actions.items():
    rig.animation_data.action=bpy.data.actions['WarriorRef_'+name]
    worst=(0,'',0)
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        for bone,ref in expected[name,frame].items():
            b=rig.pose.bones[bone]
            actual=(b.tail-b.head).normalized()
            degrees=math.degrees(actual.angle(ref))
            if degrees>worst[0]: worst=(degrees,bone,frame)
    print('DIRECTION_ERROR',name,worst)
    # Different head rest directions and the compression/scale of the target
    # permit a small difference; all sampled limbs should match closely.
    assert worst[0] < 5.0, (name,worst)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'beat-striker/Assets/Project/Strikers/DarkSatan/DemonKing_WarriorRef_All.fbx'))
rigs=[o for o in bpy.data.objects if o.type=='ARMATURE']
assert len(rigs)==1
assert len([o for o in bpy.data.objects if o.type=='MESH'])>=100
for name in actions:
    assert any('WarriorRef_'+name in a.name for a in bpy.data.actions), name
    print('FBX_OK',name)
print('VERIFY_OK')
