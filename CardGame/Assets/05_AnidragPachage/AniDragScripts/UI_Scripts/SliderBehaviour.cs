using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class SliderBehaviour : MonoBehaviour
{
    public enum RoundToDecimals
    {
        one,
        two,
        three,
        four,
    }

    [Header("========= Information =========")]
    [SerializeField] string sliderTitle = "Insert Title";
    [SerializeField] string specialTitle = "Unlimited";
    [SerializeField] float minSliderValue = 0;
    [SerializeField] float maxSliderValue = 100;
    [SerializeField] public RoundToDecimals roundToDecimals = RoundToDecimals.one;
    [SerializeField] bool useIntValues;
    [Tooltip("If on it will show x / 100 else it will be x")]
    [SerializeField] bool showCap;
    [Tooltip("If using its lowest possible nuber it will say a specific word")]
    [SerializeField] bool hasSpecialTitle;

    [Header("========= Refrences =========")]
    [SerializeField] TMP_Text titleField;
    [SerializeField] TMP_Text valueField;
    [SerializeField] Slider slider;
    string extraText;
    float decimalDevider;
    private void Awake()
    {
        ErrorChecks();

        switch (roundToDecimals)
        {
            case RoundToDecimals.one:
                decimalDevider = 10;
                break;
            case RoundToDecimals.two:
                decimalDevider = 100;
                break;
            case RoundToDecimals.three:
                decimalDevider = 1000;
                break;
            case RoundToDecimals.four:
                decimalDevider = 10000;
                break;
        }
        slider.maxValue = maxSliderValue;
        slider.minValue = minSliderValue;
        titleField.text = sliderTitle;
        slider.wholeNumbers = useIntValues;
        extraText = showCap ? (" / " + maxSliderValue.ToString()) : "";
        HasValueChanged();
    }
    public void HasValueChanged()
    {
        float value = slider.value;
        if (!useIntValues)
        {
            value = value * decimalDevider;
            value = Mathf.RoundToInt(value);
            value = value / decimalDevider;
        }

        if (hasSpecialTitle && value == minSliderValue)
        {
            valueField.text = specialTitle;
        }
        else
        {
            valueField.text = value.ToString() + extraText;
        }
    }
    void ErrorChecks()
    {
        if (slider == null)
        {
            Debug.LogError("Component sliderOptions has no conected slider or reference to one");
            return;
        }
        if (maxSliderValue <= minSliderValue)
        {
            Debug.LogError("Max value of slider is smaller or = to min value, Error max value cannot be less or = to min value");
            return;
        }
        if (titleField == null || valueField == null)
        {
            Debug.LogError("Missing one of the text field refrences --> value field - OR - title field");
            return;
        }
        if (sliderTitle == "" || sliderTitle == "Insert Title")
        {
            Debug.LogWarning("no slider title allocated");
        }
    }
}

