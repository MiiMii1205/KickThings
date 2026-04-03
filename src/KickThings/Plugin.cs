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
        Instance = this;
        var harmony = new Harmony(Id);

        harmony.PatchAll(typeof(KickPatcher));
        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    public void ManageKick(CharacterGrabbing characterGrabbing)
    {
        this.StartCoroutine(DoKickThings(
            characterGrabbing.kickDelay, characterGrabbing.kickForce, characterGrabbing.character));
    }

    private IEnumerator DoKickThings(float kickDelay, float kickForce, Character character)
    {
        yield return new WaitForSeconds(kickDelay);

        var g = character.refs.grabbing;

        var lookDir = character.data.lookDirection_Flat;

        Collider[] results = new Collider[10];

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

                        mob.mobState = Mob.MobState.RigidbodyControlled;
                        mob.rig.AddForceAtPosition(character.data.lookDirection * kickForce, point, ForceMode.Impulse);
                        character.view.RPC("RPCA_KickImpact", RpcTarget.All, point);
                        kickedThings.Add(mob.gameObject.GetInstanceID());
                    }
                    
                }
                else if (rig.TryGetComponent(out Item it) && it.itemState == ItemState.Ground)
                {
                    if (!kickedThings.Contains(it.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking item: {it}");
                        
                        var point = character.Center + character.data.lookDirection *
                            Vector3.Distance(character.Center, it.Center());

                        if (rig.isKinematic && kickableKinematicItemList.Contains(it.UIData.itemName))
                        {
                            it.SetKinematicNetworked(true);
                        }

                        it.rig.AddForceAtPosition(character.data.lookDirection * kickForce, point, ForceMode.Impulse);
                        character.view.RPC("RPCA_KickImpact", RpcTarget.All, point);
                        kickedThings.Add(it.gameObject.GetInstanceID());
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

                        rig.AddForceAtPosition(character.data.lookDirection * kickForce, point, ForceMode.Impulse);
                        character.view.RPC("RPCA_KickImpact", RpcTarget.All, point);
                        
                        // Make every climbing character fall.
                        foreach (var chara in rope.charactersClimbing)
                        {
                            if (KickThingsHandler.IsRegistered(chara.player.photonView.Owner.ActorNumber))
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

                        kickedThings.Add(rope.gameObject.GetInstanceID());
                        
                    }
                    
                }
            }
            else
            {
                // Simple colliders interactions
                
                Log.LogInfo($"Kicking {collider.gameObject.name}");

                if (collider.GetComponentInParent<Luggage>() is var luggage && luggage != null)
                {
                    if (!kickedThings.Contains(luggage.gameObject.GetInstanceID()))
                    {
                        Log.LogInfo($"Kicking Luggage: {luggage}");
                        // Open a luggage immediately
                        luggage.Interact_CastFinished(character);
                        
                        character.view.RPC("RPCA_KickImpact", RpcTarget.All, collider.ClosestPoint(character.Center + character.data.lookDirection));
                        
                        kickedThings.Add(luggage.gameObject.GetInstanceID());

                    }
                    
                }
                else if (collider.GetComponentInParent<JungleVine>() is var jungleVine && jungleVine != null)
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
                                    if (KickThingsHandler.IsRegistered(chara.player.photonView.Owner.ActorNumber))
                                    {
                                        Log.LogWarning(
                                            $"Dropping {chara.gameObject.name} off of {jungleVine}.");
                                        chara.refs.vineClimbing.view.RPC("StopVineClimbingRpc", RpcTarget.All);
                                        chara.view.RPC("RPCA_Fall", RpcTarget.All, 1);
                                    }
                                    else
                                    {
                                        Log.LogWarning($"{chara.gameObject.name} is not registered. Not dropping them off of {jungleVine}.");
                                    }
                                }
                            }

                            character.view.RPC("RPCA_KickImpact", RpcTarget.All,
                                collider.ClosestPoint(character.Center + character.data.lookDirection));

                            kickedThings.Add(jungleVine.gameObject.GetInstanceID());

                        }
                        
                    }
                    else if (jungleVine.gameObject.TryGetComponent(out BreakableBridge bridge))
                    {
                        if (!kickedThings.Contains(bridge.gameObject.GetInstanceID()))
                        {
                            // Breakable bridge. It's now breaking, all thanks to you.
                            Log.LogInfo($"Kicking Breakable bridge: {bridge}");
                            
                            if (bridge.peopleOnBridgeDict.Keys.All((c)=> KickThingsHandler.IsRegistered(c.player.photonView.Owner.ActorNumber)))
                            {
                                Log.LogInfo($"Every people on the bridge are registered. Breaking {bridge}. Remember it's your fault {Character.localCharacter.characterName}");
                                bridge.photonView.RPC("ShakeBridge_Rpc", RpcTarget.All);
                            }
                            else
                            {
                                Log.LogWarning(
                                    $"Some Players on the bridge were not registered. Not Breaking {bridge}");
                            }


                            character.view.RPC("RPCA_KickImpact", RpcTarget.All,
                                collider.ClosestPoint(character.Center + character.data.lookDirection));
                            
                            kickedThings.Add(bridge.gameObject.GetInstanceID());

                        }
                    }
                }
            }
        }
    }

}