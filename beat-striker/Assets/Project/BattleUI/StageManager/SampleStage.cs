
using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class SampleStatge : MonoBehaviour {
    public CinemachineCamera camera0;
    public CinemachineCamera camera1;
    public CinemachineCamera stageCamera;
    public Animator stageCameraAnimator;

    void Start() {
        StartCoroutine(StartSequence());
        Battle.Instance.outroState.OnEnter += () => {
            StartCoroutine(OutroSequence());
        };
    }

    private IEnumerator StartSequence() {
        stageCameraAnimator.Play("SampleStageIntro");
        yield return new WaitForSeconds(4f);
        SwitchTo(camera0);
        Battle.Instance.strikers[0].IntroPose();
        yield return new WaitForSeconds(3f);
        SwitchTo(camera1);
        Battle.Instance.strikers[1].IntroPose();
        yield return new WaitForSeconds(3f);
        SwitchTo(stageCamera);
        yield return new WaitForSeconds(1f);
        Battle.Instance.ChangeState(Battle.Instance.playingState);
    }

    private IEnumerator OutroSequence() {
        yield return new WaitForSeconds(3f);
        var winner = Array.FindIndex(Battle.Instance.strikers, s => s.Rank == 1);
        SwitchTo(winner == 0 ? camera0 : camera1);
        Battle.Instance.strikers[1 - winner].gameObject.SetActive(false);
        Battle.Instance.strikers[winner].OutroPose();
        yield return new WaitForSeconds(3f);
        Battle.Instance.ChangeState(Battle.Instance.resultState);
    }

    public void BattleStart() {
        Battle.Instance.ChangeState(Battle.Instance.playingState);
    }

    public void BattleEnd() {
        Battle.Instance.ChangeState(Battle.Instance.resultState);
    }

    public void SwitchTo(CinemachineCamera target) {
        camera0.Priority = 0;
        camera1.Priority = 0;
        stageCamera.Priority = 0;
        target.Priority = 10;
    }
}