﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Mirror;

/// <summary>
/// This is the Viewer object for a joined player.
/// Once they join they will have local ownership of this object until a job is determined
/// and then they are spawned as player entity
/// </summary>
public class JoinedViewer : NetworkBehaviour
{
	public override void OnStartLocalPlayer()
	{
		base.OnStartLocalPlayer();
		PlayerManager.SetViewerForControl(this);

		if (!PlayerPrefs.HasKey(PlayerPrefKeys.ClientID))
		{
			PlayerPrefs.SetString(PlayerPrefKeys.ClientID, "");
			PlayerPrefs.Save();
		}

		Logger.LogFormat("JoinedViewer on this client calling CmdServerSetupPlayer, our clientID: {0} username: {1}", Category.Connections,
			PlayerPrefs.GetString(PlayerPrefKeys.ClientID), PlayerManager.CurrentCharacterSettings.username);

		CmdServerSetupPlayer(PlayerPrefs.GetString(PlayerPrefKeys.ClientID),
			PlayerManager.CurrentCharacterSettings.username, DatabaseAPI.ServerData.UserID, GameData.BuildNumber);
	}

	[Command]
	private void CmdServerSetupPlayer(string clientID, string username, string userid, int clientVersion)
	{
		Logger.LogFormat("A joinedviewer called CmdServerSetupPlayer on this server, clientID: {0} username: {1}", Category.Connections,
			clientID, username);

		//Register player to player list (logging code exists in PlayerList so no need for extra logging here)
		var connPlayer = PlayerList.Instance.AddOrUpdate(new ConnectedPlayer
		{
			Connection = connectionToClient,
			GameObject = gameObject,
			Username = username,
			Job = JobType.NULL,
			ClientId = clientID
		});

		if (!PlayerList.Instance.ValidatePlayer(clientID, username, userid, clientVersion, connPlayer))
		{
			return;
		}

		// Check if they have a player to rejoin. If not, assign them a new client ID
		var loggedOffPlayer = PlayerList.Instance.TakeLoggedOffPlayer(clientID);
		if (loggedOffPlayer == null)
		{
			//This is the players first time connecting to this round, assign them a Client ID;
			var oldID = clientID;
			clientID = System.Guid.NewGuid().ToString();
			Logger.LogFormat("This server did not find a logged off player with clientID {0}, assigning" +
			                 " joined viewer a new ID {1}", Category.Connections, oldID, clientID);

		}

		// Sync all player data and the connected player count
		CustomNetworkManager.Instance.SyncPlayerData(gameObject);
		UpdateConnectedPlayersMessage.Send();



		// Only sync the pre-round countdown if it's already started
		if (GameManager.Instance.CurrentRoundState == RoundState.PreRound)
		{
			if (GameManager.Instance.waitForStart)
			{
				TargetSyncCountdown(connectionToClient, GameManager.Instance.waitForStart, GameManager.Instance.startTime);
			}
			else
			{
				GameManager.Instance.CheckPlayerCount();
			}
		}

		//if there's a logged off player, we will force them to rejoin. Previous logic allowed client to reenter
		//their body or not, which should not be up to the client!
		if (loggedOffPlayer != null)
		{
			PlayerSpawn.ServerRejoinPlayer(this, loggedOffPlayer);
		}
		else
		{
			TargetLocalPlayerSetupNewPlayer(connectionToClient, connPlayer.ClientId, GameManager.Instance.CurrentRoundState);
		}
	}

	/// <summary>
	/// Target which tells this joined viewer they are a new player, tells them what their ID is,
	/// and tells them what round state the game is on
	/// </summary>
	/// <param name="target">this connection</param>
	/// <param name="serverClientID">client ID server</param>
	/// <param name="roundState"></param>
	[TargetRpc]
	private void TargetLocalPlayerSetupNewPlayer(NetworkConnection target,
		string serverClientID, RoundState roundState)
	{
		Logger.LogFormat("JoinedViewer on this client updating our client id to what server tells us, from {0} to {1}", Category.Connections,
			PlayerPrefs.GetString(PlayerPrefKeys.ClientID), serverClientID);
		//save our ID so we can rejoin
		PlayerPrefs.SetString(PlayerPrefKeys.ClientID, serverClientID);
		PlayerPrefs.Save();

		//clear our UI because we're about to change it based on the round state
		UIManager.ResetAllUI();

		// Determine what to do depending on the state of the round
		switch (roundState)
		{
			case RoundState.PreRound:
				// Round hasn't yet started, give players the pre-game screen
				UIManager.Display.SetScreenForPreRound();
				break;
			// case RoundState.Started:
			// TODO spawn a ghost if round has already ended?
			default:
				// occupation select
				UIManager.Display.SetScreenForJobSelect();

				break;
		}
	}

	/// <summary>
	/// At the moment players can choose their jobs on round start:
	/// </summary>
	[Command]
	public void CmdRequestJob(JobType jobType, CharacterSettings characterSettings)
	{
		PlayerSpawn.ServerSpawnPlayer(this, GameManager.Instance.GetRandomFreeOccupation(jobType),
			characterSettings);
	}

	/// <summary>
	/// Tells the client to start the countdown if it's already started
	/// </summary>
	[TargetRpc]
	private void TargetSyncCountdown(NetworkConnection target, bool started, float countdownTime)
	{
		Logger.Log("Syncing countdown!", Category.Round);
		UIManager.Instance.displayControl.preRoundWindow.GetComponent<GUI_PreRoundWindow>().SyncCountdown(started, countdownTime);
	}
}