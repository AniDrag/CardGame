using System;
using AniDrag.Utility.Inspector;
using UnityEngine;
using UnityEngine.UI;

namespace AniDrag.Utility.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class ButtonBehaviour : MonoBehaviour
    {
        public enum ButtonAction
        {
            OnlySound,
            Open,
            Close,
            Toggle
        }

        public enum TargetSource
        {
            AssignedObjects,
            ThisGameObject,
            Parent,
            GrandParent,
            ChildByIndex,
            GrandChildByIndex
        }

        public enum SelfDisableMode
        {
            None,
            DisableButtonInteractable,
            DisableThisGameObject
        }

        public enum ButtonSoundMode
        {
            None,
            ManualClip,
            PresetName
        }

        [HeaderShowIf("Button Action", size: 14, color: "#6FA8DC", defaultVisible: true)]
        [ShowIf]
        [ColorField("#6FA8DC", 0.18f)]
        [Tooltip("OnlySound = only plays audio. Open = SetActive(true). Close = SetActive(false). Toggle = flips active state.")]
        [SerializeField] private ButtonAction buttonAction = ButtonAction.Toggle;

        [ShowIf]
        [ColorField("#93C47D", 0.18f)]
        [Tooltip("Where the button should find the object or objects it controls.")]
        [SerializeField] private TargetSource targetSource = TargetSource.AssignedObjects;

        [ShowIf]
        [ColorField("#F6B26B", 0.18f)]
        [Tooltip("Optional. After pressing, this button can disable itself.")]
        [SerializeField] private SelfDisableMode selfDisableMode = SelfDisableMode.None;

        [ShowIf]
        [Tooltip("If true, warning messages are printed when something is missing or an index is wrong.")]
        [SerializeField] private bool showWarnings = true;

        [ShowIf]
        [ConditionShowIf(nameof(UsesAssignedObjects))]
        [Tooltip("Objects this button opens, closes, or toggles.")]
        [SerializeField] private GameObject[] targetObjects;

        [ShowIf]
        [ConditionShowIf(nameof(UsesChildIndex))]
        [Tooltip("Used when Target Source is ChildByIndex or GrandChildByIndex.")]
        [SerializeField] private int childIndex;

        [ShowIf]
        [ConditionShowIf(nameof(UsesGrandChildIndex))]
        [Tooltip("Used when Target Source is GrandChildByIndex.")]
        [SerializeField] private int grandChildIndex;

        [HeaderShowIf("Sound Settings", size: 14, color: "#C27CFF", defaultVisible: true)]
        [ShowIf]
        [Tooltip("If false, this button will not play sound.")]
        [SerializeField] private bool playSound = true;

        [ShowIf]
        [ConditionShowIf(nameof(playSound))]
        [Tooltip("Optional. If empty, this script finds the first AniDrag2DSoundController in the scene.")]
        [SerializeField] private AniDrag2DSoundController soundController;

        [ShowIf]
        [ConditionShowIf(nameof(playSound))]
        [SerializeField] private ButtonSoundMode soundMode = ButtonSoundMode.PresetName;

        [ShowIf]
        [ConditionShowIf(nameof(UsesPresetName))]
        [Tooltip("Name of the preset inside AniDrag2DSoundController.")]
        [SerializeField] private string presetName = "Click";

        [ShowIf]
        [ConditionShowIf(nameof(UsesManualClip))]
        [Tooltip("Manual sound clip assigned directly on this button.")]
        [SerializeField] private AudioClip manualClip;

        [ShowIf]
        [ConditionShowIf(nameof(UsesManualClip))]
        [Range(0f, 1f)]
        [SerializeField] private float manualVolume = 1f;

        [ShowIf]
        [ConditionShowIf(nameof(UsesManualClip))]
        [Range(-3f, 3f)]
        [SerializeField] private float manualPitch = 1f;

        [ShowIf]
        [ConditionShowIf(nameof(playSound))]
        [Tooltip("If false, the controller default blend mode is used.")]
        [SerializeField] private bool overrideBlendMode;

        [ShowIf]
        [ConditionShowIf(nameof(UsesBlendOverride))]
        [SerializeField]
        private AniDrag2DSoundController.UISoundBlendMode blendMode =
            AniDrag2DSoundController.UISoundBlendMode.OneShotOverlap;

        private Button _button;

        private bool UsesAssignedObjects() => targetSource == TargetSource.AssignedObjects;

        private bool UsesChildIndex()
        {
            return targetSource == TargetSource.ChildByIndex ||
                   targetSource == TargetSource.GrandChildByIndex;
        }

        private bool UsesGrandChildIndex() => targetSource == TargetSource.GrandChildByIndex;

        private bool UsesManualClip()
        {
            return playSound && soundMode == ButtonSoundMode.ManualClip;
        }

        private bool UsesPresetName()
        {
            return playSound && soundMode == ButtonSoundMode.PresetName;
        }

        private bool UsesBlendOverride()
        {
            return playSound && overrideBlendMode;
        }

        private void Reset()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        private void Awake()
        {
            CacheComponents();
            ResolveSoundController();
        }

        private void OnEnable()
        {
            CacheComponents();

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonPressed);
                _button.onClick.AddListener(OnButtonPressed);
            }
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonPressed);
        }

        private void CacheComponents()
        {
            if (_button == null)
                _button = GetComponent<Button>();
        }

        private void ResolveSoundController()
        {
            if (!playSound)
                return;

            if (soundController != null)
                return;

#if UNITY_2022_2_OR_NEWER
            soundController = FindFirstObjectByType<AniDrag2DSoundController>();
#else
            soundController = FindObjectOfType<AniDrag2DSoundController>();
#endif

            if (soundController == null)
                Warn("No AniDrag2DSoundController found in the scene.");
        }

        public void OnButtonPressed()
        {
            PlayButtonSound();
            ApplyButtonAction();
            ApplySelfDisableMode();
        }

        private void PlayButtonSound()
        {
            if (!playSound)
                return;

            ResolveSoundController();

            if (soundController == null)
                return;

            AniDrag2DSoundController.UISoundBlendMode finalBlendMode =
                overrideBlendMode ? blendMode : soundController.DefaultBlendMode;

            switch (soundMode)
            {
                case ButtonSoundMode.None:
                    break;

                case ButtonSoundMode.ManualClip:
                    soundController.PlayClip(manualClip, manualVolume, manualPitch, finalBlendMode);
                    break;

                case ButtonSoundMode.PresetName:
                    soundController.PlayPreset(presetName, finalBlendMode);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ApplyButtonAction()
        {
            switch (buttonAction)
            {
                case ButtonAction.OnlySound:
                    break;

                case ButtonAction.Open:
                    SetTargetsActive(true);
                    break;

                case ButtonAction.Close:
                    SetTargetsActive(false);
                    break;

                case ButtonAction.Toggle:
                    ToggleTargets();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ApplySelfDisableMode()
        {
            switch (selfDisableMode)
            {
                case SelfDisableMode.None:
                    break;

                case SelfDisableMode.DisableButtonInteractable:
                    if (_button != null)
                        _button.interactable = false;
                    break;

                case SelfDisableMode.DisableThisGameObject:
                    gameObject.SetActive(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SetTargetsActive(bool state)
        {
            ForEachTarget(target => target.SetActive(state));
        }

        private void ToggleTargets()
        {
            ForEachTarget(target => target.SetActive(!target.activeSelf));
        }

        private void ForEachTarget(Action<GameObject> action)
        {
            if (action == null)
                return;

            switch (targetSource)
            {
                case TargetSource.AssignedObjects:
                    if (targetObjects == null || targetObjects.Length == 0)
                    {
                        Warn("No target objects assigned.");
                        return;
                    }

                    foreach (GameObject target in targetObjects)
                        SafeUseTarget(target, action);

                    break;

                case TargetSource.ThisGameObject:
                    SafeUseTarget(gameObject, action);
                    break;

                case TargetSource.Parent:
                    SafeUseTarget(transform.parent != null ? transform.parent.gameObject : null, action);
                    break;

                case TargetSource.GrandParent:
                    SafeUseTarget(
                        transform.parent != null && transform.parent.parent != null
                            ? transform.parent.parent.gameObject
                            : null,
                        action
                    );
                    break;

                case TargetSource.ChildByIndex:
                    SafeUseTarget(GetChild(childIndex), action);
                    break;

                case TargetSource.GrandChildByIndex:
                    SafeUseTarget(GetGrandChild(childIndex, grandChildIndex), action);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SafeUseTarget(GameObject target, Action<GameObject> action)
        {
            if (target == null)
            {
                Warn("Tried to control a null target.");
                return;
            }

            action.Invoke(target);
        }

        private GameObject GetChild(int index)
        {
            if (index < 0 || index >= transform.childCount)
            {
                Warn($"Child index {index} is out of range on {name}.");
                return null;
            }

            return transform.GetChild(index).gameObject;
        }

        private GameObject GetGrandChild(int child, int grandChild)
        {
            if (child < 0 || child >= transform.childCount)
            {
                Warn($"Child index {child} is out of range on {name}.");
                return null;
            }

            Transform childTransform = transform.GetChild(child);

            if (grandChild < 0 || grandChild >= childTransform.childCount)
            {
                Warn($"Grandchild index {grandChild} is out of range on {childTransform.name}.");
                return null;
            }

            return childTransform.GetChild(grandChild).gameObject;
        }

        private void Warn(string message)
        {
            if (!showWarnings)
                return;

            Debug.LogWarning($"[ButtonBehaviour] {message}", this);
        }

        [DebugButton(title: "Debug Press Button", size: 26f, color: "#6FA8DC", enableOnlyOnPlay: false)]
        private void DebugPressButton()
        {
            CacheComponents();
            ResolveSoundController();
            OnButtonPressed();
        }

        [DebugButton(title: "Debug Play Sound", size: 24f, color: "#C27CFF", enableOnlyOnPlay: false)]
        private void DebugPlaySound()
        {
            ResolveSoundController();
            PlayButtonSound();
        }

        [DebugButton(title: "Debug Open Targets", size: 24f, color: "#93C47D", enableOnlyOnPlay: false)]
        private void DebugOpenTargets()
        {
            SetTargetsActive(true);
        }

        [DebugButton(title: "Debug Close Targets", size: 24f, color: "#E06666", enableOnlyOnPlay: false)]
        private void DebugCloseTargets()
        {
            SetTargetsActive(false);
        }

        [DebugButton(title: "Debug Toggle Targets", size: 24f, color: "#FFD966", enableOnlyOnPlay: false)]
        private void DebugToggleTargets()
        {
            ToggleTargets();
        }
    }
}