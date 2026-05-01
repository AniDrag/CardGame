
//using System;
using System.Collections.Generic; 
using UnityEngine;

/// <summary>
/// This is an instance created on the server. per room. well we will generate all cards on the server at start
/// And this will only coppy the list / make a reference to the list of cards.
/// then when drawing we will pass ID of he card we are picking and the client will get it from its resources.
/// IF card not found on the clientside then Error, ReInstal game to fix or User modified card ID database and cannot find card.
/// This is also where we will set the match settings and generate the list of cards that will be used in the match based on those settings.
/// In the matchmaking the Host will request these settings to be changed and then the server will update the settings.
/// </summary>
public class GameManager : MonoBehaviour
{
    private int ID = 0; // Id check for the server to have an easier time,
    private bool useSpecialCard = false;
    private bool useEffectCard = false;
    private int startingHealth = 100;
    private int startingMana = 50;
    private int ManaRegenPerTurn = 10;
    private int currentTurn = 0;
    private bool isHostsTurn = true;// false means other cliets turn, only 2 players are in ine gae so this is fine.

    private List<Card> cards = new List<Card>();

    // in game 
    public System.Action<int, int> playCardOnBoard;// row coll
    public System.Action<int> playCardFromHand;


    public void MatchSettings(bool pUseSpecialCard, bool pUseEffectCard, int pStartingHealth, int pStartingMana, int pManaRegenPerTurn)
    {
        this.useSpecialCard = pUseSpecialCard;
        this.useEffectCard = pUseEffectCard;
        this.startingHealth = pStartingHealth;
        this.startingMana = pStartingMana;
        this.ManaRegenPerTurn = pManaRegenPerTurn;
        Debug.Log($"Match settings updated: UseSpecialCard={useSpecialCard}, UseEffectCard={useEffectCard}, StartingHealth={startingHealth}, StartingMana={startingMana}, ManaRegenPerTurn={ManaRegenPerTurn}");

        GenerateUsableCards();
    }

    void GenerateUsableCards() {         cards.Clear();
       // cards += Server.RequestCards(cardType.Monsters);
       //
       // if(useSpecialCard)
       //     cards += Server.RequestCards(cardType.Special);
       //
       // if(useEffectCard)
       //    cards += Server.RequestCards(cardType.Effect);
    }

    public void ClientIsReady() { }// this is somethng on the client side ill need to manage . in matchmaking not here.

    public void StartMatch()
    {
        //Server.StartMatch(ID);// this will tell clients their HP and stuff, hmm this doesnt need to be here 
    }

    public void StopMatch() { }
    public void EndTurn() { 
        currentTurn++;
        isHostsTurn = !isHostsTurn;
        Debug.Log($"Turn ended. Current turn: {currentTurn}. Is host's turn: {isHostsTurn}");
    }
    public Card DrawCard() {
        int randomIndex = Random.Range(0, cards.Count - 1);
        return cards[randomIndex];
    }
}
