


namespace BeatStriker.App.Views {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public class CursorView : MonoBehaviour {

        public void SetCursorVisible(bool visible) {
            Cursor.visible = visible;
        }

        public void SetCursorLockState(CursorLockMode lockMode) {
            Cursor.lockState = lockMode;
        }
    }
}