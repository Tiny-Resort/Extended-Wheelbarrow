using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Mirror;
using UnityEngine;
using HarmonyLib;
using System.Reflection;


namespace ExtendedWheelbarrow {
    
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ExtendedWheelbarrow : BaseUnityPlugin {

        public static ManualLogSource StaticLogger;
        public const string pluginGuid = "tinyresort.dinkum.extendedwheelbarrow";
        public const string pluginName = "Extended Wheelbarrow";
        public const string pluginVersion = "1.3.0";
        public static ConfigEntry<int> multiplier;
        public static ConfigEntry<bool> isDebug;
        public static int maxDirt;
        public static Dictionary<uint, int> WheelbarrowSavedValues = new Dictionary<uint, int>();
        

        public static void Dbgl(string str = "", bool pref = true) {
            if (isDebug.Value) { StaticLogger.LogInfo(str); }
        }
        
        private void Awake() {

            // Configuration
            var maxDirtEntry = Config.Bind<int>("General", "MaxDirt", 100, "The maximum number of shovels of dirt that can be emptied into the wheelbarrow before its full. RANGE: 10 - 10000");
            maxDirt = Mathf.Clamp(maxDirtEntry.Value, 10, 10000);
            multiplier = Config.Bind<int>("General", "ShovelMultiplier", 1, "The number of shovels-worth of dirt that is added to the wheelbarrow when inserting dirt. Range: 1-100");
            isDebug = Config.Bind<bool>("General", "DebugMode", false, "If true, the BepinEx console will print out debug messages related to this mod.");
        
            #region Logging
            StaticLogger = Logger;
            BepInExInfoLogInterpolatedStringHandler handler = new BepInExInfoLogInterpolatedStringHandler(18, 1, out var flag);
            if (flag) { handler.AppendLiteral("Plugin " + pluginGuid + " (v" + pluginVersion + ") loaded!"); }
            StaticLogger.LogInfo(handler);
            #endregion

            #region Patching
            Harmony harmony = new Harmony(pluginGuid);

            MethodInfo removeDirt = AccessTools.Method(typeof(Wheelbarrow), "removeDirt");
            MethodInfo removeDirtPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "removeDirtPatch");

            MethodInfo updateContents = AccessTools.Method(typeof(Wheelbarrow), "updateContents");
            MethodInfo updateContentsPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "updateContentsPatch");

            MethodInfo OnStartClient = AccessTools.Method(typeof(Wheelbarrow), "OnStartClient");
            MethodInfo OnStartClientPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "OnStartClientPatch");
            
            MethodInfo isHoldingAShovel = AccessTools.Method(typeof(Wheelbarrow), "isHoldingAShovel");
            MethodInfo isHoldingAShovelPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "isHoldingAShovelPatch");
            
            MethodInfo insertDirt = AccessTools.Method(typeof(Wheelbarrow), "insertDirt");
            MethodInfo insertDirtPrefix = AccessTools.Method(typeof(ExtendedWheelbarrow), "insertDirtPrefix");

            harmony.Patch(removeDirt, new HarmonyMethod(removeDirtPatch));
            harmony.Patch(updateContents, new HarmonyMethod(updateContentsPatch));
            harmony.Patch(OnStartClient, new HarmonyMethod(OnStartClientPatch));
            harmony.Patch(isHoldingAShovel, new HarmonyMethod(isHoldingAShovelPatch));
            harmony.Patch(insertDirt, new HarmonyMethod(insertDirtPrefix));
            #endregion

        }
        
        // Overrides the updateContents method to use our maxDirt setting instead of 10
        public static bool updateContentsPatch(Wheelbarrow __instance) {
            
            // If there's no dirt, hide the dirt model entirely
            if (__instance.totalDirt == 0) {
                __instance.dirtFillUp.gameObject.SetActive(false);
                return false;
            }
            
            // Otherwise, show the dirt model and size it appropriately
            __instance.dirtFillUp.gameObject.SetActive(true);
            float fillPercentage = 0.05f + (__instance.totalDirt / (float) maxDirt) * 0.95f;
            __instance.dirtFillUp.transform.localScale = new Vector3(fillPercentage >= 0.5f ? 1 : 0.7f, fillPercentage, fillPercentage >= 0.5f ? 1 : 0.7f);
            return false;
            
        }

        
        public static bool isHoldingAShovelPatch(Wheelbarrow __instance, InventoryItem itemToCheck, ref bool __result) {
            Dbgl($"Checking shovel contents (Time: {Time.time})");
            for (int index = 0; index < __instance.shovelsToUse.Length; ++index) {
                if ((UnityEngine.Object) itemToCheck == (UnityEngine.Object) __instance.shovelsToUse[index]) {
                    if (!WheelbarrowSavedValues.ContainsKey(__instance.netId)) {
                        Dbgl("Found dirt in shovel (Time: {Time.time})"); 
                        WheelbarrowSavedValues[__instance.netId] = __instance.totalDirt;
                    }
                    if (__instance.totalDirt >= 10 && __instance.totalDirt < maxDirt) { __instance.totalDirt = 9; }
                    __result = true;
                    return false;
                }
            }
            __result = false;
            return false;
        }

        // When inserting dirt, this make sure the new total dirt is based on the true amount that we have saved previously
        [HarmonyPrefix]
        public static void insertDirtPrefix(Wheelbarrow __instance) {
            
            if (!WheelbarrowSavedValues.ContainsKey(__instance.netId)) return;
            
            __instance.totalDirt = Mathf.Clamp(WheelbarrowSavedValues[__instance.netId] + multiplier.Value - 1, 0, maxDirt - 1);
            WheelbarrowSavedValues.Remove(__instance.netId);
            Dbgl($"Adding Dirt (Time: {Time.time})"); 
            Dbgl($"New Total Dirt: {__instance.totalDirt + multiplier.Value}/{__instance.layerIds.Length}");
            
            
        }

        // When dirt is removed from the wheelbarrow, this makes sure our saved value is used
        // Also fixes an issue with the base game that caused the layers of dirt to be wrong when removing dirt
        public static bool removeDirtPatch(Wheelbarrow __instance) {
            
            //if (!WheelbarrowSavedValues.ContainsKey(__instance.netId)) return true;
            //__instance.NetworktotalDirt = WheelbarrowSavedValues[__instance.netId] - 1;
            //WheelbarrowSavedValues.Remove(__instance.netId);
            __instance.NetworktotalDirt = __instance.totalDirt - 1;
            __instance.NetworktopDirtId = __instance.layerIds[Mathf.Clamp(__instance.totalDirt - 1, 0, maxDirt)];
            Dbgl($"Adding Dirt (Time: {Time.time})");
            Dbgl($"New Total Dirt: {__instance.totalDirt}/{__instance.layerIds.Length}");

            return false;
            
        }

        // Sets the dirt layers array to be the correct length
        public static bool OnStartClientPatch(Wheelbarrow __instance) {
            if (__instance.layerIds.Length == 10) { __instance.layerIds = new int[maxDirt]; }
            return true;
        }

    }

}
