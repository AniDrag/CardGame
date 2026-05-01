using System.Collections.Generic;
using UnityEngine;

public class CardRegistry : MonoBehaviour
{
    public static CardRegistry Instance;
    public Card[] allCards; // drag all card assets here in Inspector

    private Dictionary<int, Card> cardMap;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            cardMap = new Dictionary<int, Card>();

            Card[] allCards = Resources.LoadAll<Card>("Cards");
            if(allCards.Length == 0)
            {
                Debug.LogWarning("No cards found in Resources/Cards! Make sure to place card assets there.");
                return;
            }
                
            foreach (Card c in allCards)
                cardMap[c.ID] = c;
        }
        else
        {
            Debug.LogWarning("Multiple instances of CardRegistry detected! Destroying duplicate.");
            Destroy(gameObject);
        }

       
    }

    public Card GetCard(int id) => cardMap.TryGetValue(id, out Card c) ? c : null;
}