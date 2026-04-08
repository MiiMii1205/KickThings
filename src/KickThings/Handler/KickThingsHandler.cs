using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace KickThings.Handler;

public class KickThingsHandler : MonoBehaviourPunCallbacks
{
    private const int KickThingsViewID = 5898;

    private static KickThingsHandler _instance = null!;

    public PhotonView view = null!;
    
    public static KickThingsHandler Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("KickThingsHandler");
                _instance = go.AddComponent<KickThingsHandler>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            _instance = this;
            view = GetComponent<PhotonView>() ?? InitializeView();
        }
    }

    private PhotonView InitializeView()
    {
        var returnView = gameObject.AddComponent<PhotonView>();
        returnView.ViewID = KickThingsViewID;
        return returnView;
    }
    
    [PunRPC]
    public void RPC_KickMob(PhotonView mobView, Vector3 kickForce, Vector3 pos)
    {
        Plugin.Log.LogInfo($"Received kicking #{mobView}");
        var mob = mobView.GetComponent<Mob>();
        StartCoroutine(ApplyMobForce(mob, kickForce, pos));
    }

    private IEnumerator ApplyMobForce(Mob mob, Vector3 force, Vector3 pos)
    {
        var physicsSyncer = mob.GetComponent<PhysicsSyncer>();
        mob.mobState = Mob.MobState.Dead;
        
        yield return new WaitUntil(() => mob.mobState == Mob.MobState.Dead);

        mob.rig.AddForceAtPosition(force, pos, ForceMode.Impulse);
        
        mob.rig.rotation = Quaternion.LookRotation(mob.transform.forward, Vector3.down);
        mob.transform.rotation = Quaternion.LookRotation(mob.transform.forward, Vector3.down);
        
        physicsSyncer.ForceSyncForFrames(3);
        StartCoroutine(Plugin.ReanimateMob(mob));
    }

    [PunRPC]
    public void RPC_KickItem(PhotonView itemView, PhotonView charView, Vector3 kickForce, Vector3 pos)
    {
        Plugin.Log.LogInfo($"Received kicking #{itemView}");
        var it = itemView.GetComponent<Item>();
        
        it.rig.AddForceAtPosition(kickForce, pos, ForceMode.Impulse);
        it.physicsSyncer.ForceSyncForFrames(3);
        
        var chara = charView.GetComponent<Character>();

        it.lastHolderCharacter = chara;
        it.lastThrownCharacter = chara;
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
    
}