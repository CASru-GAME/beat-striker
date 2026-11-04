using System;
using System.IO;
using System.Linq;
using Core.DarkSatan;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Alice.Editor {
    public static class DemonTechniqueAuthoring {
        const string Folder = "Assets/Project/Strikers/DarkSatan/";
        const string Prefab = Folder + "DemonKing.prefab";
        static float S(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
        static void Muscle(HumanPose pose, string name, float value) {
            int i = Array.IndexOf(HumanTrait.MuscleName, name);
            if (i < 0) throw new InvalidOperationException(name);
            pose.muscles[i] = value;
        }
        static void Pose(ref HumanPose pose, int technique, float t) {
            if (technique == 0) {
                float load = S(0,.045f,t)*(1-S(.055f,.11f,t));
                float punch = S(.05f,.13f,t);
                float recover = S(.30f,.4f,t);
                float power = punch*(1-recover*.25f);
                Muscle(pose,"Spine Front-Back",.5f*load-.55f*power);
                Muscle(pose,"Chest Twist Left-Right",-.5f*load+.65f*power);
                Muscle(pose,"Right Shoulder Down-Up",.55f*power);
                Muscle(pose,"Right Arm Down-Up",-.8f+1.95f*power);
                Muscle(pose,"Right Arm Front-Back",.85f);
                Muscle(pose,"Right Forearm Stretch",-1f+1.95f*power);
                Muscle(pose,"Right Hand Down-Up",-.4f);
                Muscle(pose,"Left Arm Down-Up",-.65f);
                Muscle(pose,"Left Arm Front-Back",.6f-.8f*power);
                Muscle(pose,"Left Forearm Stretch",-.95f);
                Muscle(pose,"Right Upper Leg Front-Back",.6f*load-.6f*power);
                Muscle(pose,"Right Lower Leg Stretch",-.8f*load-.2f*power);
                Muscle(pose,"Left Upper Leg Front-Back",.7f*load+1.15f*power);
                Muscle(pose,"Left Lower Leg Stretch",-.8f*load-1f*power);
                pose.bodyPosition += new Vector3(0,-.22f*load,0);
                pose.bodyRotation = Quaternion.Euler(-16f*power,0,-12f*power)*Quaternion.Euler(0,180+15f*power,0);
            } else if (technique == 1) {
                float raise = S(.015f,.11f,t), chop = S(.13f,.22f,t), recover = S(.29f,.4f,t);
                float power = 1-recover;
                Muscle(pose,"Right Upper Leg Front-Back",(1.9f*raise-2.1f*chop)*power);
                Muscle(pose,"Right Lower Leg Stretch",(-.9f*S(.01f,.05f,t)+1.8f*S(.05f,.11f,t)-.85f*chop)*power);
                Muscle(pose,"Right Foot Up-Down",-.85f*raise*power);
                Muscle(pose,"Left Upper Leg Front-Back",.5f*raise*power);
                Muscle(pose,"Left Lower Leg Stretch",-.9f*raise*power);
                Muscle(pose,"Spine Front-Back",(.5f*raise+.55f*chop)*power);
                Muscle(pose,"Chest Twist Left-Right",-.4f*chop*power);
                Muscle(pose,"Left Arm Down-Up",-.4f+.9f*raise*(1-chop));
                Muscle(pose,"Right Arm Down-Up",-.4f+.9f*raise*(1-chop));
                Muscle(pose,"Left Forearm Stretch",-.8f);
                Muscle(pose,"Right Forearm Stretch",-.8f);
                pose.bodyRotation = Quaternion.Euler(-12f*raise+35f*chop,0,0)*Quaternion.Euler(0,180,0);
            } else {
                // Charge: deeply loaded sprinter stance. Launch: rear leg drives the
                // hips forward. Sprint: a complete opposite-arm/leg stride per loop.
                float crouch = technique == 2 ? S(.2f,.85f,t) : technique == 3 ? 1-S(0,.16f,t) : 0;
                float stride = technique == 4 ? Mathf.Sin(2*Mathf.PI*t/.28f) : technique == 3 ? S(0,.16f,t) : 0;
                float braking = technique == 5 ? S(0,.32f,t) : 0;
                float lean = technique == 2 ? 62f*crouch : technique == 3 ? 62f-20f*S(0,.16f,t) : technique == 4 ? 42f : 42f*(1-braking);
                pose.bodyRotation = Quaternion.Euler(lean,0,0)*Quaternion.Euler(0,180,0);
                pose.bodyPosition += Vector3.up * (technique == 2 ? -.42f*crouch : technique == 3 ? -.42f*crouch-.08f : technique == 4 ? -.08f+.035f*Mathf.Cos(4*Mathf.PI*t/.28f) : -.08f*(1-braking));
                Muscle(pose,"Spine Front-Back",technique == 2 ? -.2f*crouch : -.2f);
                Muscle(pose,"Neck Turn Left-Right",0);
                Muscle(pose,"Head Turn Left-Right",0);
                Muscle(pose,"Head Tilt Left-Right",0);
                Muscle(pose,"Head Nod Down-Up",.35f*(1-braking));
                Muscle(pose,"Chest Twist Left-Right",.25f*stride);
                for (int side=0;side<2;side++) {
                    string prefix = side==0 ? "Left" : "Right";
                    float cycle = side==0 ? stride : -stride;
                    float thigh = technique == 2 ? (side==0 ? 1.15f : -.25f)*crouch
                        : technique==5 ? .3f*(1-braking) : .95f*cycle;
                    float knee = technique == 2 ? (side==0 ? -.95f : -.85f)*crouch
                        : technique==5 ? -.4f*(1-braking) : -.25f-.75f*Mathf.Max(0,-cycle);
                    Muscle(pose,prefix+" Upper Leg Front-Back",thigh);
                    Muscle(pose,prefix+" Lower Leg Stretch",knee);
                    Muscle(pose,prefix+" Foot Up-Down",-.45f*(1-braking));
                    Muscle(pose,prefix+" Arm Down-Up",technique==2 ? -.9f : -.65f);
                    Muscle(pose,prefix+" Arm Front-Back",technique==2 ? (side==0 ? .8f : .4f)*crouch : -.95f*cycle);
                    Muscle(pose,prefix+" Forearm Stretch",technique==2 ? .75f*crouch : -.85f);
                    Muscle(pose,prefix+" Hand Down-Up",-.4f);
                }
            }
        }
        static void SolveLimb(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole) {
            float upper=Vector3.Distance(a.position,b.position),lower=Vector3.Distance(b.position,c.position);
            var delta=target-a.position;float d=Mathf.Clamp(delta.magnitude,.001f,upper+lower-.001f);
            var direction=delta.normalized;
            var bend=Vector3.ProjectOnPlane(pole,direction).normalized;
            float cosine=Mathf.Clamp((upper*upper+d*d-lower*lower)/(2*upper*d),-1,1);
            var joint=a.position+direction*(upper*cosine)+bend*(upper*Mathf.Sqrt(1-cosine*cosine));
            a.rotation=Quaternion.FromToRotation(b.position-a.position,joint-a.position)*a.rotation;
            b.rotation=Quaternion.FromToRotation(c.position-b.position,target-b.position)*b.rotation;
        }
        static void PlantSprintPose(Animator animator,int technique,float t) {
            var origin=animator.transform.parent.position;
            var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
            float charge=technique==2 ? S(.2f,.85f,t) : technique==3 ? 1-S(0,.16f,t) : 0;
            float brake=technique==5 ? S(0,.32f,t) : 0;
            var hp=hips.position;hp.y=origin.y+Mathf.Lerp(.86f,.4f,charge);hips.position=hp;
            for(int side=0;side<2;side++) {
                bool left=side==0;float sign=left ? -1 : 1;
                float phase=2*Mathf.PI*t/.28f+(left ? 0 : Mathf.PI);
                float stride=technique==4 ? Mathf.Cos(phase) : technique==3 ? Mathf.Lerp(left ? .25f : -.4f,left ? -.35f : .35f,t/.16f) : 0;
                var target=origin+new Vector3(sign*.14f,.12f+(technique==4 ? .42f*Mathf.Max(0,Mathf.Sin(phase)) : 0),technique==2 ? Mathf.Lerp(0,left ? .3f : -.42f,charge) : stride*.6f);
                SolveLimb(animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg),animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg),animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot),target,Vector3.forward);
                var foot=animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                var toe=foot.Find(left ? "Toe.L" : "Toe.R");
                if(toe!=null) foot.rotation=Quaternion.FromToRotation(toe.position-foot.position,Vector3.forward)*foot.rotation;
                if(technique==4) {
                    var hand=animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    var handTarget=origin+new Vector3(sign*.28f,1.03f+.12f*Mathf.Cos(phase),-.42f*Mathf.Cos(phase));
                    SolveLimb(animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm),animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm),hand,handTarget,Vector3.right*sign-Vector3.forward*.3f);
                }
                if(technique==2 && charge>.01f) {
                    var hand=animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    var targetHand=Vector3.Lerp(hand.position,origin+new Vector3(sign*.24f,.16f,.52f),charge);
                    SolveLimb(animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm),animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm),hand,targetHand,Vector3.right*sign);
                }
            }
        }
        static AnimationClip CreateClip(Animator animator, AnimationClip idle, int technique, string name, float duration) {
            var graph = PlayableGraph.Create("Demon technique authoring");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            try {
                var playable = AnimationClipPlayable.Create(graph, idle);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                graph.Play(); playable.SetTime(.1); graph.Evaluate(1f / 60f);
                using var handler = new HumanPoseHandler(animator.avatar, animator.transform);
                var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                var initial = (float[])pose.muscles.Clone();
                var basePosition = pose.bodyPosition; var baseRotation = pose.bodyRotation;
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var forwardHeadRotation = head.rotation;
                var bones=Enumerable.Range(0,(int)HumanBodyBones.LastBone).Select(i=>animator.GetBoneTransform((HumanBodyBones)i)).Where(b=>b!=null).Distinct().ToArray();
                var curves=bones.Select(_=>Enumerable.Range(0,7).Select(i=>new AnimationCurve()).ToArray()).ToArray();
                int count=Mathf.RoundToInt(duration*60);
                for(int f=0;f<=count;f++) {
                    float t=f/60f;
                    Array.Copy(initial,pose.muscles,initial.Length);
                    pose.bodyPosition=basePosition; pose.bodyRotation=baseRotation;
                    Pose(ref pose,technique,t);
                    handler.SetHumanPose(ref pose);
                    if(technique>=2) {
                        PlantSprintPose(animator,technique,t);
                        head.rotation = forwardHeadRotation;
                    }
                    if(technique==0) {
                        float punch=S(.05f,.13f,t);
                        var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                        var target=Vector3.Lerp(hand.position,new Vector3(.12f,1.75f,.6f),punch);
                        SolveLimb(animator.GetBoneTransform(HumanBodyBones.RightUpperArm),animator.GetBoneTransform(HumanBodyBones.RightLowerArm),hand,target,Vector3.forward);
                    }
                    if(technique==1) {
                        float raise=S(.015f,.11f,t),chop=S(.13f,.22f,t),recover=S(.29f,.4f,t);
                        var foot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
                        var target=Vector3.Lerp(foot.position,new Vector3(.13f,Mathf.Lerp(1.45f,.13f,chop),.45f),raise*(1-recover));
                        SolveLimb(animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),animator.GetBoneTransform(HumanBodyBones.RightLowerLeg),foot,target,Vector3.forward);
                    }
                    for(int i=0;i<bones.Length;i++) {
                        var p=bones[i].localPosition; var q=bones[i].localRotation;
                        float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w};
                        for(int c=0;c<7;c++) curves[i][c].AddKey(t,values[c]);
                    }
                }
                var clip=new AnimationClip {name=name,frameRate=60};
                string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
                for(int i=0;i<bones.Length;i++) for(int c=0;c<7;c++) AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[i],animator.transform),typeof(Transform),properties[c]),curves[i][c]);
                clip.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = technique == 4;
                settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
                settings.loopBlendOrientation = settings.loopBlendPositionY = settings.loopBlendPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                string path = Folder + "States/Attack/" + name + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) {AssetDatabase.CreateAsset(clip, path); return clip;}
                EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); return existing;
            } finally {graph.Destroy();}
        }
        static void Ref(SerializedObject so, string name, UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue = value;
        static void Number(SerializedObject so, string name, float value) => so.FindProperty(name).floatValue = value;
        static void Animation(SerializedObject so, string name, AnimationClip clip) {
            Ref(so, name + ".clip", clip); Number(so, name + ".fadeTime", .025f); Number(so, name + ".speed", 1);
            so.FindProperty(name + ".disableFootIK").boolValue = true;
            so.FindProperty(name+".useSkeletonCurves").boolValue=true;

        }
        static ParticleSystem Particle(Transform visual, string name) {
            var child = visual.Find(name);
            if(child==null) { child=new GameObject(name).transform; child.SetParent(visual,false); child.gameObject.AddComponent<ParticleSystem>(); }
            child.localPosition=Vector3.up*.9f;
            var particle=child.GetComponent<ParticleSystem>(); particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particle.main; main.playOnAwake=false; main.loop=true; main.duration=1; main.maxParticles=300;
            particle.GetComponent<ParticleSystemRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Effects/Bullet/DemonAttackGlow.mat");
            return particle;
        }
        [MenuItem("Tools/Strikers/Author Demon Directional And Rush Techniques")]
        public static void Generate() {
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try {
                var originalTransforms=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
                var savedPosition = root.transform.position;
                var savedRotation = root.transform.rotation;
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var visual = root.transform.Find("DemonKing Visual");
                var animator = visual.GetComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var melee = root.GetComponentInChildren<DemonMeleeAttackState>(true);
                var source = new SerializedObject(melee);
                var idle = new SerializedObject(root.GetComponentInChildren<Core.DarkSatan.IdleState>(true));
                var idleClip = (AnimationClip)idle.FindProperty("animationClip.clip").objectReferenceValue;
                var clips = new[] { CreateClip(animator, idleClip, 0, "DemonJumpUppercut", .4f), CreateClip(animator, idleClip, 1, "DemonHeelDrop", .4f), CreateClip(animator, idleClip, 2, "DemonGiantCharge", 1.25f), CreateClip(animator, idleClip, 3, "DemonSprintLaunch", .16f), CreateClip(animator, idleClip, 4, "DemonGiantRush", .28f), CreateClip(animator, idleClip, 5, "DemonRushBrake", .32f) };
                var parent = source.FindProperty("parents").GetArrayElementAtIndex(0).objectReferenceValue;
                var next = source.FindProperty("nextNode").objectReferenceValue;
                var glow=new SerializedObject(source.FindProperty("attackEffect").objectReferenceValue);glow.FindProperty("glowColor").colorValue=new Color(.035f,.0005f,.001f);glow.ApplyModifiedPropertiesWithoutUndo();
                var attackNode = new SerializedObject(root.GetComponentInChildren<Core.DarkSatan.AttackNode>(true));
                for (int i = 0; i < 2; i++) {
                    string name = i == 0 ? "DemonJumpUppercutState" : "DemonHeelDropState";
                    var transform = melee.transform.parent.Find(name);
                    if (transform == null) {transform = new GameObject(name).transform; transform.SetParent(melee.transform.parent, false); transform.gameObject.AddComponent<DemonDirectionalAttackState>();}
                    var state = transform.GetComponent<DemonDirectionalAttackState>();
                    var so = new SerializedObject(state);
                    so.FindProperty("technique").enumValueIndex = i;
                    so.FindProperty("parents").arraySize = 1;
                    so.FindProperty("parents").GetArrayElementAtIndex(0).objectReferenceValue = parent;
                    Ref(so, "nextNode", next);
                    foreach (var field in new[] {"groundChecker", "attackEffect", "hitEffect", "explosionEffect", "blockedSound"}) Ref(so, field, source.FindProperty(field).objectReferenceValue);
                    var strike = source.FindProperty("strikes").GetArrayElementAtIndex(i == 0 ? 1 : 2);
                    Ref(so, "limb", strike.FindPropertyRelative("Limb").objectReferenceValue);
                    Ref(so, "swingSound", strike.FindPropertyRelative("SwingSound").objectReferenceValue);
                    Ref(so, "hitSound", strike.FindPropertyRelative("HitSound").objectReferenceValue);
                    Animation(so, "animation", clips[i]);
                    Number(so, "duration", .4f); Number(so,"hitReach",i==0 ? .6f : .35f); Number(so,"takeoffTime",i==0 ? .07f : .055f); Number(so, "jumpSpeed", i == 0 ? 22 : 11); Number(so, "diveSpeed", 26); Number(so, "forwardSpeed", i == 0 ? 4 : 3);
                    Number(so, "hitStart", i == 0 ? .035f : .16f); Number(so, "hitEnd", .31f);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Ref(attackNode, i == 0 ? "upAttackState" : "downAttackState", state);
                    Ref(source, i == 0 ? "upAttackState" : "downAttackState", state);
                }
                source.ApplyModifiedPropertiesWithoutUndo();
                attackNode.ApplyModifiedPropertiesWithoutUndo();
                var special = new SerializedObject(root.GetComponentInChildren<Core.DarkSatan.SpecialState>(true));
                Ref(special, "nextNode", next); Ref(special, "visual", visual); Ref(special, "bodyCollider", root.GetComponent<CapsuleCollider>());
                special.FindProperty("parents").arraySize = 1; special.FindProperty("parents").GetArrayElementAtIndex(0).objectReferenceValue = parent;
                foreach (var field in new[] {"attackEffect", "explosionEffect", "blockedSound"}) Ref(special, field, source.FindProperty(field).objectReferenceValue);
                Ref(special, "hitSound", source.FindProperty("strikes").GetArrayElementAtIndex(0).FindPropertyRelative("HitSound").objectReferenceValue);
                Animation(special, "chargeAnimation", clips[2]); Animation(special, "launchAnimation", clips[3]); Animation(special, "sprintAnimation", clips[4]); Animation(special, "brakeAnimation", clips[5]);
                Number(special,"attentionSeconds",1.25f); Number(special,"growthSeconds",.48f); Number(special,"pushOffSeconds",.16f); Number(special,"rushSeconds",.72f); Number(special,"recoverySeconds",.32f); Number(special,"giantScale",2.5f); Number(special,"rushSpeed",26); Number(special,"hitRadius",1.65f);
                special.FindProperty("attentionTechniqueText").stringValue = "魔王・覇王突進";
                var auraTransform = visual.Find("Giant Rush Mana Aura");
                if (auraTransform == null) {
                    auraTransform = new GameObject("Giant Rush Mana Aura").transform;
                    auraTransform.SetParent(visual, false); auraTransform.localPosition = Vector3.up * .9f;
                    auraTransform.gameObject.AddComponent<ParticleSystem>();
                }
                var aura = auraTransform.GetComponent<ParticleSystem>();
                aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = aura.main; main.playOnAwake = false; main.loop = true; main.duration = 1.3f; main.startLifetime = .35f; main.startSpeed = 2.8f; main.startSize = new ParticleSystem.MinMaxCurve(.06f, .18f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f,.05f,.25f,1),new Color(.5f,.06f,1f,1)); main.maxParticles = 220; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var shape = aura.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .9f; shape.radiusThickness = .1f;
                var emission = aura.emission; emission.rateOverTime = 240;
                var velocity = aura.velocityOverLifetime; velocity.enabled = true; velocity.y = new ParticleSystem.MinMaxCurve(3.6f);
                var size = aura.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0,1,1,0));
                aura.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Effects/Bullet/DemonAttackGlow.mat");
                var flameRenderer=aura.GetComponent<ParticleSystemRenderer>(); flameRenderer.renderMode=ParticleSystemRenderMode.Stretch; flameRenderer.lengthScale=3; flameRenderer.velocityScale=.1f;
                var noise=aura.noise;noise.enabled=true;noise.strength=.3f;noise.frequency=2;
                Ref(special, "aura", aura);
                var shell=visual.Find("Giant Mana Mantle");
                if(shell==null) {var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Giant Mana Mantle";shell=go.transform;shell.SetParent(visual,false);UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}
                shell.localPosition=Vector3.up*.9f; shell.localScale=new Vector3(1.8f,2.4f,1.8f);
                string materialPath=Folder+"Effects/DemonRushMana.mat";
                var mantle=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(mantle==null) {mantle=new Material(Shader.Find("BeatStriker/DemonRushMana"));AssetDatabase.CreateAsset(mantle,materialPath);}
                shell.GetComponent<MeshRenderer>().sharedMaterial=mantle;
                shell.gameObject.SetActive(false);Ref(special,"manaShell",shell.gameObject);
                var wind = Particle(visual,"Rush Speed Streaks");
                var windMain = wind.main; windMain.startLifetime = .22f; windMain.startSpeed = 0; windMain.startSize = new ParticleSystem.MinMaxCurve(.05f,.14f); windMain.startColor = new Color(.7f,.1f,1,1); windMain.simulationSpace = ParticleSystemSimulationSpace.World;
                var windShape = wind.shape; windShape.shapeType = ParticleSystemShapeType.Sphere; windShape.radius = .8f;
                var windVelocity = wind.velocityOverLifetime; windVelocity.enabled = true; windVelocity.space = ParticleSystemSimulationSpace.Local; windVelocity.z = -14f;
                var windEmission = wind.emission; windEmission.rateOverTime = 100;
                var renderer = wind.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 5; renderer.velocityScale = .15f;
                var burst = Particle(visual,"Rush Launch Shockwave");
                burst.transform.localPosition = Vector3.up*.05f;
                var burstMain = burst.main; burstMain.loop = false; burstMain.startLifetime = .3f; burstMain.startSpeed = 9; burstMain.startSize = new ParticleSystem.MinMaxCurve(.08f,.24f); burstMain.startColor = new Color(1,.15f,.35f,1); burstMain.simulationSpace = ParticleSystemSimulationSpace.World;
                var burstShape = burst.shape; burstShape.shapeType = ParticleSystemShapeType.Circle; burstShape.radius = .6f; burstShape.rotation = new Vector3(90,0,0);
                var burstEmission = burst.emission; burstEmission.rateOverTime = 0; burstEmission.SetBursts(new[]{new ParticleSystem.Burst(0,90)});
                Ref(special,"rushWind",wind); Ref(special,"launchBurst",burst);
                Ref(special,"launchSound",source.FindProperty("strikes").GetArrayElementAtIndex(0).FindPropertyRelative("SwingSound").objectReferenceValue);
                special.ApplyModifiedPropertiesWithoutUndo();
                foreach(var pair in originalTransforms) {pair.Key.localPosition=pair.Value.localPosition; pair.Key.localRotation=pair.Value.localRotation; pair.Key.localScale=pair.Value.localScale;}
                root.transform.SetPositionAndRotation(savedPosition, savedRotation);
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                AssetDatabase.SaveAssets();
                Debug.Log("Demon techniques saved: 0.4s rising uppercut and heel drop; attention crouch, launch, cyclic sprint and braking, editable states and aura.");
            } finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        [MenuItem("Tools/Strikers/Preview Demon Techniques")]
        public static void Preview() {
            var preview = new PreviewRenderUtility();
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            root.transform.position = Vector3.zero;
            preview.AddSingleGO(root);
            var sheet = new Texture2D(1600, 2400, TextureFormat.RGB24, false);
            try {
                var visual = root.transform.Find("DemonKing Visual");
                var animator = visual.GetComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var originalScale = visual.localScale;
                var aura = visual.Find("Giant Rush Mana Aura").GetComponent<ParticleSystem>();
                var rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);var rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
                animator.avatar=null;
                var skins = animator.GetComponentsInChildren<SkinnedMeshRenderer>();
                var meshes = skins.Select(skin => {
                    var go = new GameObject("Baked technique pose"); go.transform.SetParent(root.transform, false);
                    var mesh = UnityEngine.Object.Instantiate(skin.sharedMesh); go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                    return mesh;
                }).ToArray();
                root.transform.rotation = Quaternion.Euler(0,-25,0);
                preview.camera.orthographic = true; preview.camera.orthographicSize = 1.55f;
                preview.camera.transform.position = new Vector3(4,2.2f,4); preview.camera.transform.LookAt(new Vector3(0,1,0));
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 50;
                preview.lights[0].intensity = 1.5f; preview.lights[0].transform.rotation = Quaternion.Euler(40,30,0); preview.lights[1].intensity = 1;
                string[] names = {"DemonJumpUppercut", "DemonHeelDrop", "DemonGiantCharge", "DemonSprintLaunch", "DemonGiantRush", "DemonRushBrake"};
                for (int row = 0; row < 6; row++) {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "States/Attack/" + names[row] + ".anim");
                    var graph = PlayableGraph.Create("Technique preview");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    try {
                        var playable = AnimationClipPlayable.Create(graph, clip);
                        playable.SetApplyFootIK(false);
                        AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable); graph.Play();
                        float[][] samples = {new[]{0f,.08f,.16f,.3f},new[]{0f,.11f,.2f,.32f},new[]{0f,.45f,.85f,1.2f},new[]{0f,.05f,.1f,.16f},new[]{0f,.07f,.14f,.21f},new[]{0f,.1f,.2f,.32f}};
                        float[] times = samples[row];
                        for (int col=0;col<4;col++) {
                            float t = times[col];
                            visual.localScale = originalScale * (row < 2 ? 1f : row == 2 ? Mathf.Lerp(1,2.5f,S(0,.48f,t)) : 2.5f);
                            preview.camera.orthographicSize = row >= 2 ? 2.7f : 1.55f;
                            preview.camera.transform.LookAt(new Vector3(0,row >= 2 ? 1.8f : 1f,0));
                            if (row == 2 && col > 0 && col < 3) aura.Simulate(.25f,true,true);
                            else aura.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                            playable.SetTime(times[col]); graph.Evaluate(1f / 60000f);
                            for (int i=0;i<skins.Length;i++) {
                                var skin = skins[i]; var source = skin.sharedMesh;
                                var matrices = skin.bones.Select((bone,index) => root.transform.worldToLocalMatrix * bone.localToWorldMatrix * source.bindposes[index]).ToArray();
                                var vertices = source.vertices; var weights = source.boneWeights;
                                for (int v=0;v<vertices.Length;v++) {
                                    var p = vertices[v]; var w = weights[v];
                                    vertices[v] = matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1 + matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                                }
                                meshes[i].vertices = vertices; meshes[i].RecalculateBounds(); meshes[i].RecalculateNormals();
                            }
                            preview.BeginPreview(new Rect(0,0,400,400), GUIStyle.none);
                            preview.Render(true);
                            var texture = preview.EndPreview();
                            var prior = RenderTexture.active;
                            var rt = RenderTexture.GetTemporary(400,400,0); Graphics.Blit(texture,rt); RenderTexture.active=rt;
                            sheet.ReadPixels(new Rect(0,0,400,400),col*400,(5-row)*400); RenderTexture.active=prior; RenderTexture.ReleaseTemporary(rt);
                            Debug.Log($"{names[row]} t={times[col]} rightHand={rightHand.position} rightFoot={rightFoot.position}");
                        }
                    } finally {graph.Destroy();}
                }
                sheet.Apply(); File.WriteAllBytes(Path.Combine(Path.GetTempPath(),"DemonTechniquesPreview.png"),sheet.EncodeToPNG());
                foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            } finally {preview.Cleanup(); UnityEngine.Object.DestroyImmediate(sheet);}
        }
    }
}
