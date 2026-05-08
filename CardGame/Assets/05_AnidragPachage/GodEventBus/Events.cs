using System;

namespace AniDrag.EventBus
{
    //Structs are allcoted on the stack, so they are more efficient than classes for small data containers.
    //AKA less pressure on the garbage collector.
    public interface IEvent { }

    #region Discard Card Events
    // request to Client script to send a message
    public struct DiscardCardRequestEvent : IEvent// will be recived by server, server will send data for new mana if sucessdully discarded, and then client will update the hand and discard pile accordingly.
    {
        public int handIndex;
        public DiscardCardRequestEvent(int pHandIndex)// : base("Discard Card Request Event")
        {
            handIndex = pHandIndex;
        }
    }

    //Replie from client that yes we can DELETE the card we tried to discard, and we can update the hand and discard pile accordingly.
    public struct DiscardCardEvent : IEvent
    {
        public int[] handIndex;
        public DiscardCardEvent(int[] pHandIndex)// : base("Discard Card Event")
        {
            handIndex = pHandIndex;
        }
    }

    public struct ManaChangeEvent : IEvent
    {
        public int newMana;
        public ManaChangeEvent(int pNewMana)// : base("Mana Change Event")
        {
            newMana = pNewMana;
        }
    }

    public struct HealthChangeEvent : IEvent {
        public int newHP;
        public HealthChangeEvent(int pNewHP)// : base("Health Change Event")
        {
            newHP = pNewHP;
        }
    }
    #endregion

    #region Draw Card Events
    public struct DrawCardEvent : IEvent
    {
        public int[] IDs; //for every ID its a draw a card. 
        public DrawCardEvent(int[] pIDs)// : base("Draw Card Event")
        {
            IDs = pIDs;
        }
    }
    // For cards that have the effect of drawing cards.
    public struct DrawCardRequestEvent : IEvent
    {
        public int count;
        public DrawCardRequestEvent(int pCount)// : base("Draw Card Request Event")
        {
            count = pCount;
        }
    }
    #endregion

    #region Play Card Events
    public struct PlayCardFromHandRequestEvent : IEvent
    {
        public int handIndex;
        public PlayCardFromHandRequestEvent(int pHandIndex)// : base("Play Card Request Event")
        {
            handIndex = pHandIndex;
        }
    }
    public struct PlayCardFromBoardRequestEvent : IEvent
    {
        public int lane;
        public int row;
        public PlayCardFromBoardRequestEvent(int pLane, int pRow)// : base("Play Card Request Event")
        {
            lane = pLane;
            row = pRow;
        }
    }
    public struct PlayCardOnTargetRequestEvent : IEvent
    {
        public int[] targetIndex;// row, coll.
        public PlayCardOnTargetRequestEvent(int[] pTargetIndex)// : base("Play Card On Target Request Event")
        {
            targetIndex = pTargetIndex;
        }
    }
    #endregion
}