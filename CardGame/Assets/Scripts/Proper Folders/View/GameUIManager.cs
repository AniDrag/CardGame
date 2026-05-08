using TMPro;
using UnityEngine;

public class GameUIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text playerHealthText;
    [SerializeField] private TMP_Text playerManaText;
    [SerializeField] private TMP_Text opponentHealthText;
    private int enemyHandSize = 5;
    private void Start()
    {
        //Client.Instance.OnPlayerStatsChanged += UpdatePlayerStats;
        //Client.Instance.OnOpponentStatsChanged += UpdateOpponentStats;
    }

    private void UpdatePlayerStats(int health, int mana)
    {
        playerHealthText.text = $"Health: {health}";
        playerManaText.text = $"Mana: {mana}";
    }

    private void UpdateOpponentStats(int health, int handSize)
    {
        opponentHealthText.text = $"Health: {health}";
        enemyHandSize = handSize;
    }
}