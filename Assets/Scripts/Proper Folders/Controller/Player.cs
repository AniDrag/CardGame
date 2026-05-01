using AniDrag.Utility;
using AniDrag.EventBus;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Player : MonoBehaviour
{
    // TODO: Make Client publish events for the recivers once recived back from server.
    // MANA, HEALTH, CARDS Drawn , CARDS Discarded, etc.
    [Header("SizeSettings")]
    [SerializeField] private float containerWidth = 1420f;
    [SerializeField] private float cardWidth = 200f;
    
    [Header("Refrences")]
    [SerializeField] private Transform playerHandContainer;
    [SerializeField] private GameObject cardPrefab;

    [Header("UI Refrences")]
    [SerializeField] private TMP_Text mpTxt;
    [SerializeField] private TMP_Text hpTxt;

    [Header("Debuging")]
    [SerializeField] int[] handIndexToDiscard = new int[] { 0, 1 }; // Example indices to discard
    [SerializeField] int[] newCardIDToDraw = new int[] { 1 }; // Example card ID to draw
    [SerializeField] int curMana = 10;
    [SerializeField] int curHP = 30;

    private HorizontalLayoutGroup s;
    private RectTransform r;


    EventBinding<DiscardCardEvent> discardCardBinding;
    EventBinding<DrawCardEvent> drawCardBinding;
    EventBinding<ManaChangeEvent> manaChangeBinding;
    EventBinding<HealthChangeEvent> healthChangeBinding;

    private void Awake()
    {
       s = playerHandContainer.GetComponent<HorizontalLayoutGroup>();
       r = playerHandContainer.GetComponent<RectTransform>();
        if(cardPrefab == null)
        {
           cardPrefab = Resources.Load<GameObject>("Prefabs/CardPrefabs/Card_prf");
        }
        containerWidth = r.rect.width;
    }
    private void OnEnable()
    {
        discardCardBinding = new EventBinding<DiscardCardEvent>(DiscardHandEvent);
        EventBus<DiscardCardEvent>.Subscribe(discardCardBinding);

        drawCardBinding = new EventBinding<DrawCardEvent>(DrawCard);
        EventBus<DrawCardEvent>.Subscribe(drawCardBinding);

        manaChangeBinding = new EventBinding<ManaChangeEvent>(ManaInfoRecive);
        EventBus<ManaChangeEvent>.Subscribe(manaChangeBinding);

        healthChangeBinding = new EventBinding<HealthChangeEvent>(HPInfoRecive);
        EventBus<HealthChangeEvent>.Subscribe(healthChangeBinding);
    }

    private void OnDisable()
    {
        EventBus<DiscardCardEvent>.Unsubscribe(discardCardBinding);
        EventBus<DrawCardEvent>.Unsubscribe(drawCardBinding);
        EventBus<ManaChangeEvent>.Unsubscribe(manaChangeBinding);
        EventBus<HealthChangeEvent>.Unsubscribe(healthChangeBinding);
    }
    private void Start()
    {
        RearrangeHand();
    }

    #region Event Handlers
    void DiscardHandEvent(DiscardCardEvent e)
    {
        List<GameObject> toRemove = new List<GameObject>();
        for (int i = 0; i < e.handIndex.Length; i++)
        {
            int index = e.handIndex[i];
            if (index >= 0 && index < playerHandContainer.childCount)
            {
                toRemove.Add(playerHandContainer.GetChild(index).gameObject);
            }
        }
        foreach (var obj in toRemove)
        {
            Destroy(obj);
        }

        InvokeRearangeHand();
    }

    void DrawCard(DrawCardEvent e)
    {
        foreach (int id in e.IDs)
        {
            GameObject card = Instantiate(cardPrefab, playerHandContainer);
            card.GetComponent<CardUI>().Initialize(id);
        }
        InvokeRearangeHand();
    }


    void ManaInfoRecive(ManaChangeEvent e) => mpTxt.text = $"MP: {e.newMana}";
    void HPInfoRecive(HealthChangeEvent e) => hpTxt.text = $"HP: {e.newHP}";
    #endregion

    #region helpful functions
    void InvokeRearangeHand()
    {
        Invoke(nameof(RearrangeHand), 0.1f);
    }
    void RearrangeHand()
    {
        Debug.Log("Rearranging hand...");
        int childCount = playerHandContainer.childCount;
        if (childCount == 0) return;
        float spacing = childCount > 6
            ? (containerWidth - childCount * cardWidth) / childCount
            : 10f;
        s.spacing = spacing;

        ResetHandInexesOnCards();
    }

    [Button]
    void ResetHandInexesOnCards()
    {
        Debug.Log("Resetting hand indexes on cards...");
        for (int i = 0; i < playerHandContainer.childCount; i++)
        {
            CardUI child = playerHandContainer.GetChild(i).GetComponent<CardUI>();
            if (child != null)
            {
                child.SetHandID(i);
            }
        }
    }
    #endregion


    #region Debug Buttons
    [Button]
    void RemoveCardBTN()
    {
        EventBus<DiscardCardEvent>.Publish(new DiscardCardEvent(handIndexToDiscard));
    }

    [Button]
    void DrawCardBTN()
    {
        EventBus<DrawCardEvent>.Publish(new DrawCardEvent(newCardIDToDraw));
    }
    [Button]
    void ReciveManaChange()
    {
        EventBus<ManaChangeEvent>.Publish(new ManaChangeEvent(curMana));
    }
    [Button]
    void ReciveHealthChange()
    {
        EventBus<HealthChangeEvent>.Publish(new HealthChangeEvent(curHP));
    }

    #endregion
}
