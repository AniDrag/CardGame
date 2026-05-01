using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace AniDrag.UI
{
    public class SetsUiElementToSelectOnInteract : MonoBehaviour
    {
        [Header("----- Setup -----")]
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private Selectable elementToSelect;

        [Header("----- Visualization -----")]
        [SerializeField] private bool showVisualization;
        [SerializeField] private Color navigationColor;



        private void OnDrawGizmos()
        {
            if (!showVisualization) return;

            if (elementToSelect == null) return;

            Gizmos.color = navigationColor;
            Gizmos.DrawLine(transform.position, elementToSelect.transform.position);

        }
        private void Reset()
        {
            eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogError("Did not find an event system");
            }
        }

        public void JumpToElement()
        {
            if (eventSystem == null)
            {
                Debug.LogError("this gameobject has NO event system");
            }
            if (elementToSelect == null)
            {
                Debug.LogError("this gameobject has NO Element to select");
            }
            eventSystem.SetSelectedGameObject(elementToSelect.gameObject);
        }
    }
}   
