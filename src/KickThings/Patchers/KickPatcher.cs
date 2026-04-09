using HarmonyLib;
 using KickThings.Handler;
 using Photon.Pun;
 using UnityEngine.UI.Extensions;

 namespace KickThings.Patchers;
 
 public static class KickPatcher
 {
     [HarmonyPatch(typeof(CharacterGrabbing), nameof(CharacterGrabbing.KickCast))]
     [HarmonyPostfix]
     public static void KickPost(CharacterGrabbing __instance)
     {
         Plugin.Instance.ManageKick(__instance);
     }

     [HarmonyPatch(typeof(RunManager), nameof(RunManager.Awake))]
     [HarmonyPostfix]
     public static void KickThingsManagerPost(RunManager __instance)
     {
         
         __instance.gameObject.GetOrAddComponent<KickThingsHandler>();

     }
     
 }