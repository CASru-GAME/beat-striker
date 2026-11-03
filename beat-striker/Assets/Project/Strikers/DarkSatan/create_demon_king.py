"""Build the original, editable Demon King character in Blender 5.x.

Run from the repository root with Blender's --background --python option.  The
reference is the rightmost character card in SelectScene, not the placeholder
tutorial portrait.  All modelling is local and no source assets are replaced.
"""
from pathlib import Path
import math
import sys

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[5]
OUT = Path(__file__).resolve().parent
sys.path.insert(0, str(OUT))
from fbx_export_utils import export_fbx
REF = ROOT / "beat-striker/Assets/Project/Screen/CharacterSelect/a.png"
OUT.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for datablock in bpy.data.materials:
    bpy.data.materials.remove(datablock)

scene = bpy.context.scene
bpy.context.preferences.filepaths.save_version = 0
scene.render.engine = 'CYCLES'
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.render.resolution_x = 1000
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.use_stamp_filename = False
scene.world.color = (0.025, 0.025, 0.025)
scene.view_settings.view_transform = 'AgX'


def collection(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c


character_collection = collection("DEMON KING | Character")
stage_collection = collection("STUDIO | Camera and lights")
reference_collection = collection("REFERENCE | select screen artwork")


def move_to_collection(obj, target):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    target.objects.link(obj)


def material(name, rgb, metallic=0.0, roughness=0.4, subsurface=0.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*rgb, 1)
    p.inputs['Metallic'].default_value = metallic
    p.inputs['Roughness'].default_value = roughness
    p.inputs['Subsurface Weight'].default_value = subsurface
    return m


skin = material('Skin | deep rose crimson', (.38, .035, .075), roughness=.37, subsurface=.08)
ear_inner = material('Ears | inner rose', (.18, .012, .028), roughness=.45)
horn = material('Horns | ruby', (.46, .028, .055), roughness=.3)
horn_tip = material('Horns | tip highlight', (.62, .075, .1), roughness=.33)
hair = material('Hair | blue black', (.011, .015, .029), roughness=.43)
hair_strand = material('Hair | cool strand highlights', (.032, .044, .067), roughness=.40)
fabric = material('Coat | midnight charcoal', (.018, .022, .034), roughness=.59)
fabric_dark = material('Gloves and boots | soft black', (.009, .011, .018), roughness=.39)
fabric_panel = material('Vest | slate', (.07, .075, .095), roughness=.58)
fabric_seam = material('Seams | subtle blue grey', (.055, .066, .09), roughness=.48)
shirt = material('Collar | pearl ivory', (.72, .73, .69), roughness=.52)
piping = material('Piping | silver ivory', (.58, .60, .58), metallic=.18, roughness=.36)
gold = material('Trim | warm gold', (.76, .49, .055), metallic=.68, roughness=.28)
gold_dark = material('Trim | shadow gold', (.32, .17, .014), metallic=.65, roughness=.34)
white_eye = material('Eyes | warm silver white', (.72, .73, .73), roughness=.23)
eye_rim = material('Eyes | dark plum outline', (.037, .009, .021), roughness=.37)
iris = material('Eyes | smoky plum iris', (.12, .10, .13), roughness=.26)
iris_dark = material('Eyes | deep burgundy', (.024, .008, .017), roughness=.24)
iris_light = material('Eyes | rose glint', (.61, .07, .14), roughness=.28)
white = material('Eyes | catchlights', (.95, .95, .90), roughness=.19)
mouth_mat = material('Mouth | dark wine', (.045, .003, .008), roughness=.45)
mic_black = material('Microphone | body', (.018, .023, .031), metallic=.5, roughness=.33)
mic_mesh = material('Microphone | mesh', (.12, .15, .17), metallic=.8, roughness=.31)
cyan = material('Microphone | teal rings', (.015, .32, .35), metallic=.45, roughness=.27)


def armature():
    data = bpy.data.armatures.new('DemonKing_Skeleton')
    obj = bpy.data.objects.new('DemonKing_Rig', data)
    character_collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    definitions = [
        ('Root', (0,0,0), (0,0,.25), None),
        ('Hips', (0,0,1.35), (0,0,1.57), 'Root'),
        ('Spine', (0,0,1.57), (0,0,1.85), 'Hips'),
        ('Chest', (0,0,1.85), (0,0,2.12), 'Spine'),
        ('Neck', (0,0,2.12), (0,0,2.31), 'Chest'),
        ('Head', (0,0,2.31), (0,0,3.20), 'Neck'),
    ]
    for side, s in [('L',1), ('R',-1)]:
        definitions += [
            ('Shoulder.'+side, (s*.24,0,2.10), (s*.40,0,2.08), 'Chest'),
            ('UpperArm.'+side, (s*.40,0,2.08), (s*.58,0,1.76), 'Shoulder.'+side),
            ('LowerArm.'+side, (s*.58,0,1.76), (s*.73,-.015,1.47), 'UpperArm.'+side),
            ('Hand.'+side, (s*.73,-.015,1.47), (s*.79,-.025,1.30), 'LowerArm.'+side),
            ('UpperLeg.'+side, (s*.18,0,1.38), (s*.19,0,.88), 'Hips'),
            ('LowerLeg.'+side, (s*.19,0,.88), (s*.19,0,.24), 'UpperLeg.'+side),
            ('Foot.'+side, (s*.19,0,.24), (s*.19,-.28,.12), 'LowerLeg.'+side),
            ('Toe.'+side, (s*.19,-.28,.12), (s*.19,-.39,.12), 'Foot.'+side),
        ]
    for name, head, tail, parent in definitions:
        b = data.edit_bones.new(name)
        b.head, b.tail = head, tail
        if parent:
            b.parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)
    obj.show_in_front = False
    return obj


rig = armature()
parts = []


def finish(obj, name, mat, bone=None):
    obj.name = name
    move_to_collection(obj, character_collection)
    if mat:
        obj.data.materials.append(mat)
    if obj.type == 'MESH':
        for p in obj.data.polygons:
            p.use_smooth = True
    if bone:
        # All rigid ornaments have deliberately explicit weights, so the model
        # remains editable and the visible parts follow the skeleton on export.
        group = obj.vertex_groups.new(name=bone)
        group.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
        mod = obj.modifiers.new('Skin | '+bone, 'ARMATURE')
        mod.object = rig
        obj.parent = rig
        parts.append(obj)
    return obj


def sphere(name, center, scale, mat, bone, segments=40, rings=24, rot=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    ob = bpy.context.object
    ob.scale = scale
    if rot:
        ob.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(ob, name, mat, bone)


def tube(name, points, widths, depths, mat, bone, sides=18):
    """Organic tapered tube with independently controlled cross sections."""
    pts = [Vector(p) for p in points]
    verts, faces = [], []
    for i, p in enumerate(pts):
        tangent = (pts[min(i+1,len(pts)-1)] - pts[max(i-1,0)]).normalized()
        q = Vector((0,0,1)).rotation_difference(tangent)
        for j in range(sides):
            a = math.tau*j/sides
            offset = q @ Vector((widths[i]*math.cos(a), depths[i]*math.sin(a), 0))
            verts.append(tuple(p+offset))
    for i in range(len(pts)-1):
        for j in range(sides):
            a=i*sides+j; b=i*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple((len(pts)-1)*sides+j for j in range(sides)))
    mesh=bpy.data.meshes.new(name+' mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    ob=bpy.data.objects.new(name, mesh)
    character_collection.objects.link(ob)
    # Smooth the actual silhouette, not just the normals: this is especially
    # visible on the curved bangs and horn tips.
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active=ob
    ob.select_set(True)
    sub=ob.modifiers.new('Organic surface', 'SUBSURF')
    sub.levels=2
    bpy.ops.object.modifier_apply(modifier=sub.name)
    ob.select_set(False)
    return finish(ob,name,mat,bone)


def curve(name, points, radius, mat, bone, resolution=3):
    data=bpy.data.curves.new(name+' path', 'CURVE')
    data.dimensions='3D'
    data.resolution_u=16
    data.bevel_depth=radius
    data.bevel_resolution=resolution
    sp=data.splines.new('BEZIER')
    sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):
        p.co=co
        p.handle_left_type='AUTO'
        p.handle_right_type='AUTO'
    ob=bpy.data.objects.new(name,data)
    character_collection.objects.link(ob)
    # Converting any other selected mesh would silently apply its skin modifier.
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active=ob
    ob.select_set(True)
    bpy.ops.object.convert(target='MESH')
    ob=bpy.context.object
    ob.select_set(False)
    return finish(ob,name,mat,bone)


def shaped_panel(name, coords, mat, bone, thickness=.012, bevel=.012):
    mesh=bpy.data.meshes.new(name+' mesh')
    mesh.from_pydata(coords, [], [tuple(range(len(coords)))])
    mesh.update()
    ob=bpy.data.objects.new(name,mesh)
    character_collection.objects.link(ob)
    sol=ob.modifiers.new('Fabric thickness','SOLIDIFY'); sol.thickness=thickness
    bev=ob.modifiers.new('Soft tailored edge','BEVEL'); bev.width=bevel; bev.segments=3
    bpy.context.view_layer.objects.active=ob; ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier=sol.name)
    bpy.ops.object.modifier_apply(modifier=bev.name)
    ob.select_set(False)
    return finish(ob,name,mat,bone)


def torso_mesh(name, rings, mat, bone, count=48):
    verts=[]; faces=[]
    for z, rx, ry, yc in rings:
        for j in range(count):
            a=math.tau*j/count
            verts.append((rx*math.sin(a),yc-ry*math.cos(a),z))
    for i in range(len(rings)-1):
        for j in range(count):
            k=i*count+j; kn=i*count+(j+1)%count
            faces.append((k,kn,kn+count,k+count))
    faces += [tuple(reversed(range(count))), tuple((len(rings)-1)*count+j for j in range(count))]
    mesh=bpy.data.meshes.new(name+' mesh'); mesh.from_pydata(verts, [], faces); mesh.update()
    ob=bpy.data.objects.new(name,mesh); character_collection.objects.link(ob)
    bevel=ob.modifiers.new('Rounded tailoring','BEVEL'); bevel.width=.025; bevel.segments=3
    bpy.context.view_layer.objects.active=ob; ob.select_set(True); bpy.ops.object.modifier_apply(modifier=bevel.name); ob.select_set(False)
    return finish(ob,name,mat,bone)


def torus(name, center, major, minor, mat, bone, rotation=(0,0,0), scale=(1,1,1)):
    bpy.ops.mesh.primitive_torus_add(major_segments=28, minor_segments=10, location=center,
        major_radius=major, minor_radius=minor, rotation=rotation)
    ob=bpy.context.object; ob.scale=scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(ob,name,mat,bone)


def cyl_between(name, a, b, r1, r2, mat, bone, vertices=32):
    a,b=Vector(a),Vector(b); d=b-a
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=d.length, location=(a+b)/2)
    ob=bpy.context.object; ob.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    return finish(ob,name,mat,bone)


# Body, legs and individually sculpted shoes.
torso_mesh('Coat | tailored bodice', [(1.48,.285,.185,.01),(1.60,.305,.19,.01),(1.78,.29,.18,0),
                                    (1.99,.34,.19,0),(2.10,.365,.19,.01),(2.18,.29,.17,.015)], fabric, 'Spine')
sphere('Neck', (0,0,2.24), (.12,.12,.20), skin, 'Neck')
torso_mesh('Waist | belt',[(1.55,.315,.198,.005),(1.65,.31,.20,.005)],fabric_dark,'Hips')
for side, s in [('L',1),('R',-1)]:
    sphere(side+' | fitted trousers', (s*.185,0,1.13), (.165,.17,.32), fabric, 'UpperLeg.'+side)
    tube(side+' | boot shaft', [(s*.19,0,.91),(s*.19,0,.72),(s*.19,0,.38),(s*.19,-.015,.14)],
         [.15,.15,.135,.13],[.15,.145,.13,.13],fabric_dark,'LowerLeg.'+side,28)
    sphere(side+' | boot foot', (s*.19,-.105,.135), (.15,.265,.135), fabric_dark, 'Foot.'+side)
    curve(side+' | sole seam',[(s*.055,-.27,.065),(s*.10,-.37,.065),(s*.20,-.40,.065),
                              (s*.30,-.35,.065),(s*.33,-.15,.065)], .008, fabric_seam,'Foot.'+side)
    curve(side+' | boot top stitch',[(s*.06,-.075,.85),(s*.12,-.13,.85),(s*.20,-.145,.85),
                                     (s*.28,-.11,.85),(s*.32,-.06,.85)],.007,fabric_seam,'LowerLeg.'+side)

# Open-front long coat; the back and two sides are a single editable shell.
N=64
angles=[.58+(math.tau-1.16)*i/N for i in range(N+1)]
coat_rings=[(1.58,.315,.205,.02),(1.38,.36,.23,.025),(1.10,.41,.255,.04),(.83,.44,.28,.045),(.72,.445,.285,.045)]
verts=[]; faces=[]
for z,rx,ry,yc in coat_rings:
    for a in angles:
        verts.append((rx*math.sin(a), yc-ry*math.cos(a), z))
for i in range(len(coat_rings)-1):
    for j in range(N):
        k=i*(N+1)+j
        faces.append((k,k+1,k+N+2,k+N+1))
mesh=bpy.data.meshes.new('Coat tails mesh'); mesh.from_pydata(verts,[],faces); mesh.update()
ob=bpy.data.objects.new('Coat | long split tails',mesh); character_collection.objects.link(ob)
sol=ob.modifiers.new('Woven thickness','SOLIDIFY'); sol.thickness=.025
bev=ob.modifiers.new('Hem rounding','BEVEL'); bev.width=.012; bev.segments=3
bpy.context.view_layer.objects.active=ob; ob.select_set(True)
bpy.ops.object.modifier_apply(modifier=sol.name); bpy.ops.object.modifier_apply(modifier=bev.name); ob.select_set(False)
finish(ob,'Coat | long split tails',fabric,'Hips')
# A flat ribbon follows the lower edge, matching the broad gold hem in the card.
hem_verts=[]; hem_faces=[]
# The shell's thickness extends outwards; keep the ribbon above that surface.
for z,rx,ry in [(.727,.485,.325),(.785,.479,.319)]:
    hem_verts.extend((rx*math.sin(a),.045-ry*math.cos(a),z) for a in angles)
for j in range(N):
    hem_faces.append((j,j+1,j+N+2,j+N+1))
mesh=bpy.data.meshes.new('Gold hem ribbon mesh'); mesh.from_pydata(hem_verts,[],hem_faces); mesh.update()
ob=bpy.data.objects.new('Coat | woven gold hem ribbon',mesh); character_collection.objects.link(ob)
finish(ob,'Coat | woven gold hem ribbon',gold,'Hips')
for side,s in [('L',1),('R',-1)]:
    edge=[(s*rx*math.sin(.58),yc-ry*math.cos(.58)-.012,z) for z,rx,ry,yc in coat_rings]
    curve(side+' | gold coat opening',edge,.025,gold,'Hips')
    for k in range(3):
        a=.58+(math.tau-1.16)*k/3
        if s<0: a=math.tau-a
    hem=[(.445*math.sin(a),.045-.285*math.cos(a),.735) for a in angles]
    if s==1:
        curve('Coat | continuous gold hem',hem,.023,gold,'Hips',2)
for angle in [2.25,math.pi,math.tau-2.25]:
    seam=[((rx+.031)*math.sin(angle),yc-(ry+.031)*math.cos(angle),z) for z,rx,ry,yc in coat_rings[:-1]]
    curve('Coat | back tailored seam',seam,.006,fabric_seam,'Hips')

# Front of jacket with shirt, lapels, piping and belt buckle.
shaped_panel('Vest | front panel', [(-.19,-.194,2.09),(.19,-.194,2.09),(.17,-.197,1.55),
   (.18,-.205,1.21),(.11,-.211,1.18),(0,-.215,1.32),(-.11,-.211,1.18),(-.18,-.205,1.21),(-.17,-.197,1.55)],fabric_panel,'Spine')
for s in [-1,1]:
    shaped_panel(('L' if s>0 else 'R')+' | white shirt collar', [(0,-.218,2.115),(s*.085,-.222,2.16),
       (s*.18,-.20,2.13),(s*.095,-.231,2.00)],shirt,'Chest')
    shaped_panel(('L' if s>0 else 'R')+' | black lapel', [(s*.17,-.209,2.15),(s*.265,-.175,2.08),
       (s*.14,-.225,1.89),(s*.065,-.228,2.00)],fabric_dark,'Chest')
    curve(('L' if s>0 else 'R')+' | ivory vest edge',[(s*.14,-.231,1.95),(s*.16,-.225,1.69),
       (s*.17,-.222,1.45),(s*.17,-.228,1.23),(s*.11,-.23,1.20),(0,-.23,1.34)],.010,piping,'Spine')
    curve(('L' if s>0 else 'R')+' | gold upper piping',[(s*.19,-.20,2.10),(s*.225,-.184,1.97),
       (s*.22,-.19,1.79),(s*.215,-.205,1.61)],.018,gold,'Spine')
    for z in [1.79,1.89]:
        sphere(('L' if s>0 else 'R')+' | jacket button', (s*.09,-.228,z),(.019,.010,.019),gold,'Spine',20,12)
shaped_panel('Belt | gold buckle', [(-.075,-.216,1.64),(.075,-.216,1.64),(.075,-.22,1.55),(-.075,-.22,1.55)],gold,'Hips',.01,.01)
shaped_panel('Belt | dark inset', [(-.048,-.229,1.622),(.048,-.229,1.622),(.048,-.23,1.568),(-.048,-.23,1.568)],fabric_dark,'Hips',.006,.004)
curve('Collar | black ribbon left',[(-.01,-.243,2.07),(-.09,-.25,2.105),(-.105,-.24,2.055),(-.02,-.245,2.04)],.017,fabric_dark,'Chest')
curve('Collar | black ribbon right',[(.01,-.243,2.07),(.09,-.25,2.105),(.105,-.24,2.055),(.02,-.245,2.04)],.017,fabric_dark,'Chest')
sphere('Collar | teal jewel',(0,-.264,2.07),(.026,.015,.027),cyan,'Chest',24,14)

# Arms, gloves and articulated-looking fingers. Parts follow the nearest bone.
for side,s in [('L',1),('R',-1)]:
    upper='UpperArm.'+side; lower='LowerArm.'+side; hand='Hand.'+side
    a=(s*.39,0,2.08); b=(s*.58,0,1.76); c=(s*.73,-.015,1.47)
    sphere(side+' | shoulder',a,(.145,.16,.15),fabric,upper)
    tube(side+' | upper sleeve',[a,(s*.47,0,1.94),b],[.145,.135,.12],[.145,.13,.12],fabric,upper,24)
    sphere(side+' | elbow',b,(.118,.125,.12),fabric,lower)
    tube(side+' | forearm sleeve',[b,(s*.66,-.008,1.61),c],[.12,.115,.11],[.12,.115,.105],fabric,lower,24)
    # The torus local Z is aligned to the direction of the forearm.
    direction=(Vector(c)-Vector(b)).normalized()
    quat=Vector((0,0,1)).rotation_difference(direction)
    t=torus(side+' | gold cuff',(s*.705,-.012,1.51),.112,.022,gold,lower,quat.to_euler())
    sphere(side+' | glove palm',(s*.75,-.02,1.40),(.105,.085,.12),fabric_dark,hand,32,18,rot=(0,s*.15,0))
    for j in range(4):
        x=s*(.69+j*.038)
        sphere(side+' | glove finger '+str(j+1),(x,-.035,1.325-(.012 if j in (1,2) else 0)),
               (.027,.04,.07),fabric_dark,hand,24,14,rot=(0,s*.1,0))
    sphere(side+' | glove thumb',(s*.65,-.075,1.395),(.045,.045,.08),fabric_dark,hand,24,14,rot=(0,-s*.5,0))
    curve(side+' | sleeve seam',[(s*.40,-.14,2.07),(s*.47,-.13,1.95),(s*.53,-.115,1.81)],.006,fabric_seam,upper)

# Delicate chain links draped over the character's right hip.
for i in range(17):
    t=i/16
    x=.08+.36*t
    z=1.60-.095*math.sin(math.pi*t)-.025*t
    y=-.222+.11*t*t
    torus('Belt chain | link %02d'%i,(x,y,z),.026,.006,gold,'Hips',
          rotation=(math.pi/2, (math.pi/2 if i%2 else 0), -.25), scale=(1.18,.72,1))
for x,y,z in [(.08,-.23,1.60),(.44,-.11,1.575)]:
    sphere('Belt chain | fastening', (x,y,z),(.025,.014,.025),gold,'Hips',20,12)

# Hair cap sits behind a sculpted red face, with overlapping locks for a bob.
sphere('Hair | back volume',(0,.035,2.76),(.545,.405,.55),hair,'Head',56,32)
sphere('Face | soft oval',(0,-.215,2.68),(.45,.315,.425),skin,'Head',64,40)
for side,s in [('L',1),('R',-1)]:
    tube(side+' | pointed ear',[(s*.40,-.01,2.73),(s*.53,-.025,2.75),(s*.67,-.035,2.80)],
         [.115,.10,.006],[.062,.055,.004],skin,'Head',24)
    tube(side+' | inner ear',[(s*.50,-.083,2.76),(s*.57,-.073,2.77),(s*.635,-.048,2.79)],
         [.035,.028,.003],[.010,.008,.002],ear_inner,'Head',18)
    # Locks frame the cheeks, then curl back at the jaw.
    tube(side+' | side fringe',[(s*.38,-.24,3.08),(s*.45,-.36,2.96),(s*.46,-.40,2.79),
                               (s*.46,-.40,2.61),(s*.42,-.38,2.45)],
         [.14,.13,.115,.10,.015],[.09,.095,.09,.08,.012],hair,'Head',24)
    tube(side+' | swept back hair',[(s*.43,.00,2.93),(s*.50,.00,2.73),(s*.53,.015,2.53),
                                    (s*.48,.02,2.37),(s*.38,.035,2.34)],
         [.13,.13,.12,.10,.008],[.12,.12,.10,.075,.005],hair,'Head',24)
    curve(side+' | hair highlight',[(s*.40,-.425,2.96),(s*.43,-.48,2.86),
                                   (s*.435,-.475,2.69),(s*.425,-.455,2.57)],.007,hair_strand,'Head')
    tube(side+' | horn',[(s*.345,.005,3.16),(s*.39,-.005,3.23),(s*.43,-.015,3.31),
                         (s*.445,-.025,3.39),(s*.425,-.03,3.47)],
         [.10,.095,.07,.042,.003],[.085,.08,.062,.035,.002],horn,'Head',28)
    curve(side+' | horn highlight',[(s*.36,-.075,3.20),(s*.40,-.086,3.28),
                                   (s*.42,-.069,3.36)],.008,horn_tip,'Head')

# Layered tapered bangs rather than a painted-on hairline.
for name, points, widths in [
    ('center',[(0,-.18,3.24),(-.02,-.33,3.16),(-.055,-.42,3.04),(-.11,-.46,2.95)],[.20,.19,.15,.008]),
    ('left',[(.17,-.16,3.22),(.20,-.29,3.15),(.24,-.40,3.04),(.28,-.43,2.98)],[.16,.15,.10,.006]),
    ('right',[(-.20,-.16,3.20),(-.24,-.30,3.12),(-.29,-.41,3.01),(-.31,-.43,2.94)],[.15,.14,.09,.006]),
]:
    tube('Hair | '+name+' fringe',points,widths,[.085,.08,.065,.005],hair,'Head',28)
curve('Hair | central shine',[(-.10,-.395,3.17),(-.12,-.465,3.10),(-.14,-.493,3.04)],.008,hair_strand,'Head')

# Layered locks and subtle grooves make the back of the bob readable in 3D.
for i,fac in enumerate([-.85,-.43,0,.43,.85]):
    path=[]
    for z in [3.08,2.98,2.82,2.64,2.49,2.36]:
        vertical=(z-2.76)/.55
        x=fac*.43*math.sqrt(max(.01,1-vertical*vertical))
        y=.035+.405*math.sqrt(max(.01,1-(x/.545)**2-vertical*vertical))+.009
        path.append((x,y,z))
    tube('Hair | back lock '+str(i+1),path,[.07,.09,.105,.095,.075,.008],
         [.025,.032,.036,.033,.027,.004],hair,'Head',20)
    if abs(fac)<.8:
        curve('Hair | back fine strand '+str(i+1),[(x,y+.033,z) for x,y,z in path[1:-1]],
              .0045,hair_strand,'Head',2)

# Anime-style eyes are real curved surfaces fitted to the face, so they follow
# the cheeks in profile rather than protruding as a set of flat discs.
def face_y(x,z,offset=0):
    v=max(.001, 1-(x/.45)**2-((z-2.68)/.425)**2)
    return -.215-.315*math.sqrt(v)-offset


def face_patch(name,cx,cz,rx,rz,mat,offset,segments=48,rings=6):
    verts=[(cx,face_y(cx,cz,offset),cz)]
    for ring in range(1,rings+1):
        r=ring/rings
        for j in range(segments):
            a=math.tau*j/segments
            x=cx+rx*r*math.cos(a); z=cz+rz*r*math.sin(a)
            verts.append((x,face_y(x,z,offset),z))
    faces=[]
    for j in range(segments):
        faces.append((0,1+j,1+(j+1)%segments))
    for ring in range(rings-1):
        start=1+ring*segments; nxt=start+segments
        for j in range(segments):
            k=(j+1)%segments
            faces.append((start+j,nxt+j,nxt+k,start+k))
    mesh=bpy.data.meshes.new(name+' mesh'); mesh.from_pydata(verts,[],faces); mesh.update()
    ob=bpy.data.objects.new(name,mesh); character_collection.objects.link(ob)
    return finish(ob,name,mat,'Head')


for side,s in [('L',1),('R',-1)]:
    x=s*.215
    face_patch(side+' | eye rim',x,2.745,.157,.127,eye_rim,.004)
    face_patch(side+' | eye white',x,2.741,.143,.111,white_eye,.009)
    face_patch(side+' | iris',x,2.738,.082,.103,iris,.014)
    face_patch(side+' | pupil',x,2.753,.043,.070,iris_dark,.018)
    face_patch(side+' | lower iris',x,2.684,.049,.022,iris_light,.022)
    face_patch(side+' | large eye glint',x-s*.032,2.785,.023,.027,white,.026)
    face_patch(side+' | small eye glint',x+s*.038,2.718,.011,.013,white,.027)
    coords=[(x-s*.15,2.765),(x-s*.11,2.835),(x,2.867),(x+s*.105,2.837),(x+s*.155,2.78)]
    curve(side+' | upper lashes',[(xx,face_y(xx,zz,.015),zz) for xx,zz in coords],.013,eye_rim,'Head')
    coords=[(x-s*.12,2.925),(x,2.952),(x+s*.12,2.927)]
    curve(side+' | eyebrow',[(xx,face_y(xx,zz,.012),zz) for xx,zz in coords],.010,hair,'Head')

curve('Face | small smiling mouth',[(-.085,-.529,2.50),(-.04,-.542,2.48),(0,-.547,2.475),
                                    (.045,-.54,2.485),(.08,-.53,2.505)],.012,mouth_mat,'Head')
curve('Face | lower lip',[(-.033,-.542,2.46),(0,-.548,2.455),(.033,-.542,2.465)],.006,horn_tip,'Head')
sphere('Face | nose',(0,-.534,2.60),(.025,.012,.018),skin,'Head',24,14)

# Handheld microphone: separate pieces, but skinned to the right hand so the
# presentation pose and exported skeleton retain the prop.
cyl_between('Microphone | handle',(-.76,-.04,1.31),(-.76,-.04,1.68),.047,.053,mic_black,'Hand.R')
sphere('Microphone | grille',(-.76,-.04,1.745),(.075,.075,.105),mic_mesh,'Hand.R',40,24)
torus('Microphone | lower teal ring',(-.76,-.04,1.66),.057,.008,cyan,'Hand.R')
torus('Microphone | upper dark ring',(-.76,-.04,1.69),.066,.006,mic_black,'Hand.R')
for z,rad in [(1.72,.071),(1.75,.074),(1.78,.071)]:
    torus('Microphone | grille band',(-.76,-.04,z),rad,.0025,mic_black,'Hand.R',scale=(1,1,1))
for a in range(0,360,30):
    q=math.radians(a)
    curve('Microphone | grille meridian', [(-.76+.068*math.cos(q),-.04+.068*math.sin(q),1.71),
        (-.76+.074*math.cos(q),-.04+.074*math.sin(q),1.75),
        (-.76+.065*math.cos(q),-.04+.065*math.sin(q),1.79)],.002,mic_black,'Hand.R',2)

# Provide an embedded, switchable copy of the actual source image in the file.
if REF.exists():
    img=bpy.data.images.load(str(REF), check_existing=True)
    # Clear the previously loaded absolute path before saving a relative one.
    img.filepath = 'x' * 700
    img.filepath = '//../../Screen/CharacterSelect/a.png'
    ref=bpy.data.objects.new('Reference | character select artwork',None)
    ref.empty_display_type='IMAGE'; ref.data=img; ref.empty_display_size=3.5
    ref.location=(3.1,.8,1.75); ref.rotation_euler=(math.pi/2,0,0)
    ref.hide_render=True
    reference_collection.objects.link(ref)
    ref.hide_set(True)


def track(obj, target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()


def area(name, loc, power, color, size, target=(0,0,1.8)):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.color=color; data.shape='DISK'; data.size=size
    ob=bpy.data.objects.new(name,data); stage_collection.objects.link(ob); ob.location=loc; track(ob,target)
    return ob


cam_data=bpy.data.cameras.new('Portrait camera'); cam=bpy.data.objects.new('Portrait camera',cam_data)
stage_collection.objects.link(cam); cam.location=(3.0,-11.0,3.35); track(cam,(0,0,1.76))
cam_data.type='ORTHO'; cam_data.ortho_scale=4.08; scene.camera=cam
area('Key | warm softbox',(4,-6,6),560,(1.0,.82,.75),4.5)
area('Fill | cool softbox',(-4,-4,3.5),400,(.65,.77,1.0),4.0)
area('Rim | purple',(-2,3,4.8),650,(.5,.36,1.0),3.0)
area('Rim | gold',(4,2,3.0),380,(1.0,.55,.21),3.0)
world=scene.world; world.use_nodes=True
world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.036,.042,.062,1)
world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.4
scene.render.film_transparent=False

# Make neutral and stage poses part of the same easily-editable action.  Bones
# rotate rigidly by design; coat tails can be refined for extreme animation.
for p in rig.pose.bones:
    p.rotation_mode='QUATERNION'
    p.keyframe_insert(data_path='rotation_quaternion',frame=1,group=p.name)
scene.frame_set(40)

def aim_bone(name, direction):
    pb=rig.pose.bones[name]
    bpy.context.view_layer.update()
    head=pb.head.copy()
    current=(pb.tail-pb.head).normalized()
    delta=current.rotation_difference(Vector(direction).normalized()).to_matrix().to_4x4()
    pb.matrix=Matrix.Translation(head) @ delta @ Matrix.Translation(-head) @ pb.matrix
    bpy.context.view_layer.update()


# A small bend lifts the microphone without obscuring the eyes or silhouette.
aim_bone('UpperArm.R',(-.12,-.17,-.27))
aim_bone('LowerArm.R',(.22,-.23,.21))
aim_bone('Hand.R',(-.12,.12,-.25))
for p in rig.pose.bones:
    p.keyframe_insert(data_path='rotation_quaternion',frame=40,group=p.name)
if rig.animation_data and rig.animation_data.action:
    rig.animation_data.action.name='Neutral to microphone pose'
scene.frame_start=1; scene.frame_end=40

# Export only the character. The reference, camera and studio never enter FBX.
scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
for ob in parts:
    if ob.type == 'MESH' and not ob.name.startswith('Mesh_'):
        ob.name = 'Mesh_' + ob.name
    ob.select_set(True)
bpy.context.view_layer.objects.active=rig
export_fbx(filepath=str(OUT/'DemonKing.fbx'), use_selection=True,
    object_types={'ARMATURE','MESH'}, add_leaf_bones=False, bake_anim=False,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', path_mode='AUTO')
bpy.ops.export_scene.gltf(filepath=str(OUT/'DemonKing.glb'), export_format='GLB',
    use_selection=True, export_animations=False)

# Deliver an attractive first-open file and a transparent render for review.
scene.frame_set(40)
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active=None
for area_obj in bpy.context.screen.areas if bpy.context.screen else []:
    if area_obj.type=='VIEW_3D':
        area_obj.spaces.active.shading.type='MATERIAL'
        area_obj.spaces.active.region_3d.view_perspective='CAMERA'
scene.render.filepath='//DemonKing_preview.png'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'DemonKing.blend'))
bpy.ops.render.render(write_still=True)
# Neutral front and back views make the whole sculpt easy to inspect.
scene.frame_set(1)
scene.cycles.samples=32
scene.render.resolution_percentage=70
for view_name, location in [('front',(0,-11,3.35)),('back',(0,11,3.35))]:
    cam.location=location
    track(cam,(0,0,1.76))
    scene.render.filepath=str(OUT/('DemonKing_'+view_name+'.png'))
    bpy.ops.render.render(write_still=True)
print('DONE: %d skinned mesh parts'%len(parts))
