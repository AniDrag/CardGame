using AniDrag.EventBus;
using AniDrag.UI.Animations;
using UnityEngine;
using UnityEngine.UI;


public class RollAgainView : MonoBehaviour
{
    [SerializeField] private Button rollAgain;
    [SerializeField] private Button dontRollAgain;
    [SerializeField] private TextCoroutineAnimator animation;
    [SerializeField] private GameObject panel;

    private void Start()
    {
        animation = GetComponent<TextCoroutineAnimator>();
        rollAgain.onClick.AddListener(Play);
        dontRollAgain.onClick.AddListener(Fold);
        Hide();
    }

    public void Show(bool canStake, string message)
    {
        panel.SetActive(canStake);
        if (animation != null) animation.StartAnimation();
    }

    public void Hide()
    {
        panel.SetActive(false);
        if (animation != null) animation.StopAnimation();
    }

    void Play()
    {
        EventBus<StakeRoll>.Publish(new StakeRoll(true));
        Hide();
    }
    void Fold()
    {
        EventBus<StakeRoll>.Publish(new StakeRoll(false));
        Hide();
    }
}