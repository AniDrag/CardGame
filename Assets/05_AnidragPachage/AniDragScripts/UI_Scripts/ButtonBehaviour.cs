using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(AudioSource))]
public class ButtonBehaviour : MonoBehaviour
{
    /*
    Usage:
    - Attach to any UI Button.
    - Choose ResolveType to decide what happens when pressed.
    - Optional target object can be enabled/disabled.
    - Child / GrandChild uses the index fields.
    - Safe: auto grabs Button + AudioSource.
    */

    public enum ActivationType
    {
        OnlySound,

        DisableThisGameObject,
        EnableThisGameObject,

        DisableTargetObject,
        EnableTargetObject,

        DisableChildObject,
        EnableChildObject,

        DisableGrandChildObject,
        EnableGrandChildObject,

        DisableParent,
        EnableParent,

        DisableGrandParent,
        EnableGrandParent
    }

    [Header(
        "--------------------------------------------" +
        "\n Button Settings \n" +
        "--------------------------------------------"
    )]
    [SerializeField] public ActivationType resolveType = ActivationType.OnlySound;

    [Tooltip("Optional target object")]
    [SerializeField] private GameObject target;

    [SerializeField] private AudioClip audioClip;

    [Tooltip("Audio mixer group used by button sounds")]
    [SerializeField] private AudioMixerGroup audioAutput;

    [SerializeField] private bool showChildIndexes;

    [SerializeField] private int childIndex;
    [SerializeField] private int grandchildIndex;

    private AudioSource _audioSource;
    private Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        _audioSource = GetComponent<AudioSource>();

        if (_audioSource != null)
        {
            _audioSource.playOnAwake = false;

            if (_audioSource.clip == null && audioClip != null)
                _audioSource.clip = audioClip;

            if (_audioSource.outputAudioMixerGroup == null && audioAutput != null)
                _audioSource.outputAudioMixerGroup = audioAutput;
        }
    }

    private void OnEnable()
    {
        if (_button != null)
            _button.onClick.AddListener(OnCaseDecision);
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnCaseDecision);
    }

    private void PlaySound()
    {
        if (_audioSource == null) return;

        _audioSource.Stop();
        _audioSource.Play();
    }

    private void OnCaseDecision()
    {
        PlaySound();

        switch (resolveType)
        {
            case ActivationType.OnlySound: break;

            case ActivationType.DisableThisGameObject:
                SafeSetActive(gameObject, false);
                break;

            case ActivationType.EnableThisGameObject:
                SafeSetActive(gameObject, true);
                break;

            case ActivationType.DisableTargetObject:
                SafeSetActive(target, false);
                break;

            case ActivationType.EnableTargetObject:
                SafeSetActive(target, true);
                break;

            case ActivationType.DisableParent:
                SafeSetActive(transform.parent?.gameObject, false);
                break;

            case ActivationType.EnableParent:
                SafeSetActive(transform.parent?.gameObject, true);
                break;

            case ActivationType.DisableGrandParent:
                SafeSetActive(transform.parent?.parent?.gameObject, false);
                break;

            case ActivationType.EnableGrandParent:
                SafeSetActive(transform.parent?.parent?.gameObject, true);
                break;

            case ActivationType.DisableChildObject:
                SafeSetActive(GetChild(childIndex), false);
                break;

            case ActivationType.EnableChildObject:
                SafeSetActive(GetChild(childIndex), true);
                break;

            case ActivationType.DisableGrandChildObject:
                SafeSetActive(GetGrandChild(childIndex, grandchildIndex), false);
                break;

            case ActivationType.EnableGrandChildObject:
                SafeSetActive(GetGrandChild(childIndex, grandchildIndex), true);
                break;
        }
    }

    private GameObject GetChild(int index)
    {
        if (index < 0 || index >= transform.childCount)
        {
            Debug.LogWarning($"Child index out of range on {name}");
            return null;
        }

        return transform.GetChild(index).gameObject;
    }

    private GameObject GetGrandChild(int child, int grandchild)
    {
        if (child < 0 || child >= transform.childCount)
        {
            Debug.LogWarning($"Child index out of range on {name}");
            return null;
        }

        Transform c = transform.GetChild(child);

        if (grandchild < 0 || grandchild >= c.childCount)
        {
            Debug.LogWarning($"GrandChild index out of range on {c.name}");
            return null;
        }

        return c.GetChild(grandchild).gameObject;
    }

    private void SafeSetActive(GameObject obj, bool state)
    {
        if (obj == null)
        {
            Debug.LogWarning($"ButtonBehaviour on {name} tried to toggle null object.");
            return;
        }

        obj.SetActive(state);
    }
}