using UnityEngine;
using R3;
using System;
using System.Collections.Generic;
using Alice;

namespace Core.LargeSatan {

    public class AttackState : StrikerState {
        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;


        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 10;

        [SerializeField] AttackPlayer attackPlayer;
        [SerializeField] float impact = 5;


        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, OnAnimationEnd);

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

        public void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }


    }
}
