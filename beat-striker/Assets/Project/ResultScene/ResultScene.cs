using UnityEngine;
using Core.Utils;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;

public class ResultScene : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void GotoSelectScene()
    {
        this.GetBus().Publish(new AppMessages.RequireTransition(AppScene.CharacterSelect));
    }
}
