using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Alice;
using UnityEditor;
using UnityEngine;
using VContainer;
using R3;

namespace Alice.Editor {
    // Exercises the production battle deployment, input requests, physics, hitboxes,
    // animation player and StageCamera. Captures are temporary review artifacts.
    public static class DemonTechniquePlayModeReview {
        static IStrikerHub demon, opponent;
        static IBattleDeployer deployer;
        static StrikerHub legacy;
        static StageCamera camera;
        static Transform visual;
        static Vector3 start, target;
        static float begun, nextCapture, maxHeight, previousHp;
        static int sequence, frame;
        static bool recording, interrupted, actionStarted;
        static CompositeDisposable subscriptions=new();
        static readonly List<string> observations = new();
        static readonly string Output = Path.Combine(Path.GetTempPath(),"DemonTechniqueReview");

        [MenuItem("Tools/Strikers/Review Demon In Playing Battle")]
        public static async void Run() {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter play mode in a battle scene first.");
            try {
                var scope = UnityEngine.Object.FindObjectsByType<BattleScope>(FindObjectsSortMode.None).Single();
                var container = scope.Container;
                var selection = container.Resolve<IPlayerSelectSetting>();
                selection.SelectStriker(0,Striker.DemonKing);
                selection.SelectStriker(1,Striker.Warrior);
                deployer = container.Resolve<IBattleDeployer>();
                deployer.DisconnectRoundInputs();
                await deployer.RedeployForNextRoundAsync();
                await Awaitable.NextFrameAsync();
                deployer.DisconnectRoundInputs();
                var registry = container.Resolve<IStrikerRegistry>();
                demon = registry.GetAllStrikers().Single(x=>x.PlayerId.CurrentValue==0);
                opponent = registry.GetAllStrikers().Single(x=>x.PlayerId.CurrentValue==1);
                legacy = UnityEngine.Object.FindObjectsByType<StrikerHub>(FindObjectsSortMode.None).Single(x=>x.EnsureAliceRuntimeHub()==demon);
                visual = legacy.transform.Find("DemonKing Visual");
                camera = UnityEngine.Object.FindObjectsByType<StageCamera>(FindObjectsSortMode.None).Single();
                camera.RequestSequenceSkip(); camera.ResetRoundCamera(); camera.PresentRoundPlayableStart();
                subscriptions.Dispose(); subscriptions=new();
                camera.OnAttentionActiveStateChanged.Subscribe(active => {
                    var body = legacy.GetComponent<CapsuleCollider>();
                    var hits = Physics.RaycastAll(body.bounds.center, Vector3.down, body.bounds.extents.y + .1f, ~0, QueryTriggerInteraction.Ignore);
                    var hitNames = string.Join(";", hits.Select(h => $"{h.collider.name}:{h.point.y:F2}:{h.normal.y:F2}"));
                    var pausedField = deployer.GetType().GetField("isRoundPaused", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var message = $"roundPaused={pausedField.GetValue(deployer)}, boundsY={body.bounds.center.y:F2},hits={hitNames},kinematic={legacy.Rigidbody.isKinematic},velocityY={legacy.Rigidbody.linearVelocity.y:F2}, sequence={sequence}, time={Time.time - begun:F2}, active={active}, y={legacy.Rigidbody.position.y:F2}, grounded={legacy.GetComponent<Core.DarkSatan.GroundChecker>().IsGrounded}, vx={legacy.Rigidbody.linearVelocity.x:F2}, gravity={legacy.Rigidbody.useGravity}";
                    File.AppendAllText(Path.Combine(Output, "attention.txt"), message + Environment.NewLine);
                    Debug.Log("Demon review attention " + message);
                }).AddTo(subscriptions);
                foreach(var box in UnityEngine.Object.FindObjectsByType<Hurtbox>(FindObjectsSortMode.None)) box.OnHit.Subscribe(hit=>Debug.Log($"Review contact {box.name}: {hit.Damage}")).AddTo(subscriptions);
                await Awaitable.WaitForSecondsAsync(.5f);
                start = demon.Position.CurrentValue;
                target = opponent.Position.CurrentValue;
                Time.captureFramerate = 60;
                Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "attention.txt"), ""); observations.Clear();
                observations.Add("technique,t,x,y,scale,opponentHp,handX,handY,footX,footY,cameraZ,targetX,targetY,targetZ");
                sequence=0; Begin();
                EditorApplication.update -= Update;
                EditorApplication.update += Update;
                recording=true;
            } catch(Exception e) {Debug.LogException(e); Stop();}
        }
        static void ResetPositions() {
            demon.Default(); opponent.Default(); demon.CancelDirection(); opponent.CancelDirection();
            legacy.Rigidbody.position=start + (sequence == 4 ? Vector3.up * 5f : Vector3.zero);
            legacy.Rigidbody.linearVelocity=Vector3.zero;
            var opponentLegacy=UnityEngine.Object.FindObjectsByType<StrikerHub>(FindObjectsSortMode.None).Single(x=>x.EnsureAliceRuntimeHub()==opponent);
            opponentLegacy.Rigidbody.position=new Vector3(start.x+(sequence>=2 && sequence<5 ? 8f : .9f),start.y,target.z);
            opponentLegacy.Rigidbody.linearVelocity=Vector3.zero;
        }
        static void Begin() {
            ResetPositions();
            begun=Time.time+.3f; actionStarted=false; nextCapture=0; frame=0; maxHeight=start.y;
            previousHp=opponent.HitPoint.CurrentValue; interrupted=false;
        }
        static void Update() {
            if(!EditorApplication.isPlaying) {Stop();return;}
            if(!recording || Time.timeScale==0) return;
            deployer.DisconnectRoundInputs();
            float t=Time.time-begun;
            if(t<0) return;
            if(!actionStarted) {
                actionStarted=true;
                if(sequence>=2 && sequence<5) {demon.AddSpecialPoint(10000);demon.Special();}
                else if(sequence==5) {demon.Attack();}
                else {demon.ChangeDirection(sequence==0 ? Vector2.up : Vector2.down);demon.Attack();demon.CancelDirection();}
            }
            maxHeight=Mathf.Max(maxHeight,legacy.Rigidbody.position.y);
            var name=sequence==0 ? "upper" : sequence==1 ? "heel" : sequence==2 ? "special" : sequence==3 ? "interrupt" : sequence==4 ? "air_special" : "punch";
            if(sequence==3 && !interrupted && t>=.65f) {interrupted=true;demon.GiveHit(new HitStatus(1,new Vector3(-2,2,0)));}
            if(t>=nextCapture) {
                nextCapture+=1f/30;
                var renderCamera=camera.GetComponent<Camera>();
                var rt=RenderTexture.GetTemporary(960,540,24);
                var prior=renderCamera.targetTexture; var active=RenderTexture.active;
                renderCamera.targetTexture=rt; renderCamera.Render(); RenderTexture.active=rt;
                var capture=new Texture2D(960,540,TextureFormat.RGB24,false); capture.ReadPixels(new Rect(0,0,960,540),0,0); capture.Apply();
                File.WriteAllBytes(Path.Combine(Output,$"{name}_{frame++:D3}.png"),capture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(capture); renderCamera.targetTexture=prior; RenderTexture.active=active; RenderTexture.ReleaseTemporary(rt);
                var animator=visual.GetComponent<Animator>();
                var hand=visual.GetComponentsInChildren<Transform>().Single(x=>x.name=="Hand.R").position;
                var foot=visual.GetComponentsInChildren<Transform>().Single(x=>x.name=="Foot.R").position;
                var p=legacy.Rigidbody.position;
                observations.Add(FormattableString.Invariant($"{name},{t:F3},{p.x:F3},{p.y:F3},{visual.localScale.x:F3},{opponent.HitPoint.CurrentValue:F1},{hand.x:F3},{hand.y:F3},{foot.x:F3},{foot.y:F3},{camera.transform.position.z:F3},{opponent.Position.CurrentValue.x:F3},{opponent.Position.CurrentValue.y:F3},{opponent.Position.CurrentValue.z:F3}"));
            }
            if(t<(sequence==4 ? 6f : sequence>=2 && sequence<5 ? 3.5f : 1.15f)) return;
            Debug.Log($"Demon play review {name}: avatarRestored={visual.GetComponent<Animator>().avatar!=null}, category={demon.CurrentStateCategory.CurrentValue}, rise={maxHeight-start.y:F2}, damage={previousHp-opponent.HitPoint.CurrentValue:F1}, finalScale={visual.localScale}, gravity={legacy.Rigidbody.useGravity}");
            if(++sequence<6) {Begin();return;}
            File.WriteAllLines(Path.Combine(Output,"observations.csv"),observations);
            demon.Default(); opponent.Default();
            camera.enabled = false;
            deployer.Undeploy();
            Stop();
            Debug.Log("Demon play review complete. Captures and measured motion saved in the temporary review directory.");
            EditorApplication.isPlaying = false;
        }
        static void Stop() {EditorApplication.update-=Update; recording=false; Time.captureFramerate=0; subscriptions.Dispose();}
    }
}
