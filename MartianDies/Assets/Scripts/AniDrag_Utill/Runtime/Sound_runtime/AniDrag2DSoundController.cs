using System;
using System.Collections;
using System.Collections.Generic;
using AniDrag.Utility.Inspector;
using UnityEngine;
using UnityEngine.Audio;

namespace AniDrag.Utility.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class AniDrag2DSoundController : MonoBehaviour
    {
        public enum UISoundBlendMode
        {
            OneShotOverlap,
            RestartCurrent,
            IgnoreIfAlreadyPlaying,
            CrossFade
        }

        [Serializable]
        public class UISoundPreset
        {
            public string presetName = "Click";
            public AudioClip clip;

            [Range(0f, 1f)]
            public float volume = 1f;

            [Range(-3f, 3f)]
            public float pitch = 1f;
        }

        [HeaderShowIf("UI Sound Controller", size: 14, color: "#6FA8DC", defaultVisible: true)]
        [ShowIf]
        [ColorField("#6FA8DC", 0.18f)]
        [SerializeField] private AudioMixerGroup uiMixerGroup;

        [ShowIf]
        [Tooltip("Default play behaviour for UI sounds.")]
        [SerializeField] private UISoundBlendMode defaultBlendMode = UISoundBlendMode.OneShotOverlap;

        [ShowIf]
        [Tooltip("Only used when Default Blend Mode is CrossFade.")]
        [SerializeField] private float crossFadeDuration = 0.12f;

        [HeaderShowIf("Sound Folder", size: 14, color: "#93C47D", defaultVisible: true)]
        [ShowIf]
        [Tooltip("Runtime loading only works from a Resources folder. Example: Assets/Resources/Audio/UI uses path Audio/UI")]
        [SerializeField] private string resourcesFolderPath = "Audio/UI";

        [ShowIf]
        [Tooltip("If true, loads every AudioClip from the Resources folder path above.")]
        [SerializeField] private bool loadResourcesFolderOnAwake = true;

        [HeaderShowIf("Manual Presets", size: 14, color: "#F6B26B", defaultVisible: true)]
        [ShowIf]
        [Tooltip("Manual UI sound presets. Buttons can play these by preset name.")]
        [SerializeField] private UISoundPreset[] presets;

        [HeaderShowIf("Debug", size: 14, color: "#E06666", defaultVisible: false)]
        [ShowIf]
        [SerializeField] private bool showWarnings = true;

        [ShowIf]
        [Tooltip("Used by the debug play button.")]
        [SerializeField] private string debugPresetName = "Click";

        private AudioSource _mainSource;
        private AudioSource _blendSource;
        private AudioSource _activeSource;

        private readonly Dictionary<string, UISoundPreset> _presetLookup = new Dictionary<string, UISoundPreset>();
        private Coroutine _crossFadeRoutine;

        public UISoundBlendMode DefaultBlendMode => defaultBlendMode;

        private void Reset()
        {
            CacheAudioSources();
            SetupSource(_mainSource);
        }

        private void Awake()
        {
            CacheAudioSources();
            SetupSource(_mainSource);

            BuildPresetLookup();

            if (loadResourcesFolderOnAwake)
                LoadResourcesFolderAsPresets();
        }

        private void CacheAudioSources()
        {
            if (_mainSource == null)
                _mainSource = GetComponent<AudioSource>();

            if (_activeSource == null)
                _activeSource = _mainSource;
        }

        private void SetupSource(AudioSource source)
        {
            if (source == null)
                return;

            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false;

            if (uiMixerGroup != null)
                source.outputAudioMixerGroup = uiMixerGroup;
        }

        private AudioSource GetBlendSource()
        {
            if (_blendSource != null)
                return _blendSource;

            _blendSource = gameObject.AddComponent<AudioSource>();
            SetupSource(_blendSource);

            return _blendSource;
        }

        private void BuildPresetLookup()
        {
            _presetLookup.Clear();

            if (presets == null)
                return;

            foreach (UISoundPreset preset in presets)
            {
                RegisterPreset(preset);
            }
        }

        private void LoadResourcesFolderAsPresets()
        {
            if (string.IsNullOrWhiteSpace(resourcesFolderPath))
                return;

            AudioClip[] loadedClips = Resources.LoadAll<AudioClip>(resourcesFolderPath);

            if (loadedClips == null || loadedClips.Length == 0)
            {
                Warn($"No AudioClips found in Resources path: {resourcesFolderPath}");
                return;
            }

            foreach (AudioClip clip in loadedClips)
            {
                if (clip == null)
                    continue;

                UISoundPreset preset = new UISoundPreset
                {
                    presetName = clip.name,
                    clip = clip,
                    volume = 1f,
                    pitch = 1f
                };

                RegisterPreset(preset);
            }
        }

        private void RegisterPreset(UISoundPreset preset)
        {
            if (preset == null || preset.clip == null)
                return;

            if (string.IsNullOrWhiteSpace(preset.presetName))
                preset.presetName = preset.clip.name;

            _presetLookup[preset.presetName] = preset;
        }

        public void PlayPreset(string presetName)
        {
            PlayPreset(presetName, defaultBlendMode);
        }

        public void PlayPreset(string presetName, UISoundBlendMode blendMode)
        {
            if (string.IsNullOrWhiteSpace(presetName))
            {
                Warn("Preset name is empty.");
                return;
            }

            if (!_presetLookup.TryGetValue(presetName, out UISoundPreset preset))
            {
                Warn($"Preset not found: {presetName}");
                return;
            }

            PlayClip(preset.clip, preset.volume, preset.pitch, blendMode);
        }

        public void PlayClip(AudioClip clip)
        {
            PlayClip(clip, 1f, 1f, defaultBlendMode);
        }

        public void PlayClip(AudioClip clip, float volume, float pitch)
        {
            PlayClip(clip, volume, pitch, defaultBlendMode);
        }

        public void PlayClip(AudioClip clip, float volume, float pitch, UISoundBlendMode blendMode)
        {
            if (clip == null)
            {
                Warn("Tried to play null AudioClip.");
                return;
            }

            CacheAudioSources();

            switch (blendMode)
            {
                case UISoundBlendMode.OneShotOverlap:
                    PlayOneShot(clip, volume, pitch);
                    break;

                case UISoundBlendMode.RestartCurrent:
                    RestartCurrent(clip, volume, pitch);
                    break;

                case UISoundBlendMode.IgnoreIfAlreadyPlaying:
                    IgnoreIfAlreadyPlaying(clip, volume, pitch);
                    break;

                case UISoundBlendMode.CrossFade:
                    StartCrossFade(clip, volume, pitch);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(blendMode), blendMode, null);
            }
        }

        private void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            if (_mainSource == null)
                return;

            float oldPitch = _mainSource.pitch;

            _mainSource.pitch = pitch;
            _mainSource.PlayOneShot(clip, volume);
            _mainSource.pitch = oldPitch;
        }

        private void RestartCurrent(AudioClip clip, float volume, float pitch)
        {
            if (_mainSource == null)
                return;

            _mainSource.Stop();
            _mainSource.clip = clip;
            _mainSource.volume = volume;
            _mainSource.pitch = pitch;
            _mainSource.Play();

            _activeSource = _mainSource;
        }

        private void IgnoreIfAlreadyPlaying(AudioClip clip, float volume, float pitch)
        {
            if (_mainSource == null)
                return;

            if (_mainSource.isPlaying)
                return;

            RestartCurrent(clip, volume, pitch);
        }

        private void StartCrossFade(AudioClip clip, float volume, float pitch)
        {
            if (_crossFadeRoutine != null)
                StopCoroutine(_crossFadeRoutine);

            _crossFadeRoutine = StartCoroutine(CrossFadeRoutine(clip, volume, pitch));
        }

        private IEnumerator CrossFadeRoutine(AudioClip clip, float targetVolume, float pitch)
        {
            AudioSource from = _activeSource != null ? _activeSource : _mainSource;
            AudioSource to = from == _mainSource ? GetBlendSource() : _mainSource;

            SetupSource(to);

            to.Stop();
            to.clip = clip;
            to.volume = 0f;
            to.pitch = pitch;
            to.Play();

            float fromStartVolume = from != null && from.isPlaying ? from.volume : 0f;
            float timer = 0f;
            float duration = Mathf.Max(0.01f, crossFadeDuration);

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / duration;

                if (from != null)
                    from.volume = Mathf.Lerp(fromStartVolume, 0f, t);

                to.volume = Mathf.Lerp(0f, targetVolume, t);

                yield return null;
            }

            if (from != null)
            {
                from.Stop();
                from.volume = targetVolume;
            }

            to.volume = targetVolume;
            _activeSource = to;
            _crossFadeRoutine = null;
        }

        private void Warn(string message)
        {
            if (!showWarnings)
                return;

            Debug.LogWarning($"[AniDrag2DSoundController] {message}", this);
        }

        [DebugButton(title: "Debug Play Preset", size: 24f, color: "#6FA8DC", enableOnlyOnPlay: false)]
        private void DebugPlayPreset()
        {
            CacheAudioSources();
            BuildPresetLookup();

            if (loadResourcesFolderOnAwake)
                LoadResourcesFolderAsPresets();

            PlayPreset(debugPresetName);
        }
    }
}