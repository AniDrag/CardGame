using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    [SerializeField]private Image cardImg;
    [SerializeField] private TMP_Text cardName;
    [SerializeField] private TMP_Text manaCost;
    [SerializeField] private TMP_Text description;
    [SerializeField] private TMP_Text stats;
    [SerializeField] private Button PlayCardBtn;

    public int id { get; private set; }
    [SerializeField] int handIndex;

    private void Start()
    {
        PlayCardBtn.onClick.AddListener(PlayCard);
        Initialize(id);
    }
    [SerializeField] private Card cardSave;
    public void Initialize(int pId)
    {
        id = pId;
        cardSave = CardRegistry.Instance.GetCard(id);
        if(cardSave == null)
        {
            Debug.LogError($"Card with ID {id} not found in CardRegistry!");
            return;
        }
        cardName.text = cardSave.name;
        cardImg.sprite = cardSave.cardSprite;
        manaCost.text = cardSave.ManaCost.ToString();
        description.text = cardSave.description;
        stats.text = cardSave.GetStats();
    }
    public void SetHandID(int handIDX) => handIndex = handIDX;
    void PlayCard()
    {
        //Debug.Log("Card " + cardName.text + " pressed");

        CardActionManager.instance.ShowCard(cardSave, handIndex);
    }
}
