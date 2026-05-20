using AniDrag.EventBus;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameView : MonoBehaviour
{
    [Header("Disconnect")]
    [field:SerializeField] public Button disconnectButton { get; private set; }

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


    // Bindings
    private EventBinding<SelectDiceReplie> selectDiceReplieBinding;

    public void SetTurnIndicator(bool isYourTurn)
    {
        turnText.text = isYourTurn ? "Your Turn!" : "Waiting for opponent...";
        turnText.color = isYourTurn ? Color.green : Color.red;
    }
    //Debug
    private string GetDieSymbol(int val)
    {
        switch (val)
        {
            case 0: return "human";// got these images actualy pretty nice to have em
            case 1: return "Cow";
            case 2: return "Chicken";
            case 3: return "Tank"; // tank emoji approximation
            case 4: return "UFO";
            default: return "ERROR";
        }
    }

    public void ClearUsers()
    {
        foreach (Transform child in usersPanel) Destroy(child.gameObject);
        userViews.Clear();
    }
    // When user Data is updateed aka on Round results, Leave room. No one can join mid game.
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
    /// <summary>
    /// On Dice Rolled. Shows the dice rolled Filters Danger Dice.
    /// </summary>
    /// <param name="diceValues"></param>
    public void GenerateRollingZoneDice(List<int> diceValues)
    {
        foreach (Transform child in rollingZone)
            Destroy(child.gameObject);

        for (int i = 0; i < diceValues.Count; i++)
        {
            if(diceValues[i] < 0 || diceValues[i] > 4)
            {
                Client.Log("[GameView] | Error |, dice index incorrect fot FUNC: Generate Rolled Dice. Incorrect use of fuction or malicious intent passed \n IDX: " + diceValues[i]);
            }
            if (diceValues[i] == 3)
            {
                GenerateCombatZoneDice(3);
                continue;
            }
            GameObject dice = Instantiate(dicePrefab, rollingZone);
            dice.GetComponent<DiceView>().Initialize(diceValues[i], diceValues[i] != 3);
        }
    }
    /// <summary>
    /// If 3 instantiate in Ofense zone else if is 4 instantiate in Defense zone.
    /// Called On select dice results answer
    /// </summary>
    /// <param name="idx"></param>
    public void GenerateCombatZoneDice(int idx)
    {
        if (idx == 3)
        {
            GameObject dice = Instantiate(dicePrefab, offenseZone);
            dice.GetComponent<DiceView>().Initialize(idx);
        }
        else if (idx == 4)
        {
            GameObject dice = Instantiate(dicePrefab, defenseZone);
            dice.GetComponent<DiceView>().Initialize(idx);
        }
        else
            Client.Log("[GameView] | Error |, dice index incorrect fot FUNC: Generate Combat Dice. Incorrect use of fuction or malicious intent passed \n IDX: " + idx);

    }
    /// <summary>
    /// Called On select dice results answer and we allocate points here.
    /// </summary>
    /// <param name="idx"></param>
    public void GeneratePointDice(int idx)
    {
        if (idx < 3 && idx > 0)
        {
            GameObject dice = Instantiate(dicePrefab, pointZone);
            dice.GetComponent<DiceView>().Initialize(idx);
        }
        else
            Client.Log("[GameView] | Error |, dice index incorrect fot FUNC: Generate Point Dice. Incorrect use of fuction or malicious intent passed \n IDX: " + idx);
    }

    public void EnableDiceSelection(bool enable)
    {
        foreach (Transform child in rollingZone)
        {
            DiceView dice = child.GetComponent<DiceView>();
            dice.EnableBtn(enable);
        }
    }

    public void OnDiceSelection(SelectDiceReplie e)
    {
        if (e.allowed)
        {

        }
        else
        {
            EventBus<GameAnnouncment>.Publish(new GameAnnouncment("Invalid Dice selection! Malicious intent Detected, Do not cheat Strike 1/2"));
        }
    }

}