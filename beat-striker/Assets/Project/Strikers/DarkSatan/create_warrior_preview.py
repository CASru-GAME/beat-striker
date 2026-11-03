"""Make an inspectable demonstration of the Warrior state transitions.

The animation actions remain separate; this demo approximates Unity's mixer
and vertical physics for visualization only, not for use as root motion.
"""
from pathlib import Path
import math
import bpy
from mathutils import Quaternion, Vector

HERE=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'DemonKing_WarriorRef.blend'))
scene=bpy.context.scene
rig=bpy.data.objects['DemonKing_Rig']
names=[b.name for b in rig.pose.bones]
clips={}
for name in ['Idle','JumpUp','Fall','Recovery']:
    action=bpy.data.actions['WarriorRef_'+name]
    rig.animation_data.action=action
    values=[]
    for frame in range(1,round(action.frame_range[1])+1):
        scene.frame_set(frame)
        values.append({n:(rig.pose.bones[n].rotation_quaternion.copy(),rig.pose.bones[n].location.copy()) for n in names})
    clips[name]=values

def sample(name, frame, loop=False):
    values=clips[name]
    # The source actions are 24 fps; this demonstration is rendered at 30.
    frame=frame*24/30
    if loop: frame=frame % (len(values)-1)
    else: frame=min(max(0,frame),len(values)-1)
    first=int(frame)
    second=min(first+1,len(values)-1)
    return mix(values[first],values[second],frame-first)

def mix(a,b,amount):
    t=max(0.0,min(1.0,amount))
    return {n:(a[n][0].slerp(b[n][0],t),a[n][1].lerp(b[n][1],t)) for n in names}

# At 30 fps: 0.2 s idle->jump fade, jump state lasts 0.4 s;
# fall fades for 0.3 s; grounded -> recovery lasts 0.3 s -> idle fade 0.3 s.
# Gravity=-50 and jump velocity=38, exp decay to 1% over 0.4 s.
start_jump=32
start_fall=start_jump+12
k=math.log(100)/.4
height_at_fall=38/k*(1-math.exp(-k*.4))
velocity_at_fall=38*.01
fall_seconds=(velocity_at_fall+math.sqrt(velocity_at_fall**2+100*height_at_fall))/50
start_recovery=start_fall+round(fall_seconds*30)
start_idle=start_recovery+9
end_frame=start_idle+31
action=bpy.data.actions.new('WarriorRef_TransitionPreview')
action.use_fake_user=True
rig.animation_data.action=action
for frame in range(1,end_frame+1):
    if frame<start_jump:
        pose=sample('Idle',frame-1,True)
    elif frame<start_fall:
        j=frame-start_jump
        pose=mix(sample('Idle',frame-1,True),sample('JumpUp',j),j/6)
    elif frame<start_recovery:
        f=frame-start_fall
        pose=mix(sample('JumpUp',10),sample('Fall',f,True),f/9)
    elif frame<start_idle:
        j=frame-start_recovery
        pose=mix(sample('Fall',frame-start_fall,True),sample('Recovery',j,True),j/9)
    else:
        j=frame-start_idle
        pose=mix(sample('Recovery',9+j,True),sample('Idle',j,True),j/9)
    t=(frame-start_jump)/30
    if 0<=t<.4:
        h=38/k*(1-math.exp(-k*t))
    elif .4<=t<.4+fall_seconds:
        dt=t-.4
        h=height_at_fall+velocity_at_fall*dt-25*dt*dt
    else:
        h=0
    for n,(q,loc) in pose.items():
        b=rig.pose.bones[n]
        b.rotation_mode='QUATERNION'
        b.rotation_quaternion=q
        b.location=loc
        if n=='Root': b.location.y+=h
    for n in names:
        b=rig.pose.bones[n]
        b.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=n)
        if n=='Root': b.keyframe_insert(data_path='location',frame=frame,group=n)

# Do this as a separate pass after all pose keys exist. Re-evaluating a partly
# constructed action can replace the first contact pose with the old idle pose.
for frame in range(start_recovery,end_frame+1):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    deps=bpy.context.evaluated_depsgraph_get()
    lowest=float('inf')
    for side in ('L','R'):
        foot=bpy.data.objects[side+' | boot foot'].evaluated_get(deps)
        mesh=foot.to_mesh()
        lowest=min(lowest,*( (foot.matrix_world @ v.co).z for v in mesh.vertices ))
        foot.to_mesh_clear()
    root=rig.pose.bones['Root']
    root.location.y-=lowest
    root.keyframe_insert(data_path='location',frame=frame,group=root.name)

scene.timeline_markers.clear()
for frame,label in [(1,'Idle'),(start_jump,'JumpUp'),(start_fall,'Fall'),(start_recovery,'Grounded / Recovery'),(start_idle,'Idle')]:
    scene.timeline_markers.new(label,frame=frame)
scene.frame_start=1;scene.frame_end=end_frame;scene.render.fps=30;scene.frame_set(1)
rig.animation_data.action=action
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'DemonKing_WarriorRef_Preview.blend'))
print('TRANSITIONS',start_jump,start_fall,start_recovery,start_idle,end_frame,'fall_seconds',fall_seconds)
