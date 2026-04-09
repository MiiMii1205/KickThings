using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace KickThings.Handler;

public class KickThingsHandler : MonoBehaviourPunCallbacks
{
    public PhotonView view = null!;
    
    public static KickThingsHandler Instance { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;
        view = GetComponent<PhotonView>();
    }
    
    [PunRPC]
    public void RPC_KickMob(PhotonView mobView, PhotonView charView, Vector3 kickForce, Vector3 pos)
    {
        Plugin.Log.LogInfo($"Received kicking #{mobView}");
        var mob = mobView.GetComponent<Mob>();
        
        if(mob is Beetle)
        {
            StartCoroutine(ApplyMobForce(mob, charView, kickForce, pos));
        }
    }
    [PunRPC]
    public void RPC_SyncKickedRig(PhotonView kickedView, Vector3 linVel, Vector3 rotVel)
    {
        Plugin.Log.LogInfo($"Received kicked rigidbody #{kickedView}");
        
        var rig = kickedView.GetComponent<Rigidbody>();
        
        rig.angularVelocity = rotVel;
        rig.linearVelocity = linVel;
    }

    private IEnumerator ApplyMobForce(Mob mob, PhotonView charView, Vector3 force, Vector3 pos)
    {
        var stateToUse = Mob.MobState.Dead;
        
        mob.mobState = stateToUse;
        yield return new WaitUntil(() => mob.mobState == stateToUse);
        
        // if (mob is not Beetle)
        // {
        //     StartCoroutine(SetMobStateForSecs(mob, Mob.MobState.RigidbodyControlled, 2f));
        // }
        
        mob.rig.AddForceAtPosition(force, pos, ForceMode.Impulse);
        
        if(charView.Owner.IsLocal)
        {
            if (mob._mobItem != null)
            {
                mob._mobItem.syncer.ForceSyncForFrames(3);
            }
            else if (mob.TryGetComponent(out PhysicsSyncer physicsSyncer))
            {
                physicsSyncer.ForceSyncForFrames(3);
            }
        }
        
        StartCoroutine(Plugin.ReanimateMob(mob));
    }

    private static IEnumerator SetMobStateForSecs(Mob mob, Mob.MobState state, float duration)
    {
        var t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            mob.mobState = state;
            mob.rig.constraints = RigidbodyConstraints.None;
            yield return null;
        }
    }

    [PunRPC]
    public void RPC_KickItem(PhotonView itemView, PhotonView charView, Vector3 kickForce, Vector3 pos)
    {
        Plugin.Log.LogInfo($"Received kicking #{itemView}");
        var it = itemView.GetComponent<Item>();
        
        if (it.TryGetComponent(out Mob mb))
        {
            mb.mobState = Mob.MobState.RigidbodyControlled;
        }
        
        it.rig.AddForceAtPosition(kickForce, pos, ForceMode.Impulse);
        
        if(charView.Owner.IsLocal)
        {
            it.physicsSyncer.ForceSyncForFrames(3);
        }

        RPC_Registered(nameof(RPC_UpdateItemData), itemView, charView);
    }
    [PunRPC]
    public void RPC_UpdateItemData(PhotonView itemView, PhotonView charView)
    {
        Plugin.Log.LogInfo($"Received kick data #{itemView}. Was kicked by {charView.Owner}");
        
        var it = itemView.GetComponent<Item>();
        var chara = charView.GetComponent<Character>();
        
        it.lastHolderCharacter = chara;
        it.lastThrownCharacter = chara;
    }

    public void RPC_Registered(string methodName, params object[] parameters)
    {
        var playerList = PhotonNetwork.PlayerList;

        for (int i = 0, length = playerList.Length; i < length; ++i)
        {
            if (Plugin.IsRegistered(playerList[i]))
            {
                view.RPC(methodName, playerList[i], parameters);
            }
        }
    }
    public void RPC_OtherRegistered(string methodName, params object[] parameters)
    {
        var playerList = PhotonNetwork.PlayerListOthers;

        for (int i = 0, length = playerList.Length; i < length; ++i)
        {
            if (Plugin.IsRegistered(playerList[i]))
            {
                view.RPC(methodName, playerList[i], parameters);
            }
        }
    }
}