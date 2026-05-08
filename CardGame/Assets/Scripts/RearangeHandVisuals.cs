using AniDrag.EventBus;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RearangeHandVisuals : MonoBehaviour
{
    [SerializeField] private float containerWidth = 1420f; 
    [SerializeField] private float cardWidth = 200f;
    private HorizontalLayoutGroup s;
    private RectTransform r;

    private void Awake()
    {
        s = GetComponent<HorizontalLayoutGroup>();
        r = GetComponent<RectTransform>();
        containerWidth = r.rect.width;
        RearrangeHand();
    }
    private void Update()
    {
        RearrangeHand();
    }

    public void RearrangeHand()
    {
        int childCount = transform.childCount;
        if (childCount == 0) return;
        float spacing = childCount > 6
            ? (containerWidth - childCount * cardWidth) / childCount
            : 10f;
        s.spacing = spacing;

       // Debug.Log($"Child count: {childCount}, spacing = {spacing:F2}");
    }
}
