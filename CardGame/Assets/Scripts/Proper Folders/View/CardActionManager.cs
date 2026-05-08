using AniDrag.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardActionManager : MonoBehaviour
{
    public static CardActionManager instance;
    [Header("Refrences")]
    [SerializeField] GameObject cardPrevObject;
    [SerializeField] private Image cardImg;
    [SerializeField] private TMP_Text cardName;
    [SerializeField] private TMP_Text manaCost;
    [SerializeField] private TMP_Text description;
    [SerializeField] private TMP_Text stats;
    [SerializeField] private Button close;

    [Header("Actions Buttons")]
    [SerializeField] private Button Play;
    [SerializeField] private Button Discard;

    private Card currCard;
    private int playerHandIDX;

    private bool isFromHand = false;





    //Debug
    EventBinding<DiscardCardRequestEvent> discardCardBinding;
    int tempMana = 100;
    int tempMaxMana = 100;

    int tempHP = 100;
    int tempMaxHP = 500;

    int currCardIDSelected = -1;
    private void OnEnable()
    {
        discardCardBinding = new EventBinding<DiscardCardRequestEvent>(DiscardCardReplie);
        EventBus<DiscardCardRequestEvent>.Subscribe(discardCardBinding);
    }
    private void Awake()
    {
        if (instance != null)
        {
            enabled = false;
        }
        else
        {
            instance = this;            
        }

        close = cardPrevObject.GetComponentInChildren<Button>();
        close.onClick.AddListener(CloseBTN);
        cardPrevObject.SetActive(false);
        if(Play == null || Discard == null)
        {
            Debug.LogError("Play or Discard button reference is missing in CardActionManager!");
            return;
        }
        Discard.onClick.AddListener(DiscardCard);
        // is a request to check if we have enough mana to play the card should be made here or in the PlayCardEvent handler? For now, we'll just publish the event and let the handler decide.
        //Play.onClick.AddListener(() => {
        //    Debug.Log("Played card: " + cardName.text);
        //    EventBus<PlayCardEvent>.Publish(new PlayCardEvent
        //    {
        //        handIndex = playerHandIDX
        //    });
        //    CloseBTN();
        //});
    }
    public void ShowCard(Transform hand, int handIDX)
    {
        currCard = CardRegistry.Instance.GetCard(hand.GetChild(handIDX).GetComponent<CardUI>().id);
        if (currCard == null) {
            Debug.Log("Card to show is null!!");
            return;
        }
        
        cardName.text = currCard.name;
        cardImg.sprite = currCard.cardSprite;
        manaCost.text = currCard.ManaCost.ToString();
        description.text = currCard.description;
        stats.text = currCard.GetStats();
        playerHandIDX = handIDX;
        isFromHand = true;
        ShowPreview();
    }
    public void ShowCard(Transform board,int lane, int row)
    {
        currCard = CardRegistry.Instance.GetCard(board.GetChild(lane).GetChild(row).GetComponent<CardSlot>().card.ID);
        if (currCard == null)
        {
            Debug.Log("Card to show is null!!");
            return;
        }

        cardName.text = currCard.name;
        cardImg.sprite = currCard.cardSprite;
        manaCost.text = currCard.ManaCost.ToString();
        description.text = currCard.description;
        stats.text = currCard.GetStats();
        playerHandIDX = -1;
        isFromHand = false;
        ShowPreview();
    }

    public void ShowPreview()
    {
        cardPrevObject.SetActive(true);
    }
    void CloseBTN() { cardPrevObject.SetActive(false); }

    void DiscardCard()
    {
        Debug.Log("Discarded card: " + cardName.text);
        EventBus<DiscardCardRequestEvent>.Publish(new DiscardCardRequestEvent
        {
            handIndex =  playerHandIDX 
        });

        CloseBTN();
    }
    void PlayCardFromHand()
    {
        Debug.Log("Played card: " + cardName.text);
        EventBus<PlayCardFromHandRequestEvent>.Publish(new PlayCardFromHandRequestEvent
        {
            handIndex = playerHandIDX
        });
        CloseBTN();
    }

    private void OnDestroy()
    {
        close.onClick.RemoveAllListeners();
    }

    //Debug method to simulate the server's response to a discard card request. In a real implementation, this would be triggered by the server after processing the discard request.
    void DiscardCardReplie(DiscardCardRequestEvent e)
    {
        Debug.Log("DiscardCardRequestEvent received for hand index: " + e.handIndex);
        
        Card CardAsses = CardRegistry.Instance.GetCard(e.handIndex);

        EventBus<ManaChangeEvent>.Publish(new ManaChangeEvent
        {
            newMana = Mathf.Min(tempMaxMana, tempMana + CardAsses.ManaCost)
        });
    }
    void CanPlayCardCardReplie(PlayCardFromHandRequestEvent e)
    {
        Debug.Log("PlayCardRequestEvent received for hand index: " + e.handIndex);

        Card CardAsses = CardRegistry.Instance.GetCard(e.handIndex);
        if(tempMana < CardAsses.ManaCost)
        {
            Debug.Log("Not enough mana to play the card: " + CardAsses.name);
            return;
        }

        Debug.Log("Can play card: " + CardAsses.name);

        currCardIDSelected = e.handIndex;


        EventBus<ManaChangeEvent>.Publish(new ManaChangeEvent
        {
            newMana = Mathf.Min(tempMaxMana, tempMana + CardAsses.ManaCost)
        });
    }
}
