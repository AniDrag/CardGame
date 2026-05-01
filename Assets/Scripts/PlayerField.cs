using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerField : MonoBehaviour
{
    public Action<int> slotSelected;

    // Mosnter slots
    [SerializeField] CardSlot Mslot0;
    [SerializeField] CardSlot Mslot1;
    [SerializeField] CardSlot Mslot2;
    [SerializeField] CardSlot Mslot3;

    // Buff slots
    [SerializeField] CardSlot Bslot0;
    [SerializeField] CardSlot Bslot1;
    [SerializeField] CardSlot Bslot2;
    [SerializeField] CardSlot Bslot3;

    Dictionary<int, CardSlot> MonsterSlots;
    Dictionary<int, CardSlot> BuffSlots;

    private void Awake()
    {
        MonsterSlots.Add(0, Mslot0);
        MonsterSlots.Add(1, Mslot0);
        MonsterSlots.Add(2, Mslot0);
        MonsterSlots.Add(3, Mslot0);

        BuffSlots.Add(0, Bslot0);
        BuffSlots.Add(1, Bslot0);
        BuffSlots.Add(2, Bslot0);
        BuffSlots.Add(3, Bslot0);
    }
}

public class CardSlot 
{
    public Vector2Int pos; // x = collm, y = row [0,1][1,1][2,1][3,1]
                           //                    [0,0][1,0][2,0][3,0] 
    public int cardID;
    public Card card;

    // Buffs
    public Buffs currBuffs;

    public CardSlot(Card pCard)
    {
        card = pCard;
    }
}

public struct Buffs
{
    public int attactIncrese;
    public int healthIncrese;
    public int defenseIncrese;
    public int attackCount;

    /// <summary>
    /// Add buffs but for less code dups just d a true at the end to reduce the buff
    /// </summary>
    /// <param name="buffs">the buff struct that will increse it</param>
    /// <param name="remove"> True = it will multiply everything with a negative num</param>
    public void Add(Buffs buffs, bool remove = false)
    {
        attactIncrese += remove ? 1 : -1 * buffs.attactIncrese;
        healthIncrese += remove ? 1 : -1 * buffs.healthIncrese;
        defenseIncrese += remove ? 1 : -1 * buffs.defenseIncrese;
        attackCount += remove ? 1 : -1 * buffs.attackCount;
    }

    public void Cleare()
    {
        attactIncrese = 0;
        healthIncrese = 0;
        defenseIncrese = 0;
        attackCount = 0;
    }
}
