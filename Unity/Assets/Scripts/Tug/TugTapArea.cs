using UnityEngine;
using UnityEngine.EventSystems;

// Counts a tap on pointer DOWN (not click), so fast tapping and multi-finger tapping all count.
public class TugTapArea : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] TugView view;
    public void OnPointerDown(PointerEventData eventData) => view.Tap();
}
