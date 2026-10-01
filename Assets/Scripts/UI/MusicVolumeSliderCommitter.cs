using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Audio changes immediately; disk writes happen once when the drag or menu closes.
[RequireComponent(typeof(Slider))]
[DisallowMultipleComponent]
public sealed class MusicVolumeSliderCommitter : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IEndDragHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // Slider.OnMove can change value even with Navigation.None. Keep arrows for tracks.
        if (eventData.button == PointerEventData.InputButton.Left && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) SettingsService.Save();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) SettingsService.Save();
    }

    private void OnDisable() => SettingsService.Save();
}
