using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public Text timerText;
    public Text targetsText;
    public Text ammoText;
    public Text shotsText;
    public GameObject resultPanel;
    public Text resultTitle;
    public Text resultDetails;

    public void SetTimer(float seconds)
    {
        if (timerText != null) timerText.text = $"TIME  {Mathf.CeilToInt(seconds):00}";
    }
    public void SetProgress(int total, int knocked)
    {
        if (targetsText != null) targetsText.text = $"TARGETS  {knocked}/{total}";
    }
    public void SetAmmo(int ammo, int magazine, bool reloading)
    {
        if (ammoText != null) ammoText.text = reloading ? "RELOADING..." : $"AMMO  {ammo} / {magazine}";
    }
    public void SetShots(int shots)
    {
        if (shotsText != null) shotsText.text = $"SHOTS  {shots}";
    }
    public void ShowResult(bool won, float timeLeft, int knocked, int total, int shots)
    {
        if (resultPanel != null) resultPanel.SetActive(true);
        if (resultTitle != null) resultTitle.text = won ? "RANGE CLEARED!" : "TIME'S UP";
        if (resultDetails != null)
            resultDetails.text = $"Time left: {Mathf.CeilToInt(timeLeft)}s\nTargets knocked down: {knocked}/{total}\nShots fired: {shots}\n\nPress T to restart";
    }
}
