using UnityEngine;
using UnityEngine.UI;

public class MyCardSlot : MonoBehaviour
{
    public CardUI card { get; private set; }
    public (int, int) GetSlotID() => (slotID.x, slotID.y);

    [SerializeField] Button slotBtn;

    private Transform parentBoard;
    private Vector2Int slotID;


    private void Awake()
    {
        slotBtn = GetComponent<Button>();
        if (slotBtn == null)
        {
            Debug.LogError($"Button component not found on [Slot: {gameObject.name} ID:{slotID}]! Please ensure there is a Button component attached.");
            return;
        }
    }
    private void OnEnable()
    {
        slotBtn.onClick.AddListener(OnSlotClicked);
    }
    private void OnDisable()
    {
        slotBtn.onClick.RemoveListener(OnSlotClicked);
    }
    private void OnSlotClicked()
    {
        if(card == null)
        {
            Debug.Log($"Slot clicked: [Slot: {gameObject.name} ID:{slotID}] with no card");
            return;
        }
        Debug.Log($"Slot clicked: [Slot: {gameObject.name} ID:{slotID}]");
        CardActionManager.instance.ShowCard(parentBoard, slotID.x, slotID.y);
    }
    
    
}
