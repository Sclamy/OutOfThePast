using HarmonyLib;

namespace OutOfThePast.Patches.DecorPatches
{
    /// <summary>Shows "Place" instead of "Throw" when looking at an ashtray holding a cigarette butt</summary>
    internal static class PlaceInAshtray
    {
        [HarmonyPatch(typeof(FirstPersonItemController), nameof(FirstPersonItemController.UpdateCurrentActions))]
        internal static class PatchUpdateCurrentActions
        {
            [HarmonyPostfix]
            static void Postfix(FirstPersonItemController __instance)
            {
                if (__instance.drawnItem == null || !__instance.finishedDrawingItem) return;
                if (__instance.isConsuming) return;

                // Must be holding a cigarette butt
                var selectedSlot = BioScreenController.Instance.selectedSlot;
                if (selectedSlot == null || selectedSlot.interactableID <= -1) return;
                var heldItem = selectedSlot.GetInteractable();
                if (heldItem == null) return;
                if (!heldItem.preset.name.ToLower().Contains("cigaretteend")) return;

                // Must be looking at an ashtray
                if (!InteractionController.Instance.lookingAtInteractable) return;
                var target = InteractionController.Instance.currentLookingAtInteractable;
                if (target == null || target.interactable == null) return;
                if (!target.interactable.preset.name.ToLower().Contains("ashtray")) return;

                // Find the place action on the current FPS item
                FirstPersonItem.FPSInteractionAction putDownAction = null;
                foreach (var action in __instance.drawnItem.actions)
                {
                    if (action.availability == FirstPersonItem.AttackAvailability.nearPutDown)
                    {
                        putDownAction = action;
                        break;
                    }
                }
                if (putDownAction == null) return;

                // Swap the active action from throw to place on the same key
                var key = putDownAction.GetInteractionKey();
                if (!__instance.currentActions.ContainsKey(key)) return;
                var slot = __instance.currentActions[key];
                if (!slot.enabled) return;

                slot.currentAction = putDownAction;
            }
        }
    }
}
