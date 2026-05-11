using OSCTools;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{
    [Header("View refrences")]
    [SerializeField] private GameView view;
    [SerializeField] private ConfirmChoiceView confirmChoice;
    [SerializeField] private RollAgainView rollAgainView;
    [SerializeField] private RoundResultsView roundResultsView;
    [SerializeField] private AnouncmentsView anouncmentsView;

    [Header("Refrences")]
    [SerializeField] private Button disconect;
    [SerializeField] private Transform usersPanel;

    private bool isYourTurn = false;

    private void Start()
    {
        if (!Refrences())
            return;
        Subscriptions();
    }
    private void Update()
    {
        
    }
    private void OnDestroy()
    {
        CleareSubscriptions();
    }
    #region Setup
    void Subscriptions()
    {
        Client.Instance.AddListener("/dice_Rolled", OnDiceRolled);
        Client.Instance.AddListener("/round_results", OnResultsPublished);
        Client.Instance.AddListener("/game_anouncment", OnAnouncmentMade);
        disconect.onClick.AddListener(()=> Client.Instance.Disconnect());
    }
    void CleareSubscriptions()
    {
        disconect.onClick.RemoveListener(() => Client.Instance.Disconnect());
    }
    bool Refrences()
    {
        view = GetComponent<GameView>();
        confirmChoice = FindFirstObjectByType<ConfirmChoiceView>();
        rollAgainView = FindFirstObjectByType<RollAgainView>();
        roundResultsView = FindFirstObjectByType<RoundResultsView>();
        anouncmentsView = FindFirstObjectByType<AnouncmentsView>();

        if(view == null)
        {
            Debug.Log("No GameView in scene! Please add missing component!");
            return false;
        }
        if(confirmChoice == null)
        {
            Debug.Log("No Confirm Choice View in scene! Please add missing component!");
            return false;
        }
        if(rollAgainView == null)
        {
            Debug.Log("No Roll Again View in scene! Please add missing component!"); 
            return false;
        }
        if(roundResultsView == null)
        {
            Debug.Log("No Round Results View in scene! Please add missing component!");
            return false;
        }
        if (anouncmentsView == null)
        {
            Debug.Log("No Anouncments View in scene! Please add missing component!");
            return false;
        }
        if (disconect == null)
        {
            Debug.Log("Missing Disconectr button refrence! Plese add or conect it !"); 
            return false;
        }
        if(usersPanel == null)
        {
            Debug.Log("Users Panel missing or not connected!! Brooo what u doing!"); 
            return false;
        }

        return true;
    }
    #endregion

    void SelectDie(int type)
    {

    }
    void OnStakeReroll(bool doReroll)
    {

    }

    /// <summary>
    /// Recives a message that is an array of all dice thrown, aka a string of numbers.
    /// 0 = human
    /// 1 = cow
    /// 2 = chicken
    /// 3 = tank
    /// 4 = UFO
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="sender"></param>
    void OnDiceRolled(OSCMessageIn msg, IPEndPoint sender)
    {
        //TODO: Cleare Dice board, Instantiate the dice, Update UI
    }
    /// <summary>
    /// Resuts are publishd for any specific reason such as Round points gained, Went to Bust
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="sender"></param>
    void OnResultsPublished(OSCMessageIn msg, IPEndPoint sender)
    {

    }
    void OnAnouncmentMade(OSCMessageIn msg, IPEndPoint sender)
    {

    }
    // probably will be an inhouse method.
    void OnRestrictedActionMade()
    {

    }
}
