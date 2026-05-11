using AniDrag.EventBus;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameView : MonoBehaviour
{
    [Header("Disconnect")]
    [SerializeField] private Button disconnectButton;

    [Header("Users")]
    [SerializeField] private Transform usersPanel;
    [SerializeField] private GameObject userTabPrefab;

    [Header("Dice Fields")]
    [SerializeField] private Transform rollingZone;   // all dice shown here initially
    [SerializeField] private Transform offenseZone;   // selected dice placed? not needed for basic
    [SerializeField] private Transform defenseZone;
    [SerializeField] private Transform pointZone;
    [SerializeField] private GameObject dicePrefab;

    [Header("Turn Indicator")]
    [SerializeField] private TMP_Text turnText;

    private Dictionary<string, UserView> userViews = new();

    public Button DisconnectButton => disconnectButton;

    public void SetTurnIndicator(bool isYourTurn)
    {
        turnText.text = isYourTurn ? "Your Turn!" : "Waiting for opponent...";
        turnText.color = isYourTurn ? Color.green : Color.red;
    }

    private string GetDieSymbol(int val)
    {
        switch (val)
        {
            case 0: return "??";// got these images actualy pretty nice to have em
            case 1: return "??";
            case 2: return "??";
            case 3: return "??"; // tank emoji approximation
            case 4: return "??";
            default: return "?";
        }
    }

    public void ClearUsers()
    {
        foreach (Transform child in usersPanel) Destroy(child.gameObject);
        userViews.Clear();
    }

    public void UpdateOrAddUser(string username, int points)
    {
        if (userViews.TryGetValue(username, out var uv)) uv.UpdateUserPoints(points);
        else
        {
            GameObject go = Instantiate(userTabPrefab, usersPanel);
            var view = go.GetComponent<UserView>();
            view.Initialized(username, 25, points);
            userViews[username] = view;
        }
    }

    public void DisplayDice(List<int> diceValues)
    {
        foreach (Transform child in rollingZone) Destroy(child.gameObject);
        for (int i = 0; i < diceValues.Count; i++)
        {
            GameObject die = Instantiate(dicePrefab, rollingZone);
            int index = i;
            die.GetComponent<DiceView>().Initialize(diceValues[i],true);
        }
    }

    public void EnableDiceSelection(bool enable)
    {
        foreach (Transform child in rollingZone)
            child.GetComponent<Button>().interactable = enable;
    }

}