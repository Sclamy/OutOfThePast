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
                
                Plugin.Log.LogInfo($"[SitAndTalk] Found Talk on key {talkKey}, enabled: {talkAction.enabled}");
                
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
                        Plugin.Log.LogInfo($"[SitAndTalk] Found Inspect on key {inspectKey}, enabled: {inspectAction.enabled}");
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
                
                Plugin.Log.LogInfo("[SitAndTalk] Assigned Talk to primary, Inspect to secondary");
            }
        }
        
        
        /// <summary> Prevent usagePoint from being cleared when Talk claims locked-in. </summary>
        [HarmonyPatch(typeof(Interactable.UsagePoint), nameof(Interactable.UsagePoint.TrySetUser))]
        internal static class PreventUsagePointClear
        {
            [HarmonyPrefix]
            static bool Prefix(Interactable.UsagePoint __instance, Interactable.UsePointSlot slot, Human newUser, string debug)
            {
                Plugin.Log.LogInfo($"[SitAndTalk] TrySetUser called: slot={slot}, newUser={(newUser != null ? "Player" : "null")}, isSitAndTalkActive={isSitAndTalkActive}, isRestoringSitting={isRestoringSitting}, hasStoredSitting={storedSittingInteractable != null}");
                
                // If we're in sit-and-talk mode OR restoring, and trying to clear the sitting usagePoint (set to null)
                if ((isSitAndTalkActive || isRestoringSitting) && newUser == null && storedSittingInteractable != null)
                {
                    // Check if this is the sitting usagePoint being cleared
                    if (storedSittingInteractable.usagePoint == __instance)
                    {
                        Plugin.Log.LogInfo("[SitAndTalk] BLOCKING sitting usagePoint from being cleared!");
                        return false;  // Skip clearing the usagePoint
                    }
                    else
                    {
                        Plugin.Log.LogInfo("[SitAndTalk] TrySetUser clearing different usagePoint, allowing");
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
                // After TalkTo completes, restore sitting interaction and usagePoint
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
                    // Block during active sit-and-talk OR during the restoration cleanup sequence
                    if (isSitAndTalkActive || isRestoringSitting)
                    {
                        Plugin.Log.LogInfo("[SitAndTalk] Blocking ReturnFromTransform to keep player seated");
                        
                        // If this is during restoration cleanup, clear the restoration flag
                        if (isRestoringSitting)
                        {
                            isRestoringSitting = false;
                            Plugin.Log.LogInfo("[SitAndTalk] Final cleanup blocked, all restoration complete");
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
                    // When dialogue ends (val = false), restore sitting as locked-in
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
                static bool Prefix(Interactable val)
                {
                    // Block the specific clear attempt that happens right after we restore sitting
                    if (val == null && isRestoringSitting)
                    {
                        Plugin.Log.LogInfo("[SitAndTalk] Blocking SetLockedInInteractionMode(null) - preserving restored sitting");
                        
                        // Clear sit-and-talk flags but keep restoration flag for ReturnFromTransform
                        isSitAndTalkActive = false;
                        storedSittingInteractable = null;
                        Plugin.Log.LogInfo("[SitAndTalk] Sit-and-talk flags cleared, restoration flag active for final block");
                        
                        return false;  // Skip clearing
                    }
                    
                    return true;  // Allow normal behavior
                }
            }
    }
}
