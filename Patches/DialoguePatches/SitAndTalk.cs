using HarmonyLib;
using UnityEngine;
using SOD.Common;
using System;
using System.Linq;
// using SOD.Common.Extensions;


namespace OutOfThePast.Patches.DialoguePatches
{
    /// <summary> Allows talking to NPCs while remaining seated </summary>
    internal static class SitAndTalk
    {
        // Normally, "Sitting" and "Talking" are both locked-in interactions that conflict
        //   For context, while in a locked-in interaction, the player can do *most* actions
        //     except those specifically blocked, which generally includes other locked-in interactions
        // When Talk claims locked-in, the game ends Sitting (clears usagePoint, player stands up)
        // This patch allows Talk to claim locked-in (needed for dialogue UI to function)
        //   but saves and immediately restores the sitting usagePoint to keep player physically seated
        // It also prevents the player transforms for "stand up" and "sit down" while transitioning into and out of SitAndTalk
        // When dialogue ends, we restore Sitting as the locked-in interaction
        // This preserves both dialogue functionality and sitting position/animation
        

        private static bool isSitAndTalkActive = false;  // Flag for when SitAndTalk is active
        private static bool isRestoringSitting = false;  // Flag for when actively restoring sitting (after dialogue ends)
        private static bool pendingReturnFromTransform = false;  // Flag for when Get Up is allowed (need to re-enable)

        private static Interactable storedSittingInteractable = null;  // Saved sitting interactable when player enters SitAndTalk
        

        /// <summary> Returns true if the stored chair still exists and can be used </summary>
        private static bool IsStoredChairValid()
        {
            if (storedSittingInteractable == null) return false;
            // Unity returns true for destroyed objects when compared to null
            if (storedSittingInteractable.usagePoint == null) return false;
            return true;
        }


        /// <summary> Clear sit-and-talk state when chair is gone (picked up, destroyed, etc.) </summary>
        private static void CleanupStaleSitAndTalkState()
        {
            if (storedSittingInteractable == null && !isSitAndTalkActive && !isRestoringSitting) return;  // Nothing to clean

            storedSittingInteractable = null;
            isSitAndTalkActive = false;
            isRestoringSitting = false;
            pendingReturnFromTransform = false;
        }
        

        // ------------------------------------------------------------------------------------------------

        /// <summary> Overrides interaction options to [Talk, Inspect] when sitting and looking at conscious NPC </summary>
        [HarmonyPatch(typeof(Interactable), nameof(Interactable.UpdateCurrentActions))]
        internal static class PrioritizeTalkWhileSitting
        {
            [HarmonyPostfix]
            static void Postfix(Interactable __instance)
            {
                if (__instance == null) return;

                InteractionController ic = InteractionController.Instance;
                Player player = Player.Instance;
                if (ic == null || player == null) return;
                
                // Check if player is sitting (locked into a sitting interaction)
                if (ic.lockedInInteraction == null) return;
                if (ic.lockedInInteraction.usagePoint == null) return;
                
                // Check if looking at a conscious citizen
                if (__instance.isActor == null) return;
                if (__instance.isActor.isDead || __instance.isActor.isAsleep || __instance.isActor.isStunned) return;
                if (__instance.isActor.ai == null || __instance.isActor.ai.ko) return;
                
                // Find the Talk action in currentActions
                InteractablePreset.InteractionKey talkKey = InteractablePreset.InteractionKey.none;
                foreach (var kvp in __instance.currentActions)
                {
                    if (kvp.Value.currentAction?.action?.presetName == "TalkTo")
                    {
                        talkKey = kvp.Key;
                        break;
                    }
                }
                
                if (talkKey == InteractablePreset.InteractionKey.none) return;
                
                var talkAction = __instance.currentActions[talkKey];
                if (talkAction == null) return;  // Sanity check
                
                // Force enable Talk
                talkAction.enabled = true;
                talkAction.display = true;
                
                // Find and enable Inspect action
                InteractablePreset.InteractionKey inspectKey = InteractablePreset.InteractionKey.none;
                foreach (var kvp in __instance.currentActions)
                {
                    if (kvp.Value.currentAction?.action?.presetName == "Inspect")
                    {
                        inspectKey = kvp.Key;
                        break;
                    }
                }
                
                if (inspectKey != InteractablePreset.InteractionKey.none)
                {
                    var inspectAction = __instance.currentActions[inspectKey];
                    if (inspectAction != null)
                    {
                        inspectAction.enabled = true;
                        inspectAction.display = true;
                    }
                }
                
                // Assign Talk to primary and Inspect to secondary
                var primaryKey = InteractablePreset.InteractionKey.primary;
                var secondaryKey = InteractablePreset.InteractionKey.secondary;
                
                __instance.currentActions[primaryKey] = talkAction;
                if (inspectKey != InteractablePreset.InteractionKey.none)
                {
                    __instance.currentActions[secondaryKey] = __instance.currentActions[inspectKey];
                }
            }
        }
        
        
        /// <summary> Saves chair binding before TalkTo, restores player's binding to the chair after TalkTo </summary>
        [HarmonyPatch(typeof(NewAIController), nameof(NewAIController.TalkTo), new Type[] { typeof(InteractionController.ConversationType) })]
        internal static class PreserveSittingDuringTalk
        {
            [HarmonyPrefix]
            static void Prefix(NewAIController __instance, InteractionController.ConversationType convoType)
            {
                // Save player binding to chair
                
                // Clear any leftover restoration flags
                if (isRestoringSitting)
                    isRestoringSitting = false;
                
                // Check if player is currently sitting
                InteractionController ic = InteractionController.Instance;
                if (ic.lockedInInteraction == null) return;
                if (ic.lockedInInteraction.usagePoint == null) return;
                
                // Plugin.Log.LogInfo("[SitAndTalk] Entering sit-and-talk mode");
                
                // Store the sitting interactable before TalkTo changes it
                storedSittingInteractable = ic.lockedInInteraction;
                isSitAndTalkActive = true;
            }
            
            [HarmonyPostfix]
            static void Postfix(NewAIController __instance, InteractionController.ConversationType convoType)
            {
                // Restore player binding to 
                
                if (!IsStoredChairValid())
                {
                    CleanupStaleSitAndTalkState();
                    return;
                }
                if (isSitAndTalkActive && storedSittingInteractable != null)
                {
                    // Plugin.Log.LogInfo("[SitAndTalk] Restoring sitting state after TalkTo");
                    
                    // Restore player interaction with the chair
                    Player.Instance.SetInteracting(storedSittingInteractable);
                    
                    // Restore the usagePoint to keep player physically seated
                    if (storedSittingInteractable.usagePoint != null)
                    {
                        storedSittingInteractable.usagePoint.TrySetUser(Interactable.UsePointSlot.defaultSlot, Player.Instance);
                    }
                }
            }
        }
        
        
        /// <summary> Prevents chair's usagePoint from clearing during TalkTo </summary>
        [HarmonyPatch(typeof(Interactable.UsagePoint), nameof(Interactable.UsagePoint.TrySetUser))]
        internal static class PreventUsagePointClear
        {
            [HarmonyPrefix]
            static bool Prefix(Interactable.UsagePoint __instance, Interactable.UsePointSlot slot, Human newUser, string debug)
            {
                // Chair gone - allow normal behavior and clear our state
                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null)
                        CleanupStaleSitAndTalkState();
                    return true;
                }

                // If we're protecting sitting and trying to clear the usagePoint
                bool protectingSitting = (isSitAndTalkActive || isRestoringSitting) ||
                    (InteractionController.Instance != null && InteractionController.Instance.lockedInInteraction == storedSittingInteractable);
                if (protectingSitting && newUser == null && storedSittingInteractable != null)
                {
                    if (storedSittingInteractable.usagePoint == __instance)
                    {
                        // Plugin.Log.LogInfo("[SitAndTalk] Blocking usagePoint clear");
                        return false;  // Skip clearing the usagePoint
                    }
                }
                
                return true;
            }
        }
        
        
        /// <summary> Fixes NPC-clearing bug when switching from chair to NPC, handles Get Up, and protects restoration </summary>
        [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.SetLockedInInteractionMode))]
        internal static class PreventClearingSitting
        {
            [HarmonyPrefix]
            static bool Prefix(Interactable val, InteractionController __instance)
            {
                // Chair gone - allow normal clear (only clean if we had state)
                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null) CleanupStaleSitAndTalkState();
                    return true;
                }

                // Switching from chair to NPC: TalkTo set Player.interactingWith=NPC before SetLockedIn
                // SetLockedIn's "clear other actor" then incorrectly clears the NPC (we're switching TO them)
                // Temporarily point at the chair, so the clear uses chair.objectRef (no Actor) and skips clearing the NPC
                if (val != null && val.isActor != null && isSitAndTalkActive && __instance.lockedInInteraction == storedSittingInteractable)
                {
                    Player.Instance.SetInteracting(storedSittingInteractable);
                }

                // Player voluntarily got up (Get Up action) - allow, set flag so Postfix calls ReturnFromTransform (OnReturnFromHide was lost)
                if (val == null && !isRestoringSitting && __instance.lockedInInteraction == storedSittingInteractable)
                {
                    pendingReturnFromTransform = true;
                    storedSittingInteractable = null;
                    isSitAndTalkActive = false;
                    return true;
                }

                // Block the explicit SetLockedInInteractionMode(null) from OnReturnFromTalkTo during restoration
                if (val == null && isRestoringSitting)
                {
                    Plugin.Log.LogInfo("[SitAndTalk] Blocking SetLockedInInteractionMode(null) - preserving restored sitting");
                    isSitAndTalkActive = false;
                    return false;  // Skip clearing
                }
                
                return true;  // Allow normal behavior
            }
            
            [HarmonyPostfix]
            static void Postfix(InteractionController __instance)
            {
                // Get Up was allowed - ReturnFromTransform never gets called because OnReturnFromHide was lost during sit-and-talk
                if (pendingReturnFromTransform)
                {
                    pendingReturnFromTransform = false;
                    Player.Instance?.ReturnFromTransform();
                    return;
                }

                // Chair gone - don't re-apply (only clean if we had state)
                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null) CleanupStaleSitAndTalkState();
                    return;

                }
                // The original caller invoked SetLockedInInteractionMode(null), which triggered restoration
                // When the callback returned, the original method continued and set lockedInInteraction = null, which overwrites our chair
                // If we're restoring and lockedIn got cleared, re-apply our restoration
                if (isRestoringSitting && storedSittingInteractable != null && __instance.lockedInInteraction == null)
                {
                    Plugin.Log.LogInfo("[SitAndTalk] Postfix: outer call overwrote our restoration, re-applying sitting");
                    __instance.lockedInInteraction = storedSittingInteractable;
                    __instance.lockedInInteractionRef = 0;
                    Player.Instance.SetInteracting(storedSittingInteractable);
                    if (storedSittingInteractable.usagePoint != null)
                        storedSittingInteractable.usagePoint.TrySetUser(Interactable.UsePointSlot.defaultSlot, Player.Instance);
                    storedSittingInteractable.UpdateCurrentActions();
                    __instance.InteractionRaycastCheck();
                    __instance.UpdateInteractionText();
                    Player.Instance.UpdateIllegalStatus();
                    isRestoringSitting = false;  // Keep storedSittingInteractable set so PreventUsagePointClear keeps protecting until player actually Gets Up
                }
            }
        }
        
        
        /// <summary> Prevent player standing when starting SitAndTalk </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.TransformPlayerController))]
        internal static class SkipTransformWhileSitting
        {
            [HarmonyPrefix]
            static bool Prefix(Player __instance, PlayerTransitionPreset newEnterTransition, Interactable newInteractable)
            {
                if (isSitAndTalkActive)
                {
                    // Plugin.Log.LogInfo("[SitAndTalk] Skipping transform");
                    return false;  // Skip the transform
                }
                
                return true;
            }
        }
        
        
        /// <summary> Don't stand when ending SitAndTalk either </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.ReturnFromTransform))]
        internal static class PreventReturnFromTransform
        {
            [HarmonyPrefix]
            static bool Prefix(Player __instance)
            {
                // Chair gone - allow ReturnFromTransform
                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null) CleanupStaleSitAndTalkState();
                    return true;
                }
                
                // Block during active sit-and-talk or restoration
                if (isSitAndTalkActive || isRestoringSitting)
                {
                    // Plugin.Log.LogInfo("[SitAndTalk] Blocking ReturnFromTransform");
                    return false;
                }
                
                return true;
            }
        }
        
        
        /// <summary> Restores chair as locked-in interaction when dialogue closes (back to normal sitting) </summary>
        [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.SetDialog))]
        internal static class RestoreSittingAfterDialogue
        {
            [HarmonyPostfix]
            static void Postfix(InteractionController __instance, bool val)
            {
                if (!IsStoredChairValid())
                {
                    CleanupStaleSitAndTalkState();
                    return;
                }
                if (!val && isSitAndTalkActive && storedSittingInteractable != null)
                {
                    // Plugin.Log.LogInfo("[SitAndTalk] Dialogue ended, restoring sitting");
                    
                    // Clear isSitAndTalkActive to prevent infinite loop
                    isSitAndTalkActive = false;
                    
                    // Set restoration flag to block subsequent cleanup
                    isRestoringSitting = true;
                    
                    // Restore sitting as the locked-in interaction
                    __instance.SetLockedInInteractionMode(storedSittingInteractable, 0, false);
                }
            }
        }
    }
}
