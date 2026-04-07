using System.Collections.Generic;
using UnityEngine;

public record CharacterSelectSlotState(int SlotIndex, bool HasGamePad, bool IsSelected, GameObject SelectedModelPrefab);

public class CharacterSelectStatusView : MonoBehaviour
{
    [SerializeField] CharacterSelectStatusSlotView[] slots;

    public void Render(IReadOnlyList<CharacterSelectSlotState> slotStates)
    {
        var activeCount = slotStates.Count;
        for (var i = 0; i < slots.Length; i++) {
            slots[i].SetVisible(i < activeCount);
        }

        for (var i = 0; i < activeCount; i++) {
            slots[i].Render(slotStates[i]);
        }
    }
}
