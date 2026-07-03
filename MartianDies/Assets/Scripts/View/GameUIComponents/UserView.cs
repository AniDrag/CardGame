using TMPro;
using UnityEngine;

public class UserView : MonoBehaviour
{
    #region View References

    [SerializeField] private TMP_Text userName;
    [SerializeField] private TMP_Text pointsField;

    #endregion

    #region State

    private int winPoints;

    #endregion

    #region Setup

    public void Initialize(string username, int targetWinPoints = 25, int points = 0)
    {
        FindReferences();

        if (userName == null)
            Client.Log("[UserView] Missing userName text on " + gameObject.name);

        if (pointsField == null)
            Client.Log("[UserView] Missing pointsField text on " + gameObject.name);

        if (userName != null)
            userName.text = username;

        winPoints = targetWinPoints;

        UpdateUserPoints(points);
    }

    private void FindReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        if (texts.Length == 0)
            return;

        if (userName == null)
            userName = texts[0];

        if (pointsField == null)
        {
            if (texts.Length > 1)
                pointsField = texts[1];
            else
                pointsField = texts[0];
        }
    }

    #endregion

    #region Display

    public void UpdateUserPoints(int points)
    {
        if (pointsField != null)
            pointsField.text = $"PT: {points} / {winPoints}";
    }

    #endregion
}