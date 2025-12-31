using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Core.App;
using Core.App.Interfaces;
using Core.App.Installers;
using Core.Utils;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Unity.VisualScripting;

[RequireComponent(typeof(RectTransform))]
public class DiagonalStripeVisual : MonoBehaviour {
    [Header("Transition Settings")]
    public float transitionDuration = 1f;
    public float stripeDelay = 0.05f;

    private RectTransform rectTransform;
    [SerializeField] private List<RectTransform> stripes = new();
    [SerializeField] private GameObject destroyOnComplete;

    private bool isTransitioning = false;
    private IAppModel appModel;
    private IDisposable transitionSub;

    void Awake() {
        appModel = AppFlowScope.GetInstance().GetAppModel();
        transitionSub = appModel.SubscribeTransitionAnimationStarted(OnTransitionStartedMessage);

        rectTransform = GetComponent<RectTransform>();

        InitializeStripes();
    }

    void OnTransitionStartedMessage(AppScene scene) {
        if (!isTransitioning) {
            StartCoroutine(PlayTransitionAnimation());
        }
    }

    void OnDestroy() {
        transitionSub?.Dispose();
    }

    IEnumerator PlayTransitionAnimation() {
        isTransitioning = true;

        yield return StartCoroutine(PlayTransition(true));

        Debug.Log("Publishing RequireLoadScene");

        appModel.FireRequireLoadScene();

        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(PlayTransition(false));

        isTransitioning = false;
        Debug.Log("PlayTransitionAnimation complete");

        Debug.Log("Destroying DiagonalStripeTransition instance");
        Destroy(destroyOnComplete);
    }

    void InitializeStripes() {
        Debug.Log("InitializeStripes called");

        Vector2 screenSize = rectTransform.rect.size;
        float diagonal = screenSize.magnitude;

        float stripeWidth = diagonal / stripes.Count;

        for (int i = 0; i < stripes.Count; i++) {
            var stripe = stripes[i];

            stripe.sizeDelta = new Vector2(0, stripeWidth);
            stripe.anchoredPosition = new Vector2(stripe.anchoredPosition.x, stripe.sizeDelta.y * (i - stripes.Count / 2));
        }

        float diagonalStripeRad = Mathf.Atan2(rectTransform.rect.height, rectTransform.rect.width);
        float diagonalAngle = diagonalStripeRad * Mathf.Rad2Deg;
        rectTransform.eulerAngles = new Vector3(0, 0, diagonalAngle);

        float width = rectTransform.rect.width;
        float height = rectTransform.rect.height;
        float requiredWidth = Mathf.Abs(width * Mathf.Cos(diagonalStripeRad)) + Mathf.Abs(height * Mathf.Sin(diagonalStripeRad));
        float requiredHeight = Mathf.Abs(width * Mathf.Sin(diagonalStripeRad)) + Mathf.Abs(height * Mathf.Cos(diagonalStripeRad));

        rectTransform.sizeDelta = new Vector2(requiredWidth, requiredHeight);
    }

    public IEnumerator PlayTransition(bool isFadeIn) {

        Vector2 screenSize = rectTransform.rect.size;
        float diagonal = screenSize.magnitude;

        Debug.Log($"{(isFadeIn ? "FadeIn" : "FadeOut")} started");

        for (int i = 0; i < stripes.Count; i++) {
            RectTransform stripe = stripes[i];
            stripe.gameObject.SetActive(true);

            (float start, float end) = isFadeIn ? (0f, diagonal) : (diagonal, 0f);

            LeanTween.cancel(stripe.gameObject);
            LeanTween.value(stripe.gameObject, start, end, transitionDuration)
                .setOnUpdate(val => {
                    if (stripe != null) {
                        stripe.sizeDelta = new Vector2(val, stripe.sizeDelta.y);
                    }
                })
                .setEase(LeanTweenType.easeInOutQuad);

            yield return new WaitForSeconds(stripeDelay);
        }

        yield return new WaitForSeconds(transitionDuration);
        Debug.Log("FadeIn complete");
    }
}
