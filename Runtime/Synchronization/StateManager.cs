using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using MessagePack;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using SimpleNet.Utilities;
using UnityEngine;

namespace SimpleNet.Synchronization
{
    internal class StateManager
    {
        private static readonly ConcurrentDictionary<int, ObjectState> snapshot = new();
        private static readonly Dictionary<int, HashSet<object>> _componentCache = new();
        private static readonly ConcurrentDictionary<int, List<(SyncMessage, DateTime)>> _pendingSyncs = new();
        private static TimeSpan _pendingSyncTimeout = TimeSpan.FromSeconds(3);
        internal static void Register(int netId, ObjectState state)
        {
            if (state == null) return;
            snapshot[netId] = state;
        }

        internal static void Register(int netId, object obj)
        {
            if (obj == null) return;
            if (snapshot.TryGetValue(netId, out ObjectState state))
            {
                state.Register(netId, obj);
                
                if (!_componentCache.TryGetValue(netId, out var components))
                {
                    components = new HashSet<object>();
                    _componentCache[netId] = components;
                }
                components.Add(obj);            
                CheckPendingSyncs(netId);
            }
            else
            {
                DebugQueue.AddMessage("Not Registered state :" + obj.GetType().Name, DebugQueue.MessageType.State);
            }
        }

        internal static void Unregister(int netId, object obj)
        {
            if (obj == null) return;

            if (snapshot.TryGetValue(netId, out ObjectState state))
            {
                state.Unregister(obj);
                
                if (_componentCache.TryGetValue(netId, out var components))
                {
                    components.Remove(obj);
                    if (components.Count == 0)
                    {
                        _componentCache.Remove(netId);
                    }
                }
            }
        }

        internal static void Unregister(int netId)
        {
            snapshot.TryRemove(netId, out _);
            _componentCache.Remove(netId);
        }

        internal static void Clear()
        {
            snapshot.Clear();
            _componentCache.Clear();
            _pendingSyncs.Clear();
        }

        internal static ObjectState GetState(int objectId)
        {
            return snapshot.TryGetValue(objectId, out ObjectState state) ? state : null;
        }

        internal static Dictionary<int, ObjectState> GetAllStates()
        {
            return new Dictionary<int, ObjectState>(snapshot);
        }
        
        internal static void RestoreState(int objectId, ObjectState state)
        {
            if (state == null) return;
            
            if (snapshot.TryGetValue(objectId, out ObjectState currentState))
            {
                foreach (var kvp in state.TrackedSyncVars)
                {
                    var component = kvp.Key;
                    if (component == null) continue;

                    foreach (var fieldKvp in kvp.Value)
                    {
                        var field = fieldKvp.Key;
                        var value = fieldKvp.Value;
                        
                        try
                        {
                            field.SetValue(component, value);
                        }
                        catch (System.Exception e)
                        {
                            DebugQueue.AddMessage($"Failed to restore state for {component.GetType().Name}: {e.Message}", DebugQueue.MessageType.Error);
                        }
                    }
                }
                snapshot[objectId] = state.Clone();
            }
            else
            {
                snapshot[objectId] = state.Clone();
            }
        }

        internal static void SendUpdateStates()
        {
            foreach (var netObject in snapshot)
            {
                if (netObject.Value == null) continue;
                
                var changes = netObject.Value.Update();
                if (changes.Count > 0)
                {
                    Send(netObject.Key, changes);
                }
            }
        }

        private static void Send(int netObjectKey, Dictionary<int, Dictionary<string, object>> changes)
        {
            int id = NetManager.ConnectionId();
            var netObject = NetScene.GetNetObject(netObjectKey);
            
            if (netObject == null)
            {
                DebugQueue.AddMessage($"Failed to find NetObject {netObjectKey} for state update", DebugQueue.MessageType.Error);
                return;
            }

            foreach (var change in changes)
            {
                if (netObject.OwnerId == id)
                {
                    SyncMessage msg = new SyncMessage(id, netObjectKey, change.Key, change.Value);
                    NetManager.Send(msg);
                }
            }
        }

        internal static void SetSync(SyncMessage syncMessage)
        {
            if (syncMessage == null) return;
            CleanupPendingSyncs();

            if (snapshot.TryGetValue(syncMessage.ObjectID, out ObjectState state) && state.HasComponent(syncMessage.ComponentId))
            {
                state.SetChange(syncMessage.ComponentId, syncMessage.changedValues);
                
            }
            else 
            {
                if (!_pendingSyncs.ContainsKey(syncMessage.ObjectID))
                    _pendingSyncs[syncMessage.ObjectID] = new List<(SyncMessage, DateTime)>();
                _pendingSyncs[syncMessage.ObjectID].Add((syncMessage, DateTime.UtcNow));

                DebugQueue.AddMessage(
                    $"Add pending Sync: {syncMessage.ObjectID}",
                    DebugQueue.MessageType.State
                );
            }
        }
        private static void CleanupPendingSyncs()
        {
            DateTime now = DateTime.UtcNow;
            var expired = new List<(int, int)>(); // (objectId, index)

            foreach (var kvp in _pendingSyncs)
            {
                for (int i = kvp.Value.Count - 1; i >= 0; i--)
                {
                    if (now - kvp.Value[i].Item2 > _pendingSyncTimeout)
                    {
                        expired.Add((kvp.Key, i));
                    }
                }
            }

            
            foreach (var (objectId, idx) in expired)
            {
                var msg = _pendingSyncs[objectId][idx].Item1;
                DebugQueue.AddMessage(
                    $"Dropped expired pending SetSync for object {objectId} after {_pendingSyncTimeout.TotalSeconds}s",
                    DebugQueue.MessageType.State
                );
                _pendingSyncs[objectId].RemoveAt(idx);
                if (_pendingSyncs[objectId].Count == 0)
                    _pendingSyncs.TryRemove(objectId, out _);
            }
        }
        private static void CheckPendingSyncs(int objectId)
        {
            if (_pendingSyncs.TryGetValue(objectId, out var syncs))
            {
                foreach (var (sync, _) in syncs)
                {
                    DebugQueue.AddMessage($"Set pending sync obj: {objectId}", DebugQueue.MessageType.State);
                    if(snapshot.TryGetValue(objectId, out var state) && state.HasComponent(sync.ComponentId)) 
                    {
                        SetSync(sync);
                        _pendingSyncs.TryRemove(objectId, out _);
                    }
                }
            }
        }
    }
}
