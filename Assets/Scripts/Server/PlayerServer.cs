﻿using Common;
using Common.NetworkingData;
using Common.Player;
using DarkRift;
using DarkRift.Server;
using System.Collections.Generic;
using UnityEngine;

namespace Server
{

    [RequireComponent(typeof(PlayerLogic))]
	public class PlayerServer : MonoBehaviour
	{
		public PlayerLogic PlayerLogic { get; private set; }
		public uint InputTick { get; private set; }
		public IClient Client { get; private set; }
		public PlayerStateData CurrentPlayerStateData => currentPlayerStateData;

		public List<PlayerStateData> PlayerStateDataHistory { get; private set; }

		private ClientConnection clientConnection;

		private ServerInstance serverInstance;

		private PlayerStateData currentPlayerStateData;

		private Buffer<PlayerInputData> inputBuffer = new Buffer<PlayerInputData>(1, 2);

		private PlayerInputData[] inputs;

		private Transform eyes;

		void Awake()
		{
			PlayerLogic = GetComponent<PlayerLogic>();
			eyes = gameObject.transform.Find("Eyes");

			PlayerStateDataHistory = new List<PlayerStateData>();
		}

		public void Initialize(Vector3 position, ClientConnection clientConnection)
		{
			this.clientConnection = clientConnection;
			serverInstance = clientConnection.ServerInstance;
			Client = clientConnection.Client;
			this.clientConnection.Player = this;

			currentPlayerStateData = new PlayerStateData(Client.ID, 0, position, 0f, 0f);
			InputTick = serverInstance.ServerTick;

            PlayerSpawnData[] playerSpawnData = serverInstance.GetSpawnDataForAllPlayers();
			using (Message m = Message.Create((ushort)NetworkingTags.GameStartDataResponse, new GameStartData(playerSpawnData, serverInstance.ServerTick)))
			{
				Client.SendMessage(m, SendMode.Reliable);
			}
		}

		public void RecieveInput(PlayerInputData input)
		{
			inputBuffer.Add(input);
		}

		public void SpawnPlayerAtPosition(Vector3 position)
        {
			transform.localPosition = position;
			currentPlayerStateData = new PlayerStateData(Client.ID, 0, position, 0f, 0f);
		}

		public void PlayerPreUpdate()
		{
			inputs = inputBuffer.Get();
			for (int i = 0; i < inputs.Length; i++)
			{
				if (inputs[i].Keyinputs[6])
				{
					serverInstance.PerformShootRayCast(inputs[i].Time, this);
					return;
				}
			}
		}

		public PlayerStateData PlayerUpdate()
		{
			if (inputs.Length > 0)
			{
				PlayerInputData input = inputs[0];
				InputTick++;

				for (int i = 1; i < inputs.Length; i++)
				{
					InputTick++;
					for (int j = 0; j < input.Keyinputs.Length; j++)
					{
						input.Keyinputs[j] = input.Keyinputs[j] || inputs[i].Keyinputs[j];
					}

					input.Yaw = inputs[i].Yaw;
					input.Pitch = inputs[i].Pitch;
				}

				currentPlayerStateData = PlayerLogic.GetNextFrameData(input, currentPlayerStateData);
			}

			transform.localPosition = currentPlayerStateData.Position;
			transform.localRotation = Quaternion.Euler(0.0f, currentPlayerStateData.Yaw, 0.0f);
			eyes.rotation = Quaternion.Euler(currentPlayerStateData.Pitch, currentPlayerStateData.Yaw, 0);

			PlayerStateDataHistory.Add(currentPlayerStateData);
			if (PlayerStateDataHistory.Count > 10)
			{
				PlayerStateDataHistory.RemoveAt(0);
			}

			return currentPlayerStateData;
		}

		public PlayerSpawnData GetPlayerSpawnData()
		{
			return new PlayerSpawnData(Client.ID, clientConnection.Name, transform.localPosition);
		}
	}
	
}
