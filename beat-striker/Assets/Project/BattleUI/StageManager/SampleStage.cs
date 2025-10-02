using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class SampleStatge : MonoBehaviour {
    [SerializeField] CinemachineCamera camera0;
    [SerializeField] CinemachineCamera camera1;
    [SerializeField] CinemachineCamera stageCamera;
    [SerializeField] Animator stageCameraAnimator;

    void Awake() {
        Battle.Instance.introState.SetStageAnime(StageAnime());
        Battle.Instance.introState.SetReadyAnime(ReadyAnime());
        Battle.Instance.introState.SetStrikerAnime(0, StrikerAnime0());
        Battle.Instance.introState.SetStrikerAnime(1, StrikerAnime1());
        Battle.Instance.outroState.SetVictoryAnime(VictoryAnime());
    }

    IEnumerator StageAnime() {
        stageCameraAnimator.Play("SampleStageIntro");
        yield return new WaitForSeconds(4f);
    }

    IEnumerator StrikerAnime0() {
        SwitchTo(camera0);
        Battle.Instance.strikers[0].IntroPose();
        yield return new WaitForSeconds(3f);

    }

    IEnumerator StrikerAnime1() {
        SwitchTo(camera1);
        Battle.Instance.strikers[1].IntroPose();
        yield return new WaitForSeconds(3f);

    }

    IEnumerator ReadyAnime() {
        SwitchTo(stageCamera);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator VictoryAnime() {
        yield return new WaitForSeconds(3f);
        SwitchTo(Battle.Instance.Winner == 0 ? camera0 : camera1);
        Battle.Instance.strikers[Battle.Instance.Winner].OutroPose();
        yield return new WaitForSeconds(3f);
    }

    void SwitchTo(CinemachineCamera target) {
        camera0.Priority = 0;
        camera1.Priority = 0;
        stageCamera.Priority = 0;
        target.Priority = 10;
    }
}