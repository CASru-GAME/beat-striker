using UnityEngine;
using R3;
using System;
using System.Collections.Generic;
using Alice;

namespace Core.LargeSatan {


    public class AttackState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;
        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;

        [SerializeField] float speed = 20f;
        [SerializeField] float duration = 0.5f;
        [SerializeField] float endSpeedRatio = 0.1f;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 10;

        [SerializeField] AttackPlayer attackPlayer;
        [SerializeField] float impact = 5;

        Vector3 initialVelocity;
        float elapsedTime;


        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip, OnAnimationEnd);

            var direction = context.InputDirection == Vector2.zero ? Vector2.up : context.InputDirection;
            this.initialVelocity = speed * direction;
            this.elapsedTime = 0f;
            context.Rigidbody.linearVelocity = this.initialVelocity;

            attackPlayer.OnFilterHit
                .Subscribe(other => other.TryGetComponent<Hurtbox>(out var hurtbox))
                .AddTo(disposables);

            attackPlayer.OnHit
                .Subscribe(other => {
                    if (other.TryGetComponent<Hurtbox>(out var hitbox)) {
                        var hitStatus = new HitStatus(damage, nockbackSpeed * (other.transform.position - context.Rigidbody.position).normalized);
                        hitbox.GiveHit(hitStatus);
                        context.GenerateImpact(new StrikerImpact(impact * Vector3.down));
                    }
                })
                .AddTo(disposables);

            attackPlayer.Emit();
        }

        public override void OnUpdate(IStrikerStateContext context) {
            elapsedTime += Time.deltaTime;
            float ratio = Mathf.Max(endSpeedRatio, 0.0001f);
            float decayRate = Mathf.Log(1f / ratio) / Mathf.Max(duration, 0.0001f);
            float decay = Mathf.Exp(-decayRate * elapsedTime);
            context.Rigidbody.linearVelocity = this.initialVelocity * decay;
        }

        public void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }


    }
}


