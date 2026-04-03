using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace KickThings.Handler;

public class KickThingsHandler: MonoBehaviourPun
{
    private const int KickThingsViewID = 5898;
    
    private static KickThingsHandler _instance;

    public PhotonView view;

    private HashSet<int> registeredPlayers = new HashSet<int>();

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
    public void RPC_RegisterPlayer(int playerId)
    {
        if (registeredPlayers.Add(playerId))
        {
            Plugin.Log.LogInfo($"Registered player #{playerId}");
        }

        if (PhotonNetwork.IsMasterClient && playerId != PhotonNetwork.LocalPlayer.ActorNumber)
        {
            view.RPC("RPC_RegisterPlayer", RpcTarget.Others, PhotonNetwork.LocalPlayer.ActorNumber);
        }
    }

    public static bool IsRegistered(int playerId)
    {
        return Instance.registeredPlayers.Contains(playerId);
    }

    public static void InitializeRegistry(int playerId)
    {
        Instance.registeredPlayers.Clear();
        Instance.registeredPlayers.Add(playerId);

        if (PhotonNetwork.InRoom)
        {
            Instance.view.RPC("RPC_RegisterPlayer", RpcTarget.Others, playerId);
        }
        
    }
}