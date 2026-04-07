using Alice;
using System;
using System.Collections.Generic;

public class CharacterSelectSelectionPolicy
{
    public const int MAXPLAYERS = 4;

    readonly List<int> selectionStack = new();

    public void Reset(IPlayerSelectSetting playerSelectSetting)
    {
        playerSelectSetting.ResetSelections();
        selectionStack.Clear();
    }

    public int ResolveSelectionTargetSlot(int requestingPlayerId, int joinedPlayers, bool isPlayer0Selected)
    {
        if (requestingPlayerId < 0 || requestingPlayerId >= MAXPLAYERS) {
            return 0;
        }

        if (joinedPlayers <= 1) {
            return isPlayer0Selected ? 1 : 0;
        }

        return requestingPlayerId;
    }

    public void RecordSelection(int slot)
    {
        selectionStack.Add(slot);
    }

    public bool TryPopUndoSlot(IPlayerSelectSetting playerSelectSetting, out int slot)
    {
        while (selectionStack.Count > 0) {
            var lastIndex = selectionStack.Count - 1;
            slot = selectionStack[lastIndex];
            selectionStack.RemoveAt(lastIndex);

            if (playerSelectSetting.TryGetStriker(slot, out _)) {
                return true;
            }
        }

        slot = -1;
        return false;
    }

    public int GetRequiredSlotCount(int joinedPlayers)
    {
        return Math.Max(2, joinedPlayers);
    }
}
