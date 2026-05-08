using System.Collections;
using TMPro;
using UnityEngine;
namespace AniDrag.UI.Animations
{
    public class TextCoroutineAnimator : MonoBehaviour
    {
        [Header("Text Settings")]
        [SerializeField] private TMP_Text targetText;           // Drag your TMP_Text here
        [SerializeField] private string animatedText = "....";
        [SerializeField] private string staticText = "Waiting for host";

        [Header("Animation Settings")]
        [SerializeField] private float charDelay = 0.05f;       // Speed of typing (seconds per char)
        [SerializeField] private float repeatWaitTime = 1f;     // Wait before repeating (if loop)
        [SerializeField] private bool loopAnimation = true;     // Repeat after finishing?

        [Header("Control")]
        [SerializeField] private bool animateOnStart = false;

        private Coroutine animationCoroutine;
        private bool isAnimating = false;

        private void Start()
        {
            if (targetText == null)
                targetText = GetComponent<TMP_Text>();

            if (animateOnStart)
                StartAnimation();
        }

        /// <summary> Starts the typewriter animation from the beginning. </summary>
        public void StartAnimation()
        {
            StopAnimation(); // Stop any ongoing animation
            isAnimating = true;
            animationCoroutine = StartCoroutine(AnimateText());
        }

        /// <summary> Stops the animation and clears the text. </summary>
        public void StopAnimation(bool clearText = false)
        {
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);
            isAnimating = false;
            if (clearText && targetText != null)
                targetText.text = "";
        }

        /// <summary> Changes the text to be animated (resets animation if running). </summary>
        public void SetText(string newText, bool autoRestart = true)
        {
            animatedText = newText;
            if (autoRestart && isAnimating)
            {
                StartAnimation();
            }
            else if (!isAnimating)
            {
                targetText.text = staticText; // show full text immediately
            }
        }

        private IEnumerator AnimateText()
        {
            while (true)
            {
                // Type out char by char
                targetText.text = staticText;
                foreach (char c in animatedText)
                {
                    targetText.text += c;
                    yield return new WaitForSeconds(charDelay);
                }

                // If not looping, stop here
                if (!loopAnimation)
                {
                    isAnimating = false;
                    yield break;
                }

                // Wait before repeating
                yield return new WaitForSeconds(repeatWaitTime);
            }
        }
    }
}