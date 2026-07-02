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
        if (animation == null)
            animation = GetComponent<TextCoroutineAnimator>();

        if (panel == null)
            panel = gameObject;

        RegisterButtons();

        Hide();
    }

    private void OnDestroy()
    {
        UnregisterButtons();
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        if (rollAgain != null)
            rollAgain.onClick.AddListener(OnRollAgainClicked);

        if (doubleStakeRoll != null)
            doubleStakeRoll.onClick.AddListener(OnRollAgainClicked);

        if (dontRollAgain != null)
            dontRollAgain.onClick.AddListener(OnBankPointsClicked);
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
        if (panel == null)
        {
            Client.Log("[RollAgainView] Missing panel reference.");
            return;
        }

        panel.SetActive(true);

        if (animation != null)
            animation.StartAnimation();
    }

    public void Hide()
    {
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