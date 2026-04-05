using HarmonyLib;
 using KickThings.Handler;
 using Photon.Pun;
 
 namespace KickThings.Patchers;
 
 public static class KickPatcher
 {
     [HarmonyPatch(typeof(CharacterGrabbing), nameof(CharacterGrabbing.KickCast))]
     [HarmonyPostfix]
     public static void KickPost(CharacterGrabbing __instance)
     {
         Plugin.Instance.ManageKick(__instance);
     }
 
     [HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRun))]
     [HarmonyPostfix]
     public static void StartRunPost(RunManager __instance)
     {
         KickThingsHandler.InitializeRegistry(PhotonNetwork.LocalPlayer.ActorNumber);
     }
 }