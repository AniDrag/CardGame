using TMPro;
using UnityEngine;

public class UserView : MonoBehaviour
{
    [SerializeField] private TMP_Text userName;
    [SerializeField] private TMP_Text pointsField;
    private int winPoints;

    public void Initialized(string pUsername, int pWinPoints = 25, int pPoints = 0)
    {
        // Assign via Inspector if not set, otherwise find
        if (userName == null) userName = GetComponentInChildren<TMP_Text>();
        if (pointsField == null) pointsField = transform.Find("PointsText")?.GetComponent<TMP_Text>();

        if (userName == null || pointsField == null)
        {
            Client.Log("Debug", "UserView missing text components on " + gameObject.name);
            return;
        }

        userName.text = pUsername;
        winPoints = pWinPoints;
        UpdateUserPoints(pPoints);
    }


    public void UpdateUserPoints(int points)
    {
        if (pointsField != null)
            pointsField.text = $"PT: {points} / {winPoints}";
    }
}