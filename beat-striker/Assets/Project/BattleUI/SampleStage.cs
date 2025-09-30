
using System;
using System.Collections;
using UnityEngine;

public class SampleStatge : MonoBehaviour {
    
    public Transform cameraTransform;
    public Transform pointA;
    public Transform pointB;
    public float approachDistance = 3f;
    public float approachSpeed = 2f;
    public float rotationSpeed = 90f;
    public float zoomDuration = 2f;
    public float zoomDistance = 2f;
    
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Start() {
        initialPosition = cameraTransform.position;
        initialRotation = cameraTransform.rotation;

        StartCoroutine(CameraOrbitSequence());

        Battle.Instance.outroState.OnEnter += () => {
            var winner = Array.Find(Battle.Instance.strikers, s => s.Rank == 1);
            if (winner != null) {
                StartCoroutine(ZoomToWinnerAndTransition(winner));
            }
        };
    }

    private IEnumerator CameraOrbitSequence() {

        yield return StartCoroutine(OrbitAroundPoint(pointA.position));

        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(OrbitAroundPoint(pointB.position));

        yield return StartCoroutine(ReturnToInitialPosition());

        Battle.Instance.ChangeState(Battle.Instance.playingState);
    }
    
    private IEnumerator OrbitAroundPoint(Vector3 targetPoint) {

        Vector3 direction = (targetPoint - cameraTransform.position).normalized;
        Vector3 approachPosition = targetPoint - direction * approachDistance;
        
        yield return StartCoroutine(MoveToPosition(approachPosition, targetPoint));

        yield return StartCoroutine(RotateAroundPoint(targetPoint, 180f));
    }
    
    private IEnumerator MoveToPosition(Vector3 targetPosition, Vector3 lookAtPoint) {
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(lookAtPoint - targetPosition);
        
        float elapsed = 0f;
        float duration = Vector3.Distance(startPosition, targetPosition) / approachSpeed;
        
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            t = Mathf.SmoothStep(0f, 1f, t);
            
            cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
            cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            
            yield return null;
        }
        
        cameraTransform.position = targetPosition;
        cameraTransform.rotation = targetRotation;
    }
    
    private IEnumerator RotateAroundPoint(Vector3 centerPoint, float totalAngle) {
        float currentAngle = 0f;
        Vector3 axis = Vector3.up;
        
        while (currentAngle < totalAngle) {
            float deltaAngle = rotationSpeed * Time.deltaTime;
            if (currentAngle + deltaAngle > totalAngle) {
                deltaAngle = totalAngle - currentAngle;
            }

            cameraTransform.RotateAround(centerPoint, axis, deltaAngle);

            Vector3 directionToCenter = centerPoint - cameraTransform.position;
            cameraTransform.rotation = Quaternion.LookRotation(directionToCenter);
            
            currentAngle += deltaAngle;
            yield return null;
        }
    }
    
    private IEnumerator ReturnToInitialPosition() {
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        
        float elapsed = 0f;
        float duration = Vector3.Distance(startPosition, initialPosition) / approachSpeed;
        
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            t = Mathf.SmoothStep(0f, 1f, t);
            
            cameraTransform.position = Vector3.Lerp(startPosition, initialPosition, t);
            cameraTransform.rotation = Quaternion.Slerp(startRotation, initialRotation, t);
            
            yield return null;
        }
        
        cameraTransform.position = initialPosition;
        cameraTransform.rotation = initialRotation;
    }
    
    private IEnumerator ZoomToWinnerAndTransition(Striker winner) {

        Vector3 winnerPosition = winner.transform.position;

        Vector3 directionToWinner = (winnerPosition - cameraTransform.position).normalized;
        Vector3 zoomPosition = winnerPosition - directionToWinner * zoomDistance;
        zoomPosition.y = winnerPosition.y + 1f;

        Quaternion lookAtWinnerRotation = Quaternion.LookRotation(winnerPosition - zoomPosition);

        yield return StartCoroutine(ZoomToPosition(zoomPosition, lookAtWinnerRotation));

        yield return new WaitForSeconds(zoomDuration);

        Battle.Instance.ChangeState(Battle.Instance.resultState);
    }
    
    private IEnumerator ZoomToPosition(Vector3 targetPosition, Quaternion targetRotation) {
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        
        float elapsed = 0f;
        float duration = 1.5f;
        
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            t = Mathf.SmoothStep(0f, 1f, t);
            
            cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
            cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            
            yield return null;
        }
        
        cameraTransform.position = targetPosition;
        cameraTransform.rotation = targetRotation;
    }
}