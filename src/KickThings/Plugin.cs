using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KickThings.Handler;
using KickThings.Patchers;
using Peak.Network;
using Photon.Pun;
using PhotonCustomPropsUtils;
using pworld.Scripts.Extensions;
using UnityEngine;

namespace KickThings;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    public static Plugin Instance { get; private set; } = null!;

    private static readonly HashSet<string> kickableKinematicItemList =
    [
        "Dynamite",
        "Aloe Vera",
        "Strange Gem",
        "Coconut",
        "Purple Kingberry",
        "Yellow Kingberry",
        "Beehive",
        "Black Clusterberry",
        "Red Clusterberry",
        "Yellow Clusterberry",
        "Green Clusterberry",
        "Remedy Fungus",
        "Big Egg",
        "Napberry",
        "Scorchberry",
        "Red Prickleberry",
        "Shelf Fungus",
        "Red Crispberry",
        "Yellow Crispberry",
        "Cloud Fungus",
        "Chubby Shroom",
        "Cluster Shroom",
        "Bugle Shroom",
        "Bounce Fungus",
        "Button Shroom",
        "Weird Shroom",
        "Green Crispberry",
        "Blue Shroomberry",
        "Green Shroomberry",
        "Yellow Shroomberry",
        "Red Shroomberry",
        "Orange Winterberry",
        "Yellow Winterberry",
        "Purple Shroomberry",
        "Gold Prickleberry",
        "Cactus",
        "Green Kingberry"
    ];

    private void Awake()
    {
        Log = Logger;
        Manager = PhotonCustomPropsUtilsPlugin.GetManager(Id);
        Instance = this;
        var harmony = new Harmony(Id);

        Manager.RegisterPlayerProperty<bool>(nameof(KickThingsRegistered), PlayerEventType.All,
            (player, b) => { KickThingsRegistered = b; });

        Manager.RegisterOnJoinedRoom(SetupRegistered);

        harmony.PatchAll(typeof(KickPatcher));
        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    private void SetupRegistered(Photon.Realtime.Player player)
    {
        if (PhotonNetwork.InRoom && Manager != null)
        {
            Log.LogInfo($"Registering player {player} as a KickThings user.");
            Manager.SetPlayerProperty(nameof(KickThingsRegistered), true);
        }
    }

    private static bool KickThingsRegistered = false;

    public static PhotonScopedManager? Manager { get; private set; }

    private void MakeBerriesFall(Character character, float maxBerries, List<Transform> spawnSpots,
        ref HashSet<int> activatedItemSet)
    {
        var totalSpawn = 0;
        var maxBerrySpawn = maxBerries;

        foreach (var bushSpawnSpot in spawnSpots)
        {
            var possibleItems = new Collider[5];

            var size1 = Physics.OverlapSphereNonAlloc(bushSpawnSpot.transform.position,
                0.1f, possibleItems);

            for (var j = 0; j < size1 && totalSpawn < maxBerrySpawn; j++)
            {
                if (possibleItems[j].attachedRigidbody != null &&
                    possibleItems[j].attachedRigidbody is var jrig &&
                    jrig.gameObject.TryGetComponent(out Item it) &&
                    !activatedItemSet.Contains(it.gameObject.GetInstanceID()) &&
                    jrig.isKinematic && it.itemState == ItemState.Ground)
                {
                    it.SetKinematicNetworked(false);
                    it.lastHolderCharacter = character;
                    totalSpawn++;
                    activatedItemSet.Add(it.gameObject.GetInstanceID());
                }
            }
        }
    }

    public void FallBerries(Collider collider, BerryBush bush, Character character)
    {
        if (bush.isKinematic)
        {
            Log.LogInfo($"Kicking fruit bearer: {bush}");

            var activatedItemSet = new HashSet<int>();
            MakeBerriesFall(character, bush.possibleBerries.y, bush.spawnSpots, ref activatedItemSet);

            // check for beehives

            if (bush.gameObject.name.StartsWith("Jungle_Willow"))
            {
                Log.LogInfo($"Checking for beehives on {bush}...");

                foreach (var col in Physics.OverlapBox(collider.bounds.center, collider.bounds.extents))
                {
                    if (col == null || col.attachedRigidbody == null ||
                        activatedItemSet.Contains(col.attachedRigidbody.gameObject.GetInstanceID()))
                    {
                        continue;
                    }

                    if (col.attachedRigidbody.gameObject.TryGetComponent(out Beehive hive) &&
                        !activatedItemSet.Contains(hive.gameObject.GetInstanceID()) &&
                        hive.TryGetComponent(out Item beehiveItem))
                    {
                        Log.LogInfo($"Kicking beehive {hive}");
                        beehiveItem.SetKinematicNetworked(false);
                        hive.currentBees.photonView.RPC("SetBeesAngryRPC", RpcTarget.AllBuffered, true);
                        activatedItemSet.Add(hive.gameObject.GetInstanceID());
                    }
                }
            }
        }
    }

    public void FallBerries(Collider collider, BerryVine bv, Character character)
    {
        if (bv.isKinematic)
        {
            Log.LogInfo($"Making vine berry fall: {bv}");
            var activatedItemSet = new HashSet<int>();
            MakeBerriesFall(character, bv.possibleBerries.y, bv.spawnSpots, ref activatedItemSet);
        }
    }

    public void ManageKick(CharacterGrabbing characterGrabbing)
    {
        this.StartCoroutine(DoKickThings(
            characterGrabbing.kickDelay, characterGrabbing.kickForce, characterGrabbing.character));
    }

    private static bool IsRegistered(Player player)
    {
        return PhotonNetwork.TryGetPlayer(player.GetActorNumber(), out var p) && 
               (p.IsLocal && KickThingsRegistered ||
            (!p.IsLocal &&
             p.CustomProperties.ContainsKey(
                 nameof(KickThingsRegistered))));
    }

    private IEnumerator DoKickThings(float kickDelay, float kickForce, Character character)
    {
        yield return new WaitForSeconds(kickDelay);

        var lookDir = character.data.lookDirection_Flat;
        var results = new Collider[10];

        var radius = Mathf.Max(character.refs.mainRenderer.bounds.extents.x,
            character.refs.mainRenderer.bounds.extents.z) / 2f;

        var size = Physics.OverlapSphereNonAlloc(
            character.GetBodypart(BodypartType.Foot_R).transform.position + (radius * lookDir),
            radius, results, -1, QueryTriggerInteraction.Collide);

        var kickedThings = new HashSet<int>();

        for (var i = 0; i < size; ++i)
        {
            var collider = results[i];

            if (collider == null)
            {
                continue;
            }

            if (collider.attachedRigidbody != null && collider.attachedRigidbody is var rig)
            {
                // Rigidbody-based interaction

                if (rig.TryGetComponent(out Mob mob))
                {
                    if (!kickedThings.Contains(mob.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking mob: {mob}");

                        var point = character.Center + character.data.lookDirection *
                            Vector3.Distance(character.Center, mob.Center());

                        // Requesting ownership for physics syncs 
                        mob.photonView.RequestOwnership();

                        yield return new WaitUntil(() => mob._mobItem.view.AmOwner);

                        mob.mobState = Mob.MobState.RigidbodyControlled;

                        mob.rig.AddForceAtPosition(character.data.lookDirection * kickForce, point, ForceMode.Impulse);

                        KickImpact(character, mob.gameObject,
                            point, ref
                            kickedThings);
                    }
                }
                else if (rig.TryGetComponent(out Spider spid))
                {
                    if (!kickedThings.Contains(mob.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking spider: {spid}");

                        spid.Bonk();

                        KickImpact(character, spid.gameObject,
                            collider.ClosestPoint(character.Center + character.data.lookDirection), ref
                            kickedThings);
                    }
                }
                else if (rig.TryGetComponent(out Item it) && it.itemState == ItemState.Ground)
                {
                    if (!kickedThings.Contains(it.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking item: {it}");

                        var point = character.Center + character.data.lookDirection *
                            Vector3.Distance(character.Center, it.Center());

                        // Requesting ownership for physics syncs 
                        it.photonView.RequestOwnership();

                        if (rig.isKinematic && kickableKinematicItemList.Contains(it.UIData.itemName))
                        {
                            it.SetKinematicNetworked(false);
                            it.lastHolderCharacter = character;
                        }

                        yield return new WaitUntil(() => it.view.AmOwner);

                        it.rig.AddForceAtPosition(character.data.lookDirection * kickForce, point, ForceMode.Impulse);

                        it.physicsSyncer.ForceSyncForFrames();

                        KickImpact(character, it.gameObject, point, ref
                            kickedThings);
                    }
                }
                else if (rig.TryGetComponent(out RopeSegment seg) && seg.rope is { } rope)
                {
                    // Move the rigidbody a bit

                    if (!kickedThings.Contains(rope.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking Rope: {rope}");

                        var point = character.Center + character.data.lookDirection *
                            Vector3.Distance(character.Center, seg.Center());

                        // Make every climbing character fall.
                        foreach (var chara in rope.charactersClimbing)
                        {
                            if (rope.antigrav ? chara.Center.y < point.y : chara.Center.y > point.y)
                            {
                                Log.LogWarning(
                                    $"{chara.gameObject.name} is passed the affected segment. Skipping...");
                            }

                            if (IsRegistered(chara.player))
                            {
                                Log.LogWarning(
                                    $"Dropping {chara.gameObject.name} off of {rope}.");
                                chara.view.RPC("StopRopeClimbingRpc", RpcTarget.All);
                                chara.view.RPC("RPCA_Fall", RpcTarget.All, 1);
                            }
                            else
                            {
                                Log.LogWarning(
                                    $"{chara.gameObject.name} is not registered. Not dropping them off of {rope}.");
                            }
                        }

                        KickImpact(character, rope.gameObject, point, ref
                            kickedThings);
                    }
                }
            }
            else
            {
                // Simple colliders interactions

                if (collider.GetComponentInParent<Luggage>() is { } luggage && luggage != null)
                {
                    if (!kickedThings.Contains(luggage.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking Luggage: {luggage}");
                        // Open a luggage immediately
                        luggage.Interact_CastFinished(character);

                        KickImpact(character, luggage.gameObject,
                            collider.ClosestPoint(character.Center + character.data.lookDirection), ref
                            kickedThings);
                    }
                }
                else if (collider.TryGetComponent<RopeSegment>(out var seg) && seg.rope is { } rope)
                {
                    if (!kickedThings.Contains(rope.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking no rig Rope: {rope}");

                        var point = character.Center + character.data.lookDirection *
                            Vector3.Distance(character.Center, seg.Center());

                        // Make every climbing character fall.
                        foreach (var chara in rope.charactersClimbing)
                        {
                            if (rope.antigrav ? chara.Center.y < point.y : chara.Center.y > point.y)
                            {
                                Log.LogWarning(
                                    $"{chara.gameObject.name} is passed the affected segment. Skipping...");
                            }

                            if (IsRegistered(chara.player))
                            {
                                Log.LogWarning(
                                    $"Dropping {chara.gameObject.name} off of {rope}.");
                                chara.view.RPC("StopRopeClimbingRpc", RpcTarget.All);
                                chara.view.RPC("RPCA_Fall", RpcTarget.All, 1);
                            }
                            else
                            {
                                Log.LogWarning(
                                    $"{chara.gameObject.name} is not registered. Not dropping them off of {rope}.");
                            }
                        }

                        KickImpact(character, rope.gameObject, point, ref
                            kickedThings);
                    }
                }
                else if (collider.GetComponentInParent<JungleVine>() is { } jungleVine && jungleVine != null)
                {
                    if (jungleVine.displayName.Length > 0)
                    {
                        if (!kickedThings.Contains(jungleVine.gameObject.GetInstanceID()))
                        {
                            // Is a climbable vine.
                            Log.LogInfo($"Kicking Vine: {jungleVine}");

                            // Make every climbing character fall.
                            foreach (var chara in Character.AllCharacters)
                            {
                                if (chara.data.heldVine == jungleVine && chara.data.isVineClimbing)
                                {
                                    if (IsRegistered(chara.player))
                                    {
                                        Log.LogWarning(
                                            $"Dropping {chara.gameObject.name} off of {jungleVine}.");
                                        chara.refs.vineClimbing.view.RPC("StopVineClimbingRpc", RpcTarget.All);
                                        chara.view.RPC("RPCA_Fall", RpcTarget.All, 1);
                                    }
                                    else
                                    {
                                        Log.LogWarning($"{chara.gameObject.name} is not registered. Skipping...");
                                    }
                                }
                            }

                            // Check if it's also a berry vine

                            if (jungleVine.TryGetComponent(out BerryVine bv))
                            {
                                FallBerries(collider, bv, character);
                            }

                            KickImpact(character, jungleVine.gameObject,
                                collider.ClosestPoint(character.Center + character.data.lookDirection), ref
                                kickedThings);
                        }
                    }
                    else if (jungleVine.gameObject.TryGetComponent(out BreakableBridge bridge))
                    {
                        if (!kickedThings.Contains(bridge.gameObject.GetInstanceID()))
                        {
                            // Breakable bridge. It's now breaking, all thanks to you.
                            Log.LogInfo($"Kicking Breakable bridge: {bridge}");

                            if (bridge.peopleOnBridgeDict.Keys.All((c) => IsRegistered(c.player)))
                            {
                                Log.LogInfo(
                                    $"Every people on the bridge are registered. Breaking {bridge}. Remember it's your fault {Character.localCharacter.characterName}");
                                bridge.photonView.RPC("ShakeBridge_Rpc", RpcTarget.All);
                            }
                            else
                            {
                                Log.LogWarning(
                                    $"Some Players on the bridge were not registered. Not Breaking {bridge}");
                            }


                            KickImpact(character, bridge.gameObject,
                                collider.ClosestPoint(character.Center + character.data.lookDirection), ref
                                kickedThings);
                        }
                    }
                }
                else if (collider.GetComponentInParent<BerryBush>() is var bush && bush != null)
                {
                    if (!kickedThings.Contains(bush.gameObject.GetInstanceID()))
                    {
                        // Check for fruits/items 

                        FallBerries(collider, bush, character);

                        KickImpact(character, bush.gameObject,
                            collider.ClosestPoint(character.Center + character.data.lookDirection), ref
                            kickedThings);
                    }
                }
            }
        }
    }

    private static void KickImpact(Character character, GameObject thing, Vector3 point, ref HashSet<int> kickedThings)
    {
        character.view.RPC("RPCA_KickImpact", RpcTarget.All, point);
        kickedThings.Add(thing.GetInstanceID());
    }
}