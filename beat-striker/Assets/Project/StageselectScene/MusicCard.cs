
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using Core;
using Core.App;
using Core.App.Installers;
using Core.App.Presenters.Scene.Types;
using Core.App.Interfaces;
using Core.App.Types;

[RequireComponent(typeof(AudioSource))]
public class MusicCard : MonoBehaviour {
    AudioSource audioSource;
    [SerializeField] Botan botan;
    [SerializeField] TextMeshProUGUI description;
    [SerializeField] AudioClip clickSound; // クリック時の効果音
    private Vector3 originalScale;
    private SelectableMusic currentMusic;
    private IAppModel appModel;

    void Awake() {
        audioSource = GetComponent<AudioSource>();
        originalScale = transform.localScale;
        appModel = AppFlowScope.GetInstance().GetAppModel();

        botan.onHover += (e) => {
            audioSource.Play();
            transform.localScale = originalScale * 1.1f;
            Debug.Log("card hovered");

        };
        botan.onHoverExit += (e) => {
            audioSource.Stop();
            transform.localScale = originalScale;
            Debug.Log("card hover exited");
        };
        botan.onClick += (e) => {
            Debug.Log("MusicCard: card clicked");

            // クリック効果音を再生（プレビュー音楽を一旦止めて効果音を再生）
            if (clickSound != null) {
                audioSource.Stop();
                audioSource.PlayOneShot(clickSound);
            }

            Debug.Log("MusicCard: Selected Track ID: " + currentMusic.trackId);
            Debug.Log("MusicCard: Publishing SelectTrack message");
            appModel.FireSelectTrack(currentMusic.trackId);

            Debug.Log("MusicCard: Publishing RequireTransition to CharacterSelect");
            appModel.FireRequireTransition(AppScene.CharacterSelect);

            Debug.Log("MusicCard: Messages published successfully");
        };
    }

    public void SetMusic(SelectableMusic music) {
        audioSource.clip = music.clip;
        description.text = music.description;
        currentMusic = music;
    }

    public void OnHidden() {
        audioSource.Stop();
    }
}