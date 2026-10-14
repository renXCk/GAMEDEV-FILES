using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public float timeLimit = 60f;
    public UIManager uiManager;
    public int totalTargets = 8;

    float timeLeft;
    int knockedDown;
    int shotsFired;
    bool finished;

    public bool IsFinished => finished;
    public float TimeLeft => timeLeft;
    public int KnockedDown => knockedDown;
    public int ShotsFired => shotsFired;

    void Start()
    {
        timeLeft = timeLimit;
        if (uiManager != null) uiManager.SetProgress(totalTargets, knockedDown);
    }

    void Update()
    {
        if (finished)
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }
        timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
        if (uiManager != null) uiManager.SetTimer(timeLeft);
        if (timeLeft <= 0f) Finish(false);
    }

    public void RegisterShot()
    {
        shotsFired++;
        if (uiManager != null) uiManager.SetShots(shotsFired);
    }

    public void TargetKnockedDown(Target target)
    {
        if (finished) return;
        knockedDown++;
        if (uiManager != null) uiManager.SetProgress(totalTargets, knockedDown);
        if (knockedDown >= totalTargets) Finish(true);
    }

    void Finish(bool won)
    {
        if (finished) return;
        finished = true;
        if (uiManager != null) uiManager.ShowResult(won, timeLeft, knockedDown, totalTargets, shotsFired);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
