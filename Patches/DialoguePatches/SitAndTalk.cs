using HarmonyLib;
using UnityEngine;
using SOD.Common;
using System;
using System.Linq;
// using SOD.Common.Extensions;


namespace OutOfThePast.Patches.DialoguePatches
{
    /// <summary> Allows talking to NPCs while remaining seated. </summary>
    internal static class SitAndTalk
    {
        // Normally, "Sitting" and "Talking" are both locked-in interactions that conflict
        //   For context, while in a locked-in interaction, the player can do *most* actions
        //     except those specifically blocked, which generally includes other locked-in interactions
        // When Talk claims locked-in, the game ends Sitting (clears usagePoint, player stands up)
        // This patch allows Talk to claim locked-in (needed for dialogue UI to function)
        //   but immediately restores the sitting usagePoint to keep player physically seated
        // When dialogue ends, we restore Sitting as the locked-in interaction
        // This preserves both dialogue functionality and sitting position/animation
        

        // Tracks the sitting interactable when player enters sit-and-talk mode
        private static Interactable storedSittingInteractable = null;
        
        // Tracks if we're currently in sit-and-talk mode
        private static bool isSitAndTalkActive = false;
        
        // Tracks if we're currently restoring sitting after dialogue ends
        private static bool isRestoringSitting = false;

        // Set when we allow Get Up (Return action) - Postfix will call ReturnFromTransform since OnReturnFromHide was lost
        private static bool pendingReturnFromTransform = false;


        /// <summary> Returns true if the stored chair still exists and can be used. </summary>
        private static bool IsStoredChairValid()
        {
            if (storedSittingInteractable == null) return false;
            // Unity returns true for destroyed objects when compared to null
            if (storedSittingInteractable.usagePoint == null) return false;
            return true;
        }

        /// <summary> Clear sit-and-talk state when chair is gone (picked up, destroyed, etc.). Only logs when actually clearing. </summary>
        private static void CleanupStaleSitAndTalkState()
        {
            if (storedSittingInteractable == null && !isSitAndTalkActive && !isRestoringSitting)
                return;  // Nothing to clean
            storedSittingInteractable = null;
            isSitAndTalkActive = false;
            isRestoringSitting = false;
            pendingReturnFromTransform = false;
            Plugin.Log.LogInfo("[SitAndTalk] Chair gone or invalid - cleared sit-and-talk state");
        }
        
        
        /// <summary> Enable and prioritize Talk action when player is sitting and looking at conscious citizen. </summary>
        [HarmonyPatch(typeof(Interactable), nameof(Interactable.UpdateCurrentActions))]
        internal static class PrioritizeTalkWhileSitting
        {
            // Normally, Talk has availableWhileLockedIn = false (or loses priority completely? this is doubtful though)
            // When player sits, UpdateCurrentActions processes actions and Talk gets disabled

            [HarmonyPostfix]
            static void Postfix(Interactable __instance)
            {
                // This patch runs after UpdateCurrentActions completes, checks if the player is both sitting and looking at an NPC,
                //   and if so, force-enables the talk option, and swaps it to the primary input key for easy access

                if (__instance == null) return;

                InteractionController ic = InteractionController.Instance;
                Player player = Player.Instance;
                if (ic == null || player == null) return;
                
                // Check if player is sitting (locked into a sitting interaction)
                if (ic.lockedInInteraction == null) return;
                if (ic.lockedInInteraction.usagePoint == null) return;  // Sitting uses usagePoint
                
                // Check if looking at a conscious citizen
                if (__instance.isActor == null) return;
                if (__instance.isActor.isDead || __instance.isActor.isAsleep || __instance.isActor.isStunned) return;
                if (__instance.isActor.ai == null || __instance.isActor.ai.ko) return;
                
                // Find the Talk action in currentActions (might be disabled)
                InteractablePreset.InteractionKey talkKey = InteractablePreset.InteractionKey.none;
                foreach (var kvp in __instance.currentActions)
                {
                    if (kvp.Value.currentAction?.action?.presetName == "TalkTo")
                    {
                        talkKey = kvp.Key;
                        break;
                    }
                }
                
                if (talkKey == InteractablePreset.InteractionKey.none)
                {
                    Plugin.Log.LogWarning("[SitAndTalk] Talk action not found in currentActions");
                    return;
                }
                
                var talkAction = __instance.currentActions[talkKey];
                if (talkAction == null) return;
                
                // Plugin.Log.LogInfo($"[SitAndTalk] Found Talk on key {talkKey}, enabled: {talkAction.enabled}");
                
                // Force enable Talk (it was likely disabled by availableWhileLockedIn)
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
                        // Plugin.Log.LogInfo($"[SitAndTalk] Found Inspect on key {inspectKey}, enabled: {inspectAction.enabled}");
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
                
                // Plugin.Log.LogInfo("[SitAndTalk] Assigned Talk to primary, Inspect to secondary");
            }
        }
        
        
        /// <summary> Prevent usagePoint from being cleared when Talk claims locked-in. </summary>
        [HarmonyPatch(typeof(Interactable.UsagePoint), nameof(Interactable.UsagePoint.TrySetUser))]
        internal static class PreventUsagePointClear
        {
            [HarmonyPrefix]
            static bool Prefix(Interactable.UsagePoint __instance, Interactable.UsePointSlot slot, Human newUser, string debug)
            {
                // Verbose: only log when relevant to sit-and-talk (reduced noise)
                // Plugin.Log.LogInfo($"[SitAndTalk] TrySetUser called: slot={slot}, newUser=...");
                
                // Chair gone (picked up/destroyed) - allow normal behavior and clear our state
                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null)
                        CleanupStaleSitAndTalkState();
                    return true;
                }

                // If we're in sit-and-talk mode, restoring, OR player is sitting in our stored chair, and trying to clear the sitting usagePoint (set to null)
                bool protectingSitting = (isSitAndTalkActive || isRestoringSitting) ||
                    (InteractionController.Instance != null && InteractionController.Instance.lockedInInteraction == storedSittingInteractable);
                if (protectingSitting && newUser == null && storedSittingInteractable != null)
                {
                    // Check if this is the sitting usagePoint being cleared
                    if (storedSittingInteractable.usagePoint == __instance)
                    {
                        Plugin.Log.LogInfo("[SitAndTalk] BLOCKING sitting usagePoint from being cleared!");
                        return false;  // Skip clearing the usagePoint
                    }
                    else
                    {
                        // Plugin.Log.LogInfo("[SitAndTalk] TrySetUser clearing different usagePoint, allowing");
                    }
                }
                
                return true;  // Allow normal behavior
            }
        }
        
        /// <summary> Preserve sitting state when NPC initiates talk with player. </summary>
        [HarmonyPatch(typeof(NewAIController), nameof(NewAIController.TalkTo), new Type[] { typeof(InteractionController.ConversationType) })]
        internal static class PreserveSittingDuringTalk
        {
            [HarmonyPrefix]
            static void Prefix(NewAIController __instance, InteractionController.ConversationType convoType)
            {
                // Clear any leftover restoration flags from previous session
                if (isRestoringSitting)
                {
                    isRestoringSitting = false;
                    Plugin.Log.LogInfo("[SitAndTalk] Cleared leftover restoration flag from previous session");
                }
                
                // Check if player is currently sitting (locked-in with usagePoint)
                InteractionController ic = InteractionController.Instance;
                if (ic.lockedInInteraction == null) return;
                if (ic.lockedInInteraction.usagePoint == null) return;
                
                Plugin.Log.LogInfo("[SitAndTalk] Player is sitting, entering sit-and-talk mode");
                
                // Store the sitting interactable before TalkTo changes it
                storedSittingInteractable = ic.lockedInInteraction;
                isSitAndTalkActive = true;
            }
            
            [HarmonyPostfix]
            static void Postfix(NewAIController __instance, InteractionController.ConversationType convoType)
            {
                // After TalkTo completes, restore player's sitting interaction and usagePoint (only if chair still exists).
                // We do NOT touch NPC behavior (stopping, facing, etc.) - that is the game's responsibility.
                if (!IsStoredChairValid())
                {
                    CleanupStaleSitAndTalkState();
                    return;
                }
                if (isSitAndTalkActive && storedSittingInteractable != null)
                {
                    Plugin.Log.LogInfo("[SitAndTalk] Restoring sitting state after TalkTo");
                    
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
        
        
            /// <summary> Skip player transform/animation when talking while seated. </summary>
            [HarmonyPatch(typeof(Player), nameof(Player.TransformPlayerController))]
            internal static class SkipTransformWhileSitting
            {
                [HarmonyPrefix]
                static bool Prefix(Player __instance, PlayerTransitionPreset newEnterTransition, Interactable newInteractable)
                {
                    // If in sit-and-talk mode, skip the transform (player stays in sitting position)
                    if (isSitAndTalkActive)
                    {
                        Plugin.Log.LogInfo("[SitAndTalk] Skipping TransformPlayerController to keep player seated");
                        return false;  // Skip the transform
                    }
                    
                    return true;  // Allow normal behavior
                }
            }
            
            
            /// <summary> Prevent standing up when returning from transform during sit-and-talk. </summary>
            [HarmonyPatch(typeof(Player), nameof(Player.ReturnFromTransform))]
            internal static class PreventReturnFromTransform
            {
                [HarmonyPrefix]
                static bool Prefix(Player __instance)
                {
                    // Chair gone - allow ReturnFromTransform so player can stand
                    if (!IsStoredChairValid())
                    {
                        if (storedSittingInteractable != null) CleanupStaleSitAndTalkState();
                        return true;
                    }
                    // Block during active sit-and-talk OR during the restoration cleanup sequence
                    if (isSitAndTalkActive || isRestoringSitting)
                    {
                        Plugin.Log.LogInfo("[SitAndTalk] Blocking ReturnFromTransform to keep player seated");
                        
                        // Don't clear flags here - keep them active to continue protecting the usagePoint
                        // Flags will be cleared on next sit-and-talk or when player actually stands up
                        if (isRestoringSitting)
                        {
                            Plugin.Log.LogInfo("[SitAndTalk] Restoration protection continuing");
                        }
                        
                        return false;  // Skip the return
                    }
                    
                    return true;  // Allow normal behavior
                }
            }
        
        
            /// <summary> Restore sitting as locked-in when dialogue ends. </summary>
            [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.SetDialog))]
            internal static class RestoreSittingAfterDialogue
            {
                [HarmonyPostfix]
                static void Postfix(InteractionController __instance, bool val)
                {
                    // When dialogue ends (val = false), restore sitting as locked-in (only if chair still exists)
                    if (!IsStoredChairValid())
                    {
                        CleanupStaleSitAndTalkState();
                        return;
                    }
                    if (!val && isSitAndTalkActive && storedSittingInteractable != null)
                    {
                        Plugin.Log.LogInfo("[SitAndTalk] Dialogue ended, restoring sitting as locked-in");
                        
                        // Clear isSitAndTalkActive FIRST to prevent infinite loop
                        isSitAndTalkActive = false;
                        
                        // Set restoration flag to block subsequent cleanup
                        isRestoringSitting = true;
                        
                        // Restore sitting as the locked-in interaction
                        __instance.SetLockedInInteractionMode(storedSittingInteractable, 0, false);
                        
                        Plugin.Log.LogInfo("[SitAndTalk] Sitting restored, restoration flag set");
                    }
                }
            }
            
            
            /// <summary> Prevent game from clearing sitting immediately after we restore it. </summary>
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
                    // Switching from chair to NPC: TalkTo set Player.interactingWith=NPC before SetLockedIn.
                    // SetLockedIn's "clear other actor" then wrongly clears the NPC (we're switching TO them).
                    // Temporarily point at the chair so the clear uses chair.objectRef (no Actor) and skips clearing the NPC.
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
                
                /// <summary> Outer SetLockedInInteractionMode(null) continues after callback and overwrites our restoration. Fix it.
                /// Also: when Get Up allowed, call ReturnFromTransform since OnReturnFromHide subscription was lost. </summary>
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
                    // The original caller invoked SetLockedInInteractionMode(null), which triggered our restoration in the callback.
                    // When the callback returned, the original method continued and set lockedInInteraction = null, overwriting our chair.
                    // If we're restoring and lockedIn got cleared, re-apply our restoration.
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
                        isRestoringSitting = false;
                        // Keep storedSittingInteractable set so PreventUsagePointClear keeps protecting until player actually Gets Up
                    }
                }
            }
    }
}
