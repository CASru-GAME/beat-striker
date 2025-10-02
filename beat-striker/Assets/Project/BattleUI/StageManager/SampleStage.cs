using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class SampleStatge : MonoBehaviour {
    public CinemachineCamera camera0;
    public CinemachineCamera camera1;
    public CinemachineCamera stageCamera;
    public Animator stageCameraAnimator;

    void Awake() {
        Battle.Instance.introState.SetStageAnime(StageAnime());
        Battle.Instance.introState.SetReadyAnime(ReadyAnime());
        Battle.Instance.introState.SetStrikerAnime(0, StrikerAnime0());
        Battle.Instance.introState.SetStrikerAnime(1, StrikerAnime1());
        Battle.Instance.outroState.SetVictoryAnime(VictoryAnime());
    }

    private IEnumerator StageAnime() {
        stageCameraAnimator.Play("SampleStageIntro");
        yield return new WaitForSeconds(4f);
    }

    private IEnumerator StrikerAnime0() {
        SwitchTo(camera0);
        Battle.Instance.strikers[0].IntroPose();
        yield return new WaitForSeconds(3f);

    }

    private IEnumerator StrikerAnime1() {
        SwitchTo(camera1);
        Battle.Instance.strikers[1].IntroPose();
        yield return new WaitForSeconds(3f);

    }

    private IEnumerator ReadyAnime() {
        SwitchTo(stageCamera);
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator VictoryAnime() {
        yield return new WaitForSeconds(3f);
        SwitchTo(Battle.Instance.Winner == 0 ? camera0 : camera1);
        Battle.Instance.strikers[Battle.Instance.Winner].OutroPose();
        yield return new WaitForSeconds(3f);
    }

    public void SwitchTo(CinemachineCamera target) {
        camera0.Priority = 0;
        camera1.Priority = 0;
        stageCamera.Priority = 0;
        target.Priority = 10;
    }
}