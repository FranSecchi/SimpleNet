using System.Collections.Generic;
using SimpleNet.Messages;
using SimpleNet.Synchronization;
using SimpleNet.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimpleNet.Network
{
    internal static class NetScene
    { 
        private static Dictionary<string, GameObject> m_prefabs = new Dictionary<string, GameObject>();
        private static Dictionary<int, NetObject> netObjects = new Dictionary<int, NetObject>();
        private static Dictionary<string, GameObject> sceneObjects = new Dictionary<string, GameObject>();
        private static int netObjectId = 0;
        private static int connectedPlayers = 0;
        private static Dictionary<int, List<int>> validatedObjects = new Dictionary<int, List<int>>();
        private static string sceneName = "";
        internal static void Init()
        {
            netObjectId = 0;
            connectedPlayers = NetManager.PlayerCount - 1;
            Messager.RegisterHandler<OwnershipMessage>(OnOwnership);
            Messager.RegisterHandler<DestroyMessage>(OnDestroyMessage);
            Messager.RegisterHandler<SceneLoadMessage>(OnSceneLoadMessage);
            Messager.RegisterHandler<SpawnMessage>(OnSpawnMessage);
            Messager.RegisterHandler<SpawnValidationMessage>(OnValidateSpawn);
            sceneName = SceneManager.GetActiveScene().name;
        }

        private static void CheckValidateSpawn(int obj, int player)
        {
            if (!validatedObjects.ContainsKey(obj))
                validatedObjects[obj] = new List<int>();
            validatedObjects[obj].Add(player);
            DebugQueue.AddMessage($"NetID {obj} | Validated: {validatedObjects[obj].Count}/{connectedPlayers}", DebugQueue.MessageType.Warning);
            if(validatedObjects[obj].Count >= connectedPlayers)
            {
                NetMessage m = new SpawnValidationMessage(obj, -1);
                NetHost.Send(m);
                validatedObjects.Remove(obj);
                ValidateSpawn(obj);
            }
        }
        private static void OnValidateSpawn(SpawnValidationMessage obj)
        {
            if (NetManager.IsHost)
            {
                CheckValidateSpawn(obj.netObjectId, obj.requesterId);
            }
            else
            {
                ValidateSpawn(obj.netObjectId);
            }
        }

        internal static void LoadScene(string name)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(name);
        }
        internal static void SendScene(int id)
        {
            NetMessage msg = new SceneLoadMessage(sceneName, -1, true);
            NetManager.Send(msg);
        }
        private static void OnSceneLoadMessage(SceneLoadMessage msg)
        {
            if (NetManager.IsHost)
            {
                if (msg.isLoaded)
                {
                    SendObjects(msg.requesterId);
                }
            }
            else if(sceneName != msg.sceneName)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                NetManager.EnqueueMainThread(() => SceneManager.LoadScene(msg.sceneName));
            }
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            sceneName = scene.name;
            NetManager.Init();
            connectedPlayers = NetManager.PlayerCount - 1;
            SceneLoadMessage msg = new SceneLoadMessage(scene.name, NetManager.ConnectionId(), true);
            NetManager.Send(msg);
        }


        internal static void RegisterPrefabs(List<GameObject> prefabs)
        {
            DebugQueue.AddMessage($"Registering {prefabs.Count} prefabs in NetScene instance");

            foreach (var prefab in prefabs)
            {
                m_prefabs[prefab.name] = prefab;
            }
        }

        private static void OnSpawnMessage(SpawnMessage msg)
        {
            if(NetManager.IsHost) Spawn(msg);
            else
            {
                if (msg.requesterId == NetManager.ConnectionId())
                {
                    Spawn(msg);
                    Reconciliate(msg);
                }
                else Spawn(msg);
            }
        }
        private static void OnOwnership(OwnershipMessage msg)
        {
            var netObj = GetNetObject(msg.netObjectId);
            if (netObj != null)
            {
                if(NetManager.IsHost) netObj.GiveOwner(msg.newOwnerId);
                else netObj.OwnerId = msg.newOwnerId;
            }
        }
        private static void OnDestroyMessage(DestroyMessage msg)
        {
            if (NetManager.IsHost)
            {
                // Validate ownership before destroying
                var netObj = GetNetObject(msg.netObjectId);
                if (netObj != null && (netObj.OwnerId == msg.requesterId || NetManager.IsHost))
                {
                    Destroy(msg.netObjectId);
                    NetHost.Send(msg);
                }
            }
            else
            {
                Destroy(msg.netObjectId);
            }
        }

        internal static void RegisterSceneObject(NetBehaviour netBehaviour)
        {
            string sceneId = netBehaviour.GetComponent<SceneObjectId>().SceneId;
            if(sceneId != null && !sceneObjects.ContainsKey(sceneId))
            {
                sceneObjects[sceneId] = netBehaviour.gameObject;
                DebugQueue.AddMessage($"Registering {netBehaviour.gameObject.name} as {sceneId}",
                    DebugQueue.MessageType.Warning);
            }
            if (!NetManager.IsHost) return;
            
            int id = netObjectId++;
            NetObject netObj = new NetObject(id, netBehaviour);
            netObj.SceneId = sceneId;
            Register(netObj);
        }
        internal static void Spawn(SpawnMessage msg)
        {
            if (msg.sceneId != "")
            {
                if (!NetManager.IsHost) NetManager.EnqueueMainThread(() => { SpawnSceneObject(msg);}); 
            }
            else
            {
                NetManager.EnqueueMainThread(() => { SpawnImmediate(msg);});
            }
        }

        private static void SpawnSceneObject(SpawnMessage msg)
        {
            if (sceneObjects.TryGetValue(msg.sceneId, out GameObject obj))
            {
                DebugQueue.AddMessage($"Spawned SceneObject with ID {msg.sceneId}, owned by {msg.owner}", DebugQueue.MessageType.Network);
                NetBehaviour netBehaviour = obj.GetComponent<NetBehaviour>();
                NetObject netObj = new NetObject(msg.netObjectId, netBehaviour, msg.owner);
                Register(netObj);
                netObj.SceneId = msg.sceneId;
                obj.transform.position = msg.position;
                obj.transform.rotation = msg.rotation;
                msg.requesterId = NetManager.ConnectionId();
                NetMessage m = new SpawnValidationMessage(msg.netObjectId, NetManager.ConnectionId());
                NetManager.Send(m);
            }
            else DebugQueue.AddMessage($"A spawn request of a not found scene object has been received. Scene Id: {msg.sceneId} Requested by {msg.requesterId}", DebugQueue.MessageType.Error);
        }

        private static void ValidateSpawn(int netObjectId)
        {
            DebugQueue.AddMessage("Validated spawn: "+netObjectId, DebugQueue.MessageType.Network);
            NetManager.EnqueueMainThread(() => GetNetObject(netObjectId)?.Enable());
        }

        private static void SpawnImmediate(SpawnMessage msg)
        {
            if (NetManager.IsHost && msg.netObjectId >= 0) return;
            if(m_prefabs.TryGetValue(msg.prefabName, out GameObject obj))
            {
                GameObject instance = GameObject.Instantiate(obj, msg.position, msg.rotation);
                NetObject netObj = instance.GetComponent<NetBehaviour>().NetObject;
                if (netObj == null)
                {
                    netObj = new NetObject(msg.netObjectId, instance.GetComponent<NetBehaviour>(), msg.owner);
                }
                else
                {
                    netObj.OwnerId = msg.owner;
                    msg.netObjectId = msg.netObjectId >= 0 ? msg.netObjectId : netObj.NetId;
                }
                
                netObj.SceneId = "";
                Register(netObj);
                ValidateSpawn(msg.netObjectId);
                DebugQueue.AddMessage($"Spawned NetObject with ID {msg.netObjectId}, owned by {netObj.OwnerId}",
                    DebugQueue.MessageType.Network);

                msg.target = null;
                if(NetManager.IsHost)
                    NetHost.Send(msg);
            }
            else
            {
                DebugQueue.AddMessage($"Spawning null prefab: {msg.prefabName}", DebugQueue.MessageType.Warning);
            };
        }

        internal static void Destroy(int objectId)
        {
            if (netObjects.TryGetValue(objectId, out NetObject obj))
            {
                NetManager.EnqueueMainThread(() => { obj.Destroy(); });
                Unregister(objectId);
            }
        }

        internal static NetObject GetNetObject(int netId)
        {
            return netObjects.TryGetValue(netId, out NetObject obj) ? obj : null;
        }

        internal static void Reconciliate(SpawnMessage spawnMessage)
        {
            //Compare
            
        }

        private static void Register(NetObject obj)
        {
            if (netObjects.ContainsKey(obj.NetId)) return;
            netObjects[obj.NetId] = obj;     
            ObjectState state = new ObjectState();
            StateManager.Register(obj.NetId, state);
            obj.State = state;

        }

        private static void Unregister(int objectId)
        {
            netObjects.Remove(objectId);
            StateManager.Unregister(objectId);
        }

        internal static void SendObjects(int id)
        {
            foreach(var sceneObjects in sceneObjects)
            {
                GameObject obj = sceneObjects.Value.gameObject;
                NetManager.EnqueueMainThread(() => {
                    NetObject netObj = obj.GetComponent<NetBehaviour>().NetObject;
                    if (netObj == null) return;
                
                    SpawnMessage msg = new SpawnMessage(
                        NetManager.ConnectionId(),
                        obj.name,
                        obj.transform.position,
                        obj.transform.rotation,
                        owner: netObj.OwnerId,
                        sceneId: sceneObjects.Key,
                        target: new List<int>{id});
                    msg.netObjectId = netObj.NetId;
                
                    NetHost.Send(msg); 
                });
                
            }
        }

        internal static void CleanUp()
        {
            m_prefabs.Clear();
            netObjects.Clear();
            sceneObjects.Clear();
        }

        internal static List<NetObject> GetAllNetObjects()
        {
            return new List<NetObject>(netObjects.Values);
        }

        internal static void DisconnectClient(int id)
        {
            foreach (var netObject in netObjects)
            {
                NetObject netObj = netObject.Value;
                if (id == netObj.OwnerId)
                {
                    netObj.GiveOwner(-1);
                    netObj.Disconnect();
                }
            }
        }
    }
}
