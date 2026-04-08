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

        Manager.RegisterPlayerProperty<bool>(nameof(_kickThingsRegistered), PlayerEventType.All,
            (player, b) => { _kickThingsRegistered = b; });

        Manager.RegisterOnJoinedRoom(SetupRegistered);

        harmony.PatchAll(typeof(KickPatcher));
        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    private void SetupRegistered(Photon.Realtime.Player player)
    {
        if (PhotonNetwork.InRoom && Manager != null)
        {
            Log.LogInfo($"Registering player {player} as a KickThings user.");
            Manager.SetPlayerProperty(nameof(_kickThingsRegistered), true);
        }
    }

    private static bool _kickThingsRegistered = false;

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
                    possibleItems[j].attachedRigidbody is { } jrig &&
                    jrig.gameObject.TryGetComponent(out Item it) &&
                    !activatedItemSet.Contains(it.gameObject.GetInstanceID()) &&
                    jrig.isKinematic && it.itemState == ItemState.Ground)
                {
                    Log.LogInfo($"Making {it} fall...");

                    it.SetKinematicNetworked(false);

                    KickThingsHandler.Instance.view.RPC(nameof(KickThingsHandler.RPC_UpdateItemData), RpcTarget.All,
                        it.view, character.view);

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
            Log.LogInfo($"Kicking {bush.GetType().Name}: {bush}");

            var activatedItemSet = new HashSet<int>();
            MakeBerriesFall(character, bush.possibleBerries.y, bush.spawnSpots, ref activatedItemSet);

            // check for beehives

            if (bush.gameObject.name.StartsWith("Jungle_Willow"))
            {
                Log.LogInfo($"Checking for {nameof(Beehive)} on {bush}...");

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
                        Log.LogInfo($"Kicking {hive.GetType().Name}: {hive}");

                        beehiveItem.SetKinematicNetworked(false);

                        KickThingsHandler.Instance.view.RPC(nameof(KickThingsHandler.RPC_UpdateItemData), RpcTarget.All,
                            beehiveItem.view, character.view);

                        hive.currentBees.photonView.RPC(nameof(BeeSwarm.SetBeesAngryRPC), RpcTarget.AllBuffered, true);
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
            Log.LogInfo($"Kicking {bv.GetType().Name}: {bv}");
            var activatedItemSet = new HashSet<int>();
            MakeBerriesFall(character, bv.possibleBerries.y, bv.spawnSpots, ref activatedItemSet);
        }
    }

    public void ManageKick(CharacterGrabbing characterGrabbing)
    {
        StartCoroutine(DoKickThings(
            characterGrabbing.kickDelay, characterGrabbing.kickForce, characterGrabbing.character));
    }

    private static bool IsRegistered(Player player)
    {
        return PhotonNetwork.TryGetPlayer(player.GetActorNumber(), out var p) && IsRegistered(p);
    }

    private static bool IsRegistered(Photon.Realtime.Player p)
    {
        return (p.IsLocal && _kickThingsRegistered ||
                (!p.IsLocal &&
                 p.CustomProperties.ContainsKey(
                     nameof(_kickThingsRegistered))));
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
                        if (CanInteractPhysics(mob.photonView))
                        {
                            Log.LogInfo($"Kicking mob: {mob}");

                            var point = character.Center + character.data.lookDirection *
                                Vector3.Distance(character.Center, mob.Center());

                            Photon.Realtime.Player? switchOwnershipBackTo = null;

                            var shouldSwitchBackToOwner = false;

                            if (!mob.photonView.IsMine)
                            {
                                shouldSwitchBackToOwner = true;
                                switchOwnershipBackTo = mob.photonView.Owner;
                                // Requesting ownership for physics syncs 
                                mob.photonView.RequestOwnership();

                                yield return new WaitUntil(() => mob.photonView.IsMine);
                            }

                            if (mob.rig.isKinematic && mob._mobItem != null)
                            {
                                mob._mobItem.SetKinematicNetworked(false);
                                mob._mobItem.lastHolderCharacter = character;
                                mob._mobItem.lastThrownCharacter = character;
                            }

                            mob.mobState = Mob.MobState.Dead;

                            yield return new WaitUntil(() => mob.mobState == Mob.MobState.Dead);

                            mob.rig.AddForceAtPosition(character.data.lookDirection * (kickForce * 100f / 5f), point,
                                ForceMode.Impulse);

                            mob.GetComponent<PhysicsSyncer>().ForceSyncForFrames(3);

                            StartCoroutine(ReanimateMob(mob));

                            if (shouldSwitchBackToOwner)
                            {
                                StartCoroutine(RetransferOwnershipDelay(mob.photonView, switchOwnershipBackTo!, 5));
                            }

                            KickImpact(character, mob.gameObject,
                                point, ref
                                kickedThings);
                        }
                        else if (IsRegistered(mob.photonView.Owner))
                        {
                            Log.LogInfo($"Kicking mob trough RPC: {mob}");

                            var point = character.Center + character.data.lookDirection *
                                Vector3.Distance(character.Center, mob.Center());

                            KickThingsHandler.Instance.view.RPC(nameof(KickThingsHandler.RPC_KickMob), RpcTarget.All,
                                mob.photonView,
                                character.data.lookDirection * (kickForce * 100f / 5f), point);

                            KickImpact(character, mob.gameObject,
                                point, ref
                                kickedThings);
                        }
                        else
                        {
                            Log.LogWarning(
                                $"Mob owner ({mob.photonView.Owner}) doesn't have {Name} installed and can't transfer ownership (ownership is {mob.photonView.OwnershipTransfer}). Not kicking {mob}.");
                        }
                    }
                    else
                    {
                        Log.LogInfo($"Already kicked {mob}. Skipping");
                    }
                }
                else if (rig.TryGetComponent(out Spider spid))
                {
                    if (!kickedThings.Contains(spid.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking spider: {spid}");

                        spid.Bonk();

                        KickImpact(character, spid.gameObject,
                            collider.ClosestPoint(character.Center + character.data.lookDirection), ref
                            kickedThings);
                    }
                    else
                    {
                        Log.LogInfo($"Already kicked {spid}. Skipping");
                    }
                }
                else if (rig.TryGetComponent(out Item it) && it.itemState == ItemState.Ground)
                {
                    if (!kickedThings.Contains(it.gameObject.GetInstanceID()))
                    {
                        if (CanInteractPhysics(it.view))
                        {
                            Log.LogInfo($"Kicking item: {it}");

                            var point = character.Center + character.data.lookDirection *
                                Vector3.Distance(character.Center, it.Center());

                            Photon.Realtime.Player? switchOwnershipBackTo = null;

                            var shouldSwitchBackToOwner = false;

                            if (!it.photonView.IsMine)
                            {
                                shouldSwitchBackToOwner = true;
                                switchOwnershipBackTo = it.view.Owner;
                                // Requesting ownership for physics syncs 
                                it.photonView.RequestOwnership();

                                yield return new WaitUntil(() => it.view.IsMine);
                            }

                            if (rig.isKinematic && kickableKinematicItemList.Contains(it.UIData.itemName))
                            {
                                it.SetKinematicNetworked(false);
                            }

                            KickThingsHandler.Instance.view.RPC(nameof(KickThingsHandler.RPC_UpdateItemData),
                                RpcTarget.All, it.view, character.photonView);

                            it.rig.AddForceAtPosition(character.data.lookDirection * kickForce, point,
                                ForceMode.Impulse);

                            it.physicsSyncer.ForceSyncForFrames(3);

                            if (shouldSwitchBackToOwner)
                            {
                                StartCoroutine(RetransferOwnershipDelay(it.view, switchOwnershipBackTo!, 5));
                            }

                            KickImpact(character, it.gameObject, point, ref
                                kickedThings);
                        }
                        else if (IsRegistered(it.view.Owner))
                        {
                            Log.LogInfo($"Kicking item trough RPC: {it}");

                            var point = character.Center + character.data.lookDirection *
                                Vector3.Distance(character.Center, it.Center());

                            KickThingsHandler.Instance.view.RPC(nameof(KickThingsHandler.RPC_KickItem), RpcTarget.All,
                                it.photonView,
                                character.photonView,
                                character.data.lookDirection * kickForce, point);

                            KickImpact(character, mob.gameObject,
                                point, ref
                                kickedThings);
                        }
                        else
                        {
                            Log.LogWarning(
                                $"Item owner ({it.view.Owner}) doesn't have {Name} installed and can't transfer ownership (ownership is {it.view.OwnershipTransfer}). Not kicking {it}.");
                        }
                    }
                    else
                    {
                        Log.LogInfo($"Already kicked {it}. Skipping");
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

                        var segPoint = seg.Center();

                        // Make every climbing character fall.
                        foreach (var c in rope.charactersClimbing)
                        {
                            if (rope.antigrav ? c.Center.y > segPoint.y : c.Center.y < segPoint.y)
                            {
                                Log.LogWarning(
                                    $"{c.gameObject.name} is passed the affected segment. Skipping...");
                            }

                            if (IsRegistered(c.player))
                            {
                                Log.LogWarning(
                                    $"Dropping {c.gameObject.name} off of {rope}.");

                                c.view.RPC(
                                    nameof(CharacterRopeHandling.StopRopeClimbingRpc), RpcTarget.All);

                                c.view.RPC(nameof(Character.RPCA_Fall), RpcTarget.All, 1f);
                            }
                            else
                            {
                                Log.LogWarning(
                                    $"{c.gameObject.name} doesn't have {Name} installed. Not dropping {c.gameObject.name} off {rope}.");
                            }
                        }

                        KickImpact(character, rope.gameObject, point, ref
                            kickedThings);
                    }
                    else
                    {
                        Log.LogInfo($"Already kicked {rope}. Skipping");
                    }
                }
            }
            else
            {
                // Simple colliders interactions

                if (collider.GetComponentInParent<Spider>() is { } spid)
                {
                    if (!kickedThings.Contains(spid.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking {spid.GetType().Name}: {spid}");

                        spid.Bonk();

                        KickImpact(character, spid.gameObject,
                            collider.ClosestPoint(character.Center + character.data.lookDirection), ref
                            kickedThings);
                    }
                    else
                    {
                        Log.LogInfo($"Already kicked {spid}. Skipping");
                    }
                }

                if (collider.GetComponentInParent<Luggage>() is { } luggage && luggage != null)
                {
                    if (!kickedThings.Contains(luggage.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking {luggage.GetType().Name}: {luggage}");
                        // Open a luggage immediately
                        luggage.Interact_CastFinished(character);

                        KickImpact(character, luggage.gameObject,
                            collider.ClosestPoint(character.Center + character.data.lookDirection), ref
                            kickedThings);
                    }
                    else
                    {
                        Log.LogInfo($"Already kicked {luggage}. Skipping");
                    }
                }
                else if (collider.TryGetComponent<RopeSegment>(out var seg) && seg.rope is { } rope)
                {
                    if (!kickedThings.Contains(rope.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking {rope.GetType().Name} (no rigidbody): {rope}");

                        var point = character.Center + character.data.lookDirection *
                            Vector3.Distance(character.Center, seg.Center());

                        var segPoint = seg.Center();

                        // Make every climbing character fall.
                        foreach (var chara in rope.charactersClimbing)
                        {
                            if (rope.antigrav ? chara.Center.y > segPoint.y : chara.Center.y < segPoint.y)
                            {
                                Log.LogWarning(
                                    $"{chara.gameObject.name} is passed the affected segment. Skipping...");
                            }

                            if (IsRegistered(chara.player))
                            {
                                Log.LogWarning(
                                    $"Dropping {chara.gameObject.name} off of {rope}.");

                                chara.view.RPC(
                                    nameof(CharacterRopeHandling.StopRopeClimbingRpc), RpcTarget.All);
                                chara.view.RPC(nameof(Character.RPCA_Fall), RpcTarget.All, 1f);
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
                    else
                    {
                        Log.LogInfo($"Already kicked {rope}. Skipping");
                    }
                }
                else if (collider.GetComponentInParent<JungleVine>() is { } jungleVine && jungleVine != null)
                {
                    if (jungleVine.displayName.Length > 0)
                    {
                        if (!kickedThings.Contains(jungleVine.gameObject.GetInstanceID()))
                        {
                            // Is a climbable vine.
                            Log.LogInfo($"Kicking {jungleVine.GetType().Name}: {jungleVine}");

                            // Make every climbing character fall.
                            foreach (var chara in Character.AllCharacters)
                            {
                                if (chara.data.heldVine == jungleVine && chara.data.isVineClimbing)
                                {
                                    if (IsRegistered(chara.player))
                                    {
                                        Log.LogWarning(
                                            $"Dropping {chara.gameObject.name} off of {jungleVine}.");
                                        chara.refs.vineClimbing.view.RPC(
                                            nameof(CharacterVineClimbing.StopVineClimbingRpc), RpcTarget.All);
                                        chara.view.RPC(nameof(Character.RPCA_Fall), RpcTarget.All, 1f);
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
                        else
                        {
                            Log.LogInfo($"Already kicked {jungleVine}. Skipping");
                        }
                    }
                    else if (jungleVine.gameObject.TryGetComponent(out BreakableBridge bridge))
                    {
                        if (!kickedThings.Contains(bridge.gameObject.GetInstanceID()))
                        {
                            // Breakable bridge. It's now breaking, all thanks to you.
                            Log.LogInfo($"Kicking {bridge.GetType().Name}: {bridge}");

                            if (bridge.peopleOnBridgeDict.Keys.All((c) => IsRegistered(c.player)))
                            {
                                Log.LogInfo(
                                    $"Every people on the bridge are registered. Breaking {bridge}. Remember it's your fault {Character.localCharacter.characterName}");
                                bridge.photonView.RPC(nameof(BreakableBridge.ShakeBridge_Rpc), RpcTarget.All);
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
                        else
                        {
                            Log.LogInfo($"Already kicked {bridge}. Skipping");
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
                    else
                    {
                        Log.LogInfo($"Already kicked {bush}. Skipping");
                    }
                }
            }
        }
    }

    private static IEnumerator RetransferOwnershipDelay(PhotonView componentPhotonView,
        Photon.Realtime.Player previousOwner, int f)
    {
        var frames = 0;
        while (frames < f)
        {
            frames++;
            yield return null;
        }

        if (componentPhotonView != null)
        {
            componentPhotonView.TransferOwnership(previousOwner);
        }
    }

    public static IEnumerator ReanimateMob(Mob component)
    {
        yield return new WaitForSeconds(6);
        component.mobState = Mob.MobState.RigidbodyControlled;
    }

    private static bool CanInteractPhysics(PhotonView componentView)
    {
        return componentView.IsMine || componentView.OwnershipTransfer != OwnershipOption.Fixed;
    }

    private static void KickImpact(Character character, GameObject thing, Vector3 point, ref HashSet<int> kickedThings)
    {
        character.view.RPC(nameof(CharacterGrabbing.RPCA_KickImpact), RpcTarget.All, point);
        kickedThings.Add(thing.GetInstanceID());
    }
}