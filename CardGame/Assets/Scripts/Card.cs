using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu( fileName = " new card", menuName = "AniDrag/Cards/New card")]
[System.Serializable]
public class Card : ScriptableObject
{
    [Header("Card Details")]
    [field: SerializeField] public int ID{ get; private set; }
    [field: SerializeField] public CardType Type { get; private set; }
    [field: SerializeField] public Sprite cardSprite{ get; private set; }
    [field: SerializeField] public string cardName { get; private set; } = "NoName";
    [TextArea]
    [field: SerializeField] public string description { get; private set; } = "No description set";
    [field: SerializeField] public int ManaCost { get; private set; } = 5;
    [field: SerializeField] public int HealthBase { get; private set; } = 10;
    [field: SerializeField] public int DamagedBase { get; private set; } = 10;
    [field: SerializeField] public int DefenseBase { get; private set; } = 10;

    public string GetStats() => $"AT: {DamagedBase} | DEF: {DefenseBase} | HP: {HealthBase}";

}
public enum CardType
{
    Monster,
    Effect,
    Special
}

//public class CardInstance
//{
//    public Card Data; // reference to ScriptableObject
//
//    public int CurrentHP;
//    public int CurrentAttack;
//    public int CurrentDefense;
//
//    public List<CardInstance> Buffs = new List<CardInstance>();
//
//    public CardInstance(Card data)
//    {
//        Data = data;
//        CurrentHP = data.HealthBase;
//        CurrentAttack = data.DamagedBase;
//        CurrentDefense = data.DefenseBase;
//    }
//}
