using AniDrag.EventBus;
using AniDrag.UI.Animations;
using UnityEngine;
using UnityEngine.UI;

public class RollAgainView : MonoBehaviour
{
    #region View References

    [SerializeField] private Button rollAgain;
    [SerializeField] private Button doubleStakeRoll;
    [SerializeField] private Button dontRollAgain;
    [SerializeField] private TextCoroutineAnimator animation;
    [SerializeField] private GameObject panel;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        FindMissingReferences();
        RegisterButtons();
        Hide();
    }

    private void OnDestroy()
    {
        UnregisterButtons();
    }

    #endregion

    #region Setup

    private void FindMissingReferences()
    {
        if (panel == null)
        {
            panel = ViewAutoFind.FindGameObject(transform, "Panel_RoolAgain", "Panel_RollAgain", "Panel_StakeRoll", "Panel_StakeRole");

            if (panel == null && name.Contains("Panel_"))
                panel = gameObject;
        }

        Transform searchRoot = panel != null ? panel.transform : transform;

        if (animation == null)
            animation = GetComponent<TextCoroutineAnimator>() ?? GetComponentInChildren<TextCoroutineAnimator>(true);

        if (doubleStakeRoll == null)
        {
            doubleStakeRoll = ViewAutoFind.FindComponentByNames<Button>(searchRoot,
                "btn_DoubleStakeRoll", "BTN_DoubleStakeRoll", "btn_DoubleStake", "DoubleStakeRoll", "DoubleStake");

            if (doubleStakeRoll == null)
                doubleStakeRoll = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "double", "stake");
        }

        if (dontRollAgain == null)
        {
            dontRollAgain = ViewAutoFind.FindComponentByNames<Button>(searchRoot,
                "btn_DontRollAgain", "BTN_DontRollAgain", "btn_BankPoints", "BankPoints", "DontRollAgain", "No", "btn_No");

            if (dontRollAgain == null)
                dontRollAgain = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "dont");

            if (dontRollAgain == null)
                dontRollAgain = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "bank");
        }

        if (rollAgain == null)
        {
            rollAgain = ViewAutoFind.FindComponentByNames<Button>(searchRoot,
                "btn_RollAgain", "BTN_RollAgain", "Button_RollAgain", "RollAgain", "ReRoll", "btn_ReRoll");

            if (rollAgain == null)
                rollAgain = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "roll", "again");

            if (rollAgain == doubleStakeRoll || rollAgain == dontRollAgain)
                rollAgain = null;
        }

        if (rollAgain == null)
            Client.Log("RollAgainView", "Roll again button missing.");

        if (dontRollAgain == null)
            Client.Log("RollAgainView", "Bank/don't roll button missing.");
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        if (rollAgain != null)
        {
            rollAgain.onClick.RemoveListener(OnRollAgainClicked);
            rollAgain.onClick.AddListener(OnRollAgainClicked);
        }

        if (doubleStakeRoll != null)
        {
            doubleStakeRoll.onClick.RemoveListener(OnRollAgainClicked);
            doubleStakeRoll.onClick.AddListener(OnRollAgainClicked);
        }

        if (dontRollAgain != null)
        {
            dontRollAgain.onClick.RemoveListener(OnBankPointsClicked);
            dontRollAgain.onClick.AddListener(OnBankPointsClicked);
        }
    }

    private void UnregisterButtons()
    {
        if (rollAgain != null)
            rollAgain.onClick.RemoveListener(OnRollAgainClicked);

        if (doubleStakeRoll != null)
            doubleStakeRoll.onClick.RemoveListener(OnRollAgainClicked);

        if (dontRollAgain != null)
            dontRollAgain.onClick.RemoveListener(OnBankPointsClicked);
    }

    #endregion

    #region Public Controls

    public void Show()
    {
        FindMissingReferences();

        if (panel == null)
        {
            Client.Log("RollAgainView", "Panel reference missing. Cannot show stake prompt.");
            return;
        }

        panel.SetActive(true);

        if (animation != null)
            animation.StartAnimation();

        Client.Log("RollAgainView", "Panel activated.");
    }

    public void Hide()
    {
        FindMissingReferences();

        if (panel != null)
            panel.SetActive(false);

        if (animation != null)
            animation.StopAnimation();
    }

    #endregion

    #region UI Events

    private void OnRollAgainClicked()
    {
        EventBus<StakeRoll>.Publish(new StakeRoll(true));
        Hide();
    }

    private void OnBankPointsClicked()
    {
        EventBus<StakeRoll>.Publish(new StakeRoll(false));
        Hide();
    }

    #endregion
}
