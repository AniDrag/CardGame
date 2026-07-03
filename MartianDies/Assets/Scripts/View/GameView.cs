using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class GameView : MonoBehaviour
{
    #region View References

    [Header("Disconnect")]
    [field: SerializeField] public Button disconnectButton { get; private set; }

    [Header("Users")]
    [SerializeField] private Transform usersPanel;
    [SerializeField] private GameObject userTabPrefab;

    


    [Header("Dice Fields")]
    [SerializeField] private float diceSpawnDelay = 0.08f;
    [SerializeField] private Transform rollingZone;
    [SerializeField] private Transform offenseZone;
    [SerializeField] private Transform defenseZone;
    [SerializeField] private Transform pointZone;
    [SerializeField] private GameObject dicePrefab;

    [Header("Turn Indicator")]
    [SerializeField] private TMP_Text turnText;

    [Header("Turn Stats")]
    [SerializeField] private TMP_Text turnPointsText;
    [SerializeField] private TMP_Text defenseText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text doubleStakeText;


    private int predictedTurnPoints;
    private int predictedDefense;
    private int predictedAttack;
    private bool predictedDoubleStakeActive;
    #endregion

    #region State

    private readonly Dictionary<string, UserView> userViews = new();

    private Coroutine diceSpawnCoroutine;
    #endregion

    #region Turn Display

    public void SetTurnIndicator(bool isYourTurn)
    {
        if (turnText == null)
            return;

        turnText.text = isYourTurn ? "Your Turn!" : "Waiting for opponent...";
        turnText.color = isYourTurn ? Color.green : Color.red;
    }

    public void UpdateTurnStats(int turnPoints, int defense, int attack, bool doubleStakeActive)
    {
        if (turnPointsText != null)
            turnPointsText.text = $"PT:\n {turnPoints}";

        if (defenseText != null)
            defenseText.text = $"DEF:\n {defense}";

        if (attackText != null)
            attackText.text = $"ATK:\n {attack}";

        if (doubleStakeText != null)
            doubleStakeText.text = doubleStakeActive ? "Double Stake Active: On" : "Double Stake Active: Off";
    }

    #endregion

    #region Users

    public void ClearUsers()
    {
        foreach (Transform child in usersPanel)
            Destroy(child.gameObject);

        userViews.Clear();
    }

    public void UpdateOrAddUser(string username, int points)
    {
        if (userViews.TryGetValue(username, out UserView existingView))
        {
            existingView.UpdateUserPoints(points);
            return;
        }

        GameObject go = Instantiate(userTabPrefab, usersPanel);

        UserView userView = go.GetComponent<UserView>();
        userView.Initialize(username, 25, points);

        userViews[username] = userView;
    }

    #endregion

    #region Dice Generation

    public void GenerateRollingZoneDice(List<int> dice)
    {
        if (diceSpawnCoroutine != null)
            StopCoroutine(diceSpawnCoroutine);

        ClearZone(rollingZone);

        diceSpawnCoroutine = StartCoroutine(GenerateRollingZoneDiceRoutine(dice));
    }

    public void GenerateCombatZoneDice(int diceType)
    {
        if (diceType != (int)DiceType.Tank && diceType != (int)DiceType.UFO)
        {
            Client.Log("[GameView] Invalid combat dice index: " + diceType);
            return;
        }

        Transform targetZone = diceType == (int)DiceType.Tank
            ? offenseZone
            : defenseZone;

        GameObject dice = Instantiate(dicePrefab, targetZone);

        DiceView diceView = dice.GetComponent<DiceView>();
        diceView.Initialize(diceType, false);
    }

    public void MoveSelectedDiceToZone(int diceType)
    {
        if (diceType == (int)DiceType.Tank)
        {
            Client.Log("[GameView] Server confirmed tank selection, but tanks should not be selectable.");
            return;
        }

        List<Transform> diceToMove = FindRolledDiceOfType(diceType);

        foreach (Transform diceTransform in diceToMove)
        {
            DiceView diceView = diceTransform.GetComponent<DiceView>();

            if (diceView != null)
                diceView.SetSelectable(false);

            if (IsPointDice(diceType))
            {
                diceTransform.SetParent(pointZone, false);
            }
            else if (diceType == (int)DiceType.UFO)
            {
                diceTransform.SetParent(defenseZone, false);
            }
        }
    }

    public void GeneratePointDice(int diceType)
    {
        if (!IsPointDice(diceType))
        {
            Client.Log("[GameView] Invalid point dice index: " + diceType);
            return;
        }

        GameObject dice = Instantiate(dicePrefab, pointZone);

        DiceView diceView = dice.GetComponent<DiceView>();
        diceView.Initialize(diceType, false);
    }

    #endregion

    #region Dice Selection

    public void EnableDiceSelection(bool enable)
    {
        foreach (Transform child in rollingZone)
        {
            DiceView dice = child.GetComponent<DiceView>();

            if (dice != null)
                dice.SetSelectable(enable);
        }
    }

    public void SetDiceSelectable(Dictionary<int, bool> selectableDice, bool isMyTurn)
    {
        foreach (Transform child in rollingZone)
        {
            DiceView dice = child.GetComponent<DiceView>();

            if (dice != null)
            {
                if (isMyTurn && selectableDice.TryGetValue(dice.TypeIndex, out bool selectable))
                    dice.SetSelectable(selectable);
                else
                    dice.SetSelectable(false);
            }
        }
    }

    #endregion

    #region Clearing

    public void ClearTurnDiceZones()
    {
        ClearZone(rollingZone);
        ClearZone(offenseZone);
        ClearZone(defenseZone);
        ClearZone(pointZone);
    }

    private void ClearZone(Transform zone)
    {
        if (zone == null)
            return;

        for (int i = zone.childCount - 1; i >= 0; i--)
            Destroy(zone.GetChild(i).gameObject);
    }

    #endregion

    #region Helpers

    private IEnumerator GenerateRollingZoneDiceRoutine(List<int> dice)
    {
        foreach (int diceType in dice)
        {
            if (diceType == 3)
            {
                GenerateCombatZoneDice(diceType);
            }
            else
            {
                SpawnDiceInZone(diceType, rollingZone, false);
            }

            yield return new WaitForSeconds(diceSpawnDelay);
        }

        diceSpawnCoroutine = null;
    }

    private DiceView SpawnDiceInZone(int diceType, Transform parent, bool selectable)
    {
        GameObject diceObject = Instantiate(dicePrefab, parent);

        DiceView diceView = diceObject.GetComponent<DiceView>();

        if (diceView != null)
            diceView.Initialize(diceType, selectable);

        return diceView;
    }
    private bool IsPointDice(int diceType)
    {
        return diceType >= 0 && diceType <= 2;
    }
    private List<Transform> FindRolledDiceOfType(int diceType)
    {
        List<Transform> matches = new List<Transform>();

        foreach (Transform child in rollingZone)
        {
            DiceView diceView = child.GetComponent<DiceView>();

            if (diceView == null)
                continue;

            if (diceView.TypeIndex == diceType)
                matches.Add(child);
        }

        return matches;
    }

    public void PredictSelectedDiceStats(int diceType)
    {
        int selectedCount = CountRolledDiceOfType(diceType);

        if (IsPointDice(diceType))
        {
            predictedTurnPoints += selectedCount;
        }
        else if (diceType == (int)DiceType.UFO)
        {
            predictedDefense += selectedCount;
        }

        UpdateTurnStats(
            predictedTurnPoints,
            predictedDefense,
            predictedAttack,
            predictedDoubleStakeActive
        );
    }
    public void SyncTurnStats(int turnPoints, int defense, int attack, bool doubleStakeActive)
    {
        predictedTurnPoints = turnPoints;
        predictedDefense = defense;
        predictedAttack = attack;
        predictedDoubleStakeActive = doubleStakeActive;

        UpdateTurnStats(turnPoints, defense, attack, doubleStakeActive);
    }

    private int CountRolledDiceOfType(int diceType)
    {
        int count = 0;

        foreach (Transform child in rollingZone)
        {
            DiceView diceView = child.GetComponent<DiceView>();

            if (diceView == null)
                continue;

            if (diceView.TypeIndex == diceType)
                count++;
        }

        return count;
    }

    #endregion

    #region Validation

    private bool IsValidDiceIndex(int diceType)
    {
        return diceType >= 0 && diceType <= 4;
    }

    #endregion
}