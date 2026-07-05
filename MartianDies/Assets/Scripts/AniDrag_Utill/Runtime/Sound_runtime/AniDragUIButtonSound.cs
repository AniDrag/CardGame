using AniDrag.Utility.Inspector;
using UnityEngine;
using UnityEngine.UI;

namespace AniDrag.Utility.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class AniDragUIButtonSound : MonoBehaviour
    {
        public enum ButtonSoundMode
        {
            None,
            ManualClip,
            PresetName
        }

        [HeaderShowIf("Button UI Sound", size: 14, color: "#6FA8DC", defaultVisible: true)]
        [ShowIf]
        [ColorField("#6FA8DC", 0.18f)]
        [SerializeField] private Button button;

        [ShowIf]
        [Tooltip("Optional. If empty, this script finds the first AniDrag2DSoundController in the scene.")]
        [SerializeField] private AniDrag2DSoundController soundController;

        [ShowIf]
        [SerializeField] private ButtonSoundMode soundMode = ButtonSoundMode.PresetName;

        [ShowIf]
        [ConditionShowIf(nameof(UsesPresetName))]
        [Tooltip("Name of the preset inside AniDrag2DSoundController.")]
        [SerializeField] private string presetName = "Click";

        [ShowIf]
        [ConditionShowIf(nameof(UsesManualClip))]
        [Tooltip("Manual clip assigned directly on this button.")]
        [SerializeField] private AudioClip manualClip;

        [ShowIf]
        [ConditionShowIf(nameof(UsesManualClip))]
        [Range(0f, 1f)]
        [SerializeField] private float manualVolume = 1f;

        [ShowIf]
        [ConditionShowIf(nameof(UsesManualClip))]
        [Range(-3f, 3f)]
        [SerializeField] private float manualPitch = 1f;

        [HeaderShowIf("Blend Override", size: 14, color: "#93C47D", defaultVisible: false)]
        [ShowIf]
        [Tooltip("If false, the controller default blend mode is used.")]
        [SerializeField] private bool overrideBlendMode;

        [ShowIf]
        [ConditionShowIf(nameof(overrideBlendMode))]
        [SerializeField]
        private AniDrag2DSoundController.UISoundBlendMode blendMode =
            AniDrag2DSoundController.UISoundBlendMode.OneShotOverlap;

        [HeaderShowIf("Options", size: 14, color: "#F6B26B", defaultVisible: false)]
        [ShowIf]
        [SerializeField] private bool playOnlyIfButtonInteractable = true;

        [ShowIf]
        [SerializeField] private bool showWarnings = true;

        private bool UsesManualClip() => soundMode == ButtonSoundMode.ManualClip;
        private bool UsesPresetName() => soundMode == ButtonSoundMode.PresetName;

        private void Reset()
        {
            CacheButton();
        }

        private void Awake()
        {
            CacheButton();
            ResolveSoundController();
        }

        private void OnEnable()
        {
            CacheButton();

            if (button != null)
            {
                button.onClick.RemoveListener(PlayButtonSound);
                button.onClick.AddListener(PlayButtonSound);
            }
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(PlayButtonSound);
        }

        private void CacheButton()
        {
            if (button == null)
                button = GetComponent<Button>();
        }

        private void ResolveSoundController()
        {
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

        public void PlayButtonSound()
        {
            if (button != null && playOnlyIfButtonInteractable && !button.interactable)
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
            }
        }

        private void Warn(string message)
        {
            if (!showWarnings)
                return;

            Debug.LogWarning($"[AniDragUIButtonSound] {message}", this);
        }

        [DebugButton(title: "Debug Play Button Sound", size: 24f, color: "#6FA8DC", enableOnlyOnPlay: false)]
        private void DebugPlayButtonSound()
        {
            PlayButtonSound();
        }
    }
}