using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ToggleSwitch : MonoBehaviour
{
    [Header(
       "--------------------------------------------" +
       "\n Slider settings \n" +
       "--------------------------------------------"
    )]
    [SerializeField] private Color enabledColor = Color.white;
    [SerializeField] private Color disabledColor = Color.black;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image backgroundImage;
    public bool isAtMaxValue { get; private set; }

    private Slider _slider;
    private Button _button;

    [Header(
       "--------------------------------------------" +
       "\n Animation \n" +
       "--------------------------------------------"
    )]
    [SerializeField, Range(0.1f, 5f)] private float animationDuration = 0.5f;
    [SerializeField] private AnimationCurve slideEase = AnimationCurve.EaseInOut(0, 0, 1, 1);


    private Coroutine _animationSliderCoroutine;

    [Header(
       "--------------------------------------------" +
       "\n Events \n" +
       "--------------------------------------------"
    )]
    [SerializeField] private UnityEvent toggleON;
    [SerializeField] private UnityEvent toggleOFF;

    private void OnValidate()
    {
        _slider = GetComponent<Slider>();
        _button = GetComponentInChildren<Button>();
        _button.onClick.AddListener(Toggle);
        fillImage = transform.GetChild(1).GetChild(0).GetComponent<Image>();
        backgroundImage = transform.GetChild(0).GetComponent<Image>();
        backgroundImage.color = disabledColor;
        fillImage.color = enabledColor;
    }
    private void OnDestroy()
    {
        _button.onClick.RemoveAllListeners();
    }
    private void Start()
    {
        if (isAtMaxValue)
            toggleON?.Invoke();
        else
            toggleOFF?.Invoke();
    }
    private void Toggle()
    {
        if (_animationSliderCoroutine != null)
            StopCoroutine(_animationSliderCoroutine);

        _animationSliderCoroutine = StartCoroutine(AnimationTransition(!isAtMaxValue));
    }
    private IEnumerator AnimationTransition(bool goingToMax)
    {
        _button.interactable = false; // Disable button during animation

        float startValue = _slider.value;
        float endValue = goingToMax ? _slider.maxValue : _slider.minValue;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            _slider.value = Mathf.Lerp(startValue, endValue, slideEase.Evaluate(t));
            yield return null;
        }

        _slider.value = endValue;
        isAtMaxValue = goingToMax;

        var colors = _slider.colors;
        colors.disabledColor = isAtMaxValue ? enabledColor : disabledColor;
        _slider.colors = colors;

        if (isAtMaxValue)
            toggleON?.Invoke();
        else
            toggleOFF?.Invoke();
        

        _button.interactable = true; // Re-enable button after animation
        _animationSliderCoroutine = null; // Reset reference
    }
}