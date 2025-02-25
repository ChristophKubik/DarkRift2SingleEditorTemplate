﻿using Common.NetworkingData;
using DarkRift;
using DarkRift.Server;
using DarkRift.Server.Unity;
using System.Collections.Generic;
using UnityEngine;
using Utility.Debugging;

namespace Server
{

    [RequireComponent(typeof(XmlUnityServer))]
    public class ServerManager : MonoBehaviour
	{
        public static ServerManager Instance;

        public ServerInstance ServerInstance;

        public Dictionary<ushort, ClientConnection> Players = new Dictionary<ushort, ClientConnection>();
        public Dictionary<string, ClientConnection> PlayersByName = new Dictionary<string, ClientConnection>();

        private XmlUnityServer xmlServer;
        private DarkRiftServer server;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(this);

            if(CommandLineArgsHelper.DebugUtilityEnabled() || Application.isEditor)
            {
                GameObject go = Instantiate(Resources.Load("Prefabs\\Utility\\DebugUtility")) as GameObject;
                go.GetComponent<DebugUtility>().RegisterAsServer();
            }
        }

        void Start()
        {
            xmlServer = GetComponent<XmlUnityServer>();
            server = xmlServer.Server;
            server.ClientManager.ClientConnected += OnClientConnected;
            server.ClientManager.ClientDisconnected += OnClientDisconnected;
        }

        void OnDestroy()
        {
            server.ClientManager.ClientConnected -= OnClientConnected;
            server.ClientManager.ClientDisconnected -= OnClientDisconnected;
        }

        private void OnClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            IClient client = e.Client;
            ClientConnection p;

            if (Players.TryGetValue(client.ID, out p))
            {
                Debug.Log($"Client [{p.Name} | {client.ID}] disconnected.");
                p.OnClientDisconnect(sender, e);
            }

            e.Client.MessageReceived -= OnMessage;
        }

        private void OnClientConnected(object sender, ClientConnectedEventArgs e)
        {
            e.Client.MessageReceived += OnMessage;
        }

        private void OnMessage(object sender, MessageReceivedEventArgs e)
        {
            IClient client = (IClient)sender;
            using (Message message = e.GetMessage())
            {
                switch ((NetworkingTags)message.Tag)
                {
                    case NetworkingTags.LoginRequest:
                        OnclientLogin(client, message.Deserialize<LoginRequestData>());
                        break;
                }
            }
        }

        private void OnclientLogin(IClient client, LoginRequestData data)
        {
            // Check if player is already logged in (name already chosen in our case) and if not create a new object to represent a logged in client.
            if (PlayersByName.ContainsKey(data.Name) || PlayersByName.Count >= ServerInstance.MaxSlots)
            {
                Debug.LogWarning($"Client login for player \"{data.Name}\" denied! Player count: {PlayersByName.Count}/{ServerInstance.MaxSlots}");

                using (Message message = Message.CreateEmpty((ushort)NetworkingTags.LoginRequestDenied))
                {
                    client.SendMessage(message, SendMode.Reliable);
                }

                return;
            }

            // In the future the ClientConnection will handle its messages
            client.MessageReceived -= OnMessage;

            Debug.Log($"Client [{data.Name} | {client.ID}] logged in.");

            new ClientConnection(client, data, ServerInstance);
        }
    }
	
}
