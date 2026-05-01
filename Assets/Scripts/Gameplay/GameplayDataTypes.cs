using System;
using System.Collections.Generic;

//[Serializable]
//public class GameState
//{
//    public int Turn;            // current turn number
//    public int ActivePlayer;    // 0 or 1
//    public PlayerState P0;
//    public PlayerState P1;
//    public List<Card> Board;  // shared board? Or separate per player
//    // ... add fields as needed
//}
//
//[Serializable]
//public class PlayerState
//{
//    public int H;      // health
//    public int M;      // current mana
//    public int MM;     // max mana
//    public List<int> Hand;     // card IDs (only for local player, server omits for opponent)
//    public int DeckCount;
//    public int GraveCount;
//}
public class GameState
{
    public List<PlayerState> Players;
    public int CurrentPlayerIndex;
    public int TurnNumber;
}

public class PlayerState
{
    public int Health;
    public int Mana;
    public List<CardInstance> Hand;
    public Board Board;
}

public class Board
{
    public MonsterSlot[] FrontRow = new MonsterSlot[3];
    public EffectSlot[] BackRow = new EffectSlot[3];
}

public class MonsterSlot
{
    public CardInstance Monster;
    public bool CanAttack; // handles "summoning sickness"
}

public class EffectSlot
{
    public List<CardInstance> Effects;
}



public class CardData // static data
{
    public int Id;
    public CardType Type;
    public int Cost;

    // Monster stats
    public int Attack;
    public int Defense;
    public int Health;

    // Optional effect handler (in = playerID, object = target)
    public Action<GameState, int, object> OnPlay;
}

public class CardInstance // runtime
{
    public int InstanceId;
    public CardData Data;

    // runtime modifications
    public int CurrentHealth;
    public int BuffAttack;
}



enum GameActionType
{
    PlayCard,
    Attack,
    EndTurn,
    DrawCard,
    Concede
}

class GameAction
{
    public GameActionType Type;
    public int PlayerId;

    public int HandIndex;
    public int TargetRow;
    public int TargetCol;

    public int TargetEntityId;
}