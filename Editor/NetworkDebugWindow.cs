using UnityEngine;
using SimpleNet.Network;
using UnityEditor;
using SimpleNet.Transport;
using SimpleNet.Synchronization;
using SimpleNet.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace SimpleNet.Editor
{
    public class NetworkDebugWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private Vector2 messageScrollPosition;
        private bool autoRefresh = true;
        private float refreshInterval = 1f;
        private double lastRefreshTime;
        private bool showDetailedInfo = false;
        private bool showNetObjects = false;
        private bool showMessages = false;
        private bool showServerInfo = false;
        private bool showClientInfo = false;
        private Dictionary<int, bool> clientFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> netObjectFoldouts = new Dictionary<int, bool>();
        private bool[] messageTypeFilters;
        private string messageSearchText = "";

        private static ServerInfo lastKnownServerInfo;
        private static List<ConnectionInfo> lastKnownClients;
        private static List<NetObject> lastKnownNetObjects;
        private static bool lastKnownIsHost;
        private static ConnectionState? lastKnownConnectionState;

        [MenuItem("Window/SimpleNet/Network Debug")]
        public static void ShowWindow()
        {
            GetWindow<NetworkDebugWindow>("Network Debug");
        }

        private void OnEnable()
        {
            messageTypeFilters = new bool[System.Enum.GetValues(typeof(DebugQueue.MessageType)).Length];
            for (int i = 0; i < messageTypeFilters.Length; i++)
            {
                messageTypeFilters[i] = true;
                DebugQueue.SetMessageTypeEnabled((DebugQueue.MessageType)i, true);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !this) return;

            if (autoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime > refreshInterval)
            {
                if (this)
                {
                    Repaint();
                    lastRefreshTime = EditorApplication.timeSinceStartup;
                }
            }

            if (Application.isPlaying && this)
            {
                UpdateLastKnownState();
            }
        }

        private void UpdateLastKnownState()
        {
            try
            {
                var newServerInfo = NetManager.GetServerInfo();
                if (newServerInfo != null)
                {
                    lastKnownServerInfo = newServerInfo;
                }
            }
            catch (System.Exception) { }

            try
            {
                lastKnownIsHost = NetManager.IsHost;
            }
            catch (System.Exception) { }

            try
            {
                var newState = NetManager.GetConnectionState();
                if (newState.HasValue)
                {
                    lastKnownConnectionState = newState;
                }
            }
            catch (System.Exception) { }

            try
            {
                if (NetManager.IsHost)
                {
                    var newClients = NetManager.GetClients();
                    if (newClients != null)
                    {
                        lastKnownClients = newClients;
                    }
                }
            }
            catch (System.Exception) { }

            try
            {
                var newNetObjects = NetManager.GetAllNetObjects();
                if (newNetObjects != null && newNetObjects.Count > 0)
                {
                    lastKnownNetObjects = new List<NetObject>();
                    foreach (var netObj in newNetObjects)
                    {
                        if (netObj != null)
                        {
                            lastKnownNetObjects.Add(netObj);
                        }
                    }
                }
            }
            catch (System.Exception) { }
        }

        private void DrawMessageLog()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Message Log", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(50));
            for (int i = 0; i < messageTypeFilters.Length; i++)
            {
                var type = (DebugQueue.MessageType)i;
                EditorGUILayout.BeginHorizontal(GUILayout.Width(100));
                bool newValue = EditorGUILayout.Toggle(messageTypeFilters[i], GUILayout.Width(20));
                if (newValue != messageTypeFilters[i])
                {
                    messageTypeFilters[i] = newValue;
                    DebugQueue.SetMessageTypeEnabled(type, newValue);
                }
                EditorGUILayout.LabelField(type.ToString(), GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndHorizontal();

            messageSearchText = EditorGUILayout.TextField("Search", messageSearchText);

            if (GUILayout.Button("Clear Messages"))
            {
                DebugQueue.ClearMessages();
            }

            messageScrollPosition = EditorGUILayout.BeginScrollView(messageScrollPosition, GUILayout.Height(200));

            var messages = DebugQueue.GetMessages();
            var filteredMessages = messages.Where(m => 
                messageTypeFilters[(int)m.Type] && 
                (string.IsNullOrEmpty(messageSearchText) || m.Message.ToLower().Contains(messageSearchText.ToLower()))
            ).ToList();

            foreach (var message in filteredMessages)
            {
                Color originalColor = GUI.color;
                switch (message.Type)
                {
                    case DebugQueue.MessageType.Error:
                        GUI.color = Color.red;
                        break;
                    case DebugQueue.MessageType.Warning:
                        GUI.color = Color.yellow;
                        break;
                    case DebugQueue.MessageType.Network:
                        GUI.color = Color.cyan;
                        break;
                    case DebugQueue.MessageType.RPC:
                        GUI.color = Color.green;
                        break;
                    case DebugQueue.MessageType.State:
                        GUI.color = Color.magenta;
                        break;
                    case DebugQueue.MessageType.Rollback:
                        GUI.color = new Color(1f, 0.5f, 0f); // Orange color for rollback messages
                        break;
                }

                EditorGUILayout.LabelField($"[{message.Timestamp:F2}s] {message.Message}");
                GUI.color = originalColor;
            }

            EditorGUILayout.EndScrollView();
            EditorGUI.indentLevel--;
        }

        private void DrawConnectionStatus()
        {
            EditorGUILayout.LabelField("Connection Status", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            try
            {
                bool isHost = Application.isPlaying ? NetManager.IsHost : lastKnownIsHost;
                ConnectionState? state = Application.isPlaying ? NetManager.GetConnectionState() : lastKnownConnectionState;
                
                EditorGUILayout.LabelField("Is Host", isHost.ToString());
                EditorGUILayout.LabelField("Running", state?.ToString() ?? "Not Connected");
            }
            catch (System.Exception)
            {
                EditorGUILayout.LabelField("Status", "Unable to retrieve connection status");
            }
            EditorGUI.indentLevel--;
        }

        private void DrawServerInformation()
        {
            showServerInfo = EditorGUILayout.Foldout(showServerInfo, "Server Information", true);
            if (showServerInfo)
            {
                EditorGUI.indentLevel++;
                try
                {
                    var serverInfo = Application.isPlaying ? NetManager.GetServerInfo() : lastKnownServerInfo;
                    if (serverInfo != null)
                    {
                        EditorGUILayout.LabelField("Server Name", serverInfo.ServerName);
                        EditorGUILayout.LabelField("Address", serverInfo.Address);
                        EditorGUILayout.LabelField("Port", serverInfo.Port.ToString());
                        EditorGUILayout.LabelField("Current Players", serverInfo.CurrentPlayers.ToString());
                        EditorGUILayout.LabelField("Max Players", serverInfo.MaxPlayers.ToString());
                        EditorGUILayout.LabelField("Game Mode", serverInfo.GameMode);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No server information available");
                    }
                }
                catch (System.Exception)
                {
                    EditorGUILayout.LabelField("Unable to retrieve server information");
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawClientInformation()
        {
            showClientInfo = EditorGUILayout.Foldout(showClientInfo, "Client Information", true);
            if (showClientInfo)
            {
                EditorGUI.indentLevel++;
                try
                {
                    bool isHost = Application.isPlaying ? NetManager.IsHost : lastKnownIsHost;
                    if (isHost)
                    {
                        var clients = Application.isPlaying ? NetManager.GetClients() : lastKnownClients;
                        if (clients != null && clients.Count > 0)
                        {
                            foreach (var client in clients)
                            {
                                EditorGUILayout.LabelField($"Client {client.Id}", $"State: {client.State}");
                            }

                            EditorGUILayout.Space();
                            showDetailedInfo = EditorGUILayout.Foldout(showDetailedInfo, "Detailed Connection Information", true);
                            if (showDetailedInfo)
                            {
                                EditorGUI.indentLevel++;
                                foreach (var client in clients)
                                {
                                    if (!clientFoldouts.ContainsKey(client.Id))
                                    {
                                        clientFoldouts[client.Id] = false;
                                    }

                                    clientFoldouts[client.Id] = EditorGUILayout.Foldout(clientFoldouts[client.Id], $"Client {client.Id} Details", true);
                                    if (clientFoldouts[client.Id])
                                    {
                                        EditorGUI.indentLevel++;
                                        ConnectionInfo connectionInfo = Application.isPlaying ? NetManager.GetConnectionInfo(client.Id) : client;
                                        if (connectionInfo != null)
                                        {
                                            EditorGUILayout.LabelField("Connection ID", connectionInfo.Id.ToString());
                                            EditorGUILayout.LabelField("State", connectionInfo.State.ToString());
                                            EditorGUILayout.LabelField("Bytes Received", connectionInfo.BytesReceived.ToString() ?? "Unknown");
                                            EditorGUILayout.LabelField("Bytes Sent", connectionInfo.BytesSent.ToString());
                                            EditorGUILayout.LabelField("Last Ping", $"{connectionInfo.Ping}ms");
                                            EditorGUILayout.LabelField("Packet Loss", connectionInfo.PacketLoss.ToString());
                                            EditorGUILayout.LabelField("Connected Since", connectionInfo.ConnectedSince.ToString("HH:mm:ss"));
                                        }
                                        EditorGUI.indentLevel--;
                                    }
                                }
                                EditorGUI.indentLevel--;
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField("No clients connected");
                        }
                    }
                    else
                    {
                        var connectionInfo = Application.isPlaying ? NetManager.GetConnectionInfo() : (lastKnownClients?.FirstOrDefault());
                        if (connectionInfo != null)
                        {
                            EditorGUILayout.LabelField("Connection ID", connectionInfo.Id.ToString());
                            EditorGUILayout.LabelField("State", connectionInfo.State.ToString());
                            EditorGUILayout.LabelField("Bytes Received", connectionInfo.BytesReceived.ToString() ?? "Unknown");
                            EditorGUILayout.LabelField("Bytes Sent", connectionInfo.BytesSent.ToString());
                            EditorGUILayout.LabelField("Last Ping", $"{connectionInfo.Ping}ms");
                            EditorGUILayout.LabelField("Packet Loss", connectionInfo.PacketLoss.ToString());
                            EditorGUILayout.LabelField("Connected Since", connectionInfo.ConnectedSince.ToString("HH:mm:ss"));
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Not connected");
                        }
                    }
                }
                catch (System.Exception)
                {
                    EditorGUILayout.LabelField("Unable to retrieve client information");
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawNetworkObjects()
        {
            EditorGUILayout.LabelField("Network Objects", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            try
            {
                showNetObjects = EditorGUILayout.Foldout(showNetObjects, "Network Objects List", true);
                if (showNetObjects)
                {
                    var netObjects = Application.isPlaying ? NetManager.GetAllNetObjects() : lastKnownNetObjects;
                    if (netObjects != null && netObjects.Count > 0)
                    {
                        foreach (var netObj in netObjects)
                        {
                            if (!netObjectFoldouts.ContainsKey(netObj.NetId))
                            {
                                netObjectFoldouts[netObj.NetId] = false;
                            }

                            netObjectFoldouts[netObj.NetId] = EditorGUILayout.Foldout(netObjectFoldouts[netObj.NetId], 
                                $"NetObject {netObj.NetId} - {netObj.SceneId}", true);
                            
                            if (netObjectFoldouts[netObj.NetId])
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.LabelField("Network ID: " + netObj.NetId.ToString());
                                EditorGUILayout.LabelField("Scene ID: " + (string.IsNullOrEmpty(netObj.SceneId) ? "(null)" : netObj.SceneId.ToString()));
                                EditorGUILayout.LabelField("Owner: " + netObj.OwnerId.ToString());
                                EditorGUILayout.LabelField("Is Scene Object: "+ !string.IsNullOrEmpty(netObj.SceneId));
                                EditorGUI.indentLevel--;
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No network objects found");
                    }
                }
            }
            catch (System.Exception)
            {
                EditorGUILayout.LabelField("Unable to retrieve network objects");
            }
            EditorGUI.indentLevel--;
        }

        private void DrawRegisteredPrefabs()
        {
            EditorGUILayout.LabelField("Registered Prefabs", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            try
            {
                var prefabsList = Application.isPlaying ? NetManager.PrefabsList : null;
                if (prefabsList != null && prefabsList.prefabs != null && prefabsList.prefabs.Count > 0)
                {
                    foreach (var prefab in prefabsList.prefabs)
                    {
                        if (prefab != null)
                        {
                            EditorGUILayout.LabelField(prefab.name);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No prefabs registered");
                }
            }
            catch (System.Exception)
            {
                EditorGUILayout.LabelField("Unable to retrieve registered prefabs");
            }
            EditorGUI.indentLevel--;
        }

        private void OnGUI()
        {
            if (!this) return;

            EditorGUILayout.BeginVertical();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Showing last known state from previous play session.", MessageType.Info);
            }

            if (Application.isPlaying)
            {
                autoRefresh = EditorGUILayout.Toggle("Auto Refresh", autoRefresh);
                if (autoRefresh)
                {
                    refreshInterval = EditorGUILayout.Slider("Refresh Interval", refreshInterval, 0.1f, 5f);
                }
                else if (GUILayout.Button("Refresh"))
                {
                    Repaint();
                }
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawConnectionStatus();
            EditorGUILayout.Space();
            DrawServerInformation();
            EditorGUILayout.Space();
            DrawClientInformation();
            EditorGUILayout.Space();
            DrawRegisteredPrefabs();
            EditorGUILayout.Space();
            DrawNetworkObjects();

            // Add Message Log section
            showMessages = EditorGUILayout.Foldout(showMessages, "Message Log", true);
            if (showMessages)
            {
                DrawMessageLog();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }
} 