using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunEndPanelController : MonoBehaviour
{
    [Header("Panel")]
    public GameObject endPanelRoot;

    [Header("Summary Text")]
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI summaryText;

    [Header("Optional References")]
    public TrainController trainController;

    private bool runEnded;

    private void Start()
    {
        if (endPanelRoot != null)
        {
            endPanelRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (runEnded)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EndRun();
        }
    }

    public void EndRun()
    {
        if (runEnded)
        {
            return;
        }

        runEnded = true;

        SessionRunStats stats = SessionRunStats.Instance;
        if (stats != null)
        {
            stats.PrintRunSummary();
        }

        string grade = ComputeGrade(stats);
        string summary = BuildSummary(stats);

        if (gradeText != null)
        {
            gradeText.text = $"Grade: {grade}";
        }

        if (summaryText != null)
        {
            summaryText.text = summary;
        }

        if (endPanelRoot != null)
        {
            endPanelRoot.SetActive(true);
        }

        Time.timeScale = 0f;
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private string ComputeGrade(SessionRunStats stats)
    {
        if (stats == null)
        {
            return "N/A";
        }

        int score = 100;

        score -= stats.missedStops * 12;
        score -= stats.dwellTimeViolations * 8;
        score -= stats.harshBrakeCount * 4;
        score -= stats.emergencyBrakeUsageCount * 10;
        score -= stats.tractionWithDoorOpenViolations * 12;
        score -= stats.offPlatformDoorOpenCommandViolations * 10;

        int serviceBonus = Mathf.Min(20, (stats.passengerPickups + stats.passengerDropOffs) / 4);
        int accurateStopBonus = Mathf.Min(10, stats.accurateStops * 2);
        score += serviceBonus + accurateStopBonus;

        score = Mathf.Clamp(score, 0, 100);

        if (score >= 97) return "S";
        if (score >= 90) return "A";
        if (score >= 80) return "B";
        if (score >= 70) return "C";
        if (score >= 60) return "D";
        return "F";
    }

    private string BuildSummary(SessionRunStats stats)
    {
        if (stats == null)
        {
            return "No run statistics were captured for this session.";
        }

        int totalDoorViolations = stats.tractionWithDoorOpenViolations + stats.offPlatformDoorOpenCommandViolations;

        return
            $"Passengers Picked Up: {stats.passengerPickups}\n" +
            $"Passengers Dropped Off: {stats.passengerDropOffs}\n" +
            $"Accurate Stops: {stats.accurateStops}\n" +
            $"Missed Stops: {stats.missedStops}\n" +
            $"Dwell Violations: {stats.dwellTimeViolations}\n" +
            $"Harsh Brakes: {stats.harshBrakeCount}\n" +
            $"Emergency Brakes: {stats.emergencyBrakeUsageCount}\n" +
            $"Door Violations: {totalDoorViolations} ({stats.doorRunSeverity})\n" +
            $"Peak Deceleration: {stats.peakDeceleration:0.00} m/s²\n" +
            $"Peak Jerk: {stats.peakJerk:0.00} m/s³";
    }
}
