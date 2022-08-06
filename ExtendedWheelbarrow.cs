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

namespace TinyResort {
    
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ExtendedWheelbarrow : BaseUnityPlugin {

        public static TRPlugin Plugin;
        public const string pluginGuid = "tinyresort.dinkum.extendedwheelbarrow";
        public const string pluginName = "Extended Wheelbarrow";
        public const string pluginVersion = "2.0.0";
        
        private void Awake() {

            Plugin = TRTools.Initialize(this, Logger, 11, pluginGuid, pluginName, pluginVersion);

            #region Patching
            Plugin.QuickPatch(typeof(Wheelbarrow), "removeDirt", typeof(ExtendedWheelbarrow), "removeDirtPatch");
            Plugin.QuickPatch(typeof(Wheelbarrow), "updateContents", typeof(ExtendedWheelbarrow), "updateContentsPatch");
            Plugin.QuickPatch(typeof(Wheelbarrow), "insertDirt", typeof(ExtendedWheelbarrow), "insertDirtPrefix");
            #endregion

        }
        
        // Makes the dirt either empty or full
        public static bool updateContentsPatch(Wheelbarrow __instance) {
            __instance.dirtFillUp.gameObject.SetActive(__instance.totalDirt != 0);
            __instance.dirtFillUp.transform.localScale = new Vector3(1, 1, 1);
            return false;
        }

        // When inserting dirt, keep it at 1 but still change the dirt type
        [HarmonyPrefix]
        public static void insertDirtPrefix(Wheelbarrow __instance, int layerId) {
            __instance.totalDirt = 0;
        }

        // Makes sure dirt is never less than 1 when removing dirt
        public static bool removeDirtPatch(Wheelbarrow __instance) {
            __instance.NetworktotalDirt = 1;
            return false;
        }

    }

}
