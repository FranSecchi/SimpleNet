using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MessagePack;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using SimpleNet.Utilities;
using UnityEngine;

namespace SimpleNet.Synchronization
{
    [AttributeUsage(AttributeTargets.Field)]
    public class Sync : Attribute { }

    public class ObjectState
    {
        private readonly object _syncLock = new object();
        //Instancia del componente - (Var info - Var value)
        private Dictionary<object, Dictionary<FieldInfo, object>> _trackedSyncVars;
        private Dictionary<int, object> _objectIds;
        private int _nextId;
        private Dictionary<object, Dictionary<string, FieldInfo>> _fieldCache;
        internal Dictionary<object, Dictionary<FieldInfo, object>> TrackedSyncVars => _trackedSyncVars;

        internal ObjectState()
        {
            _trackedSyncVars = new();
            _objectIds = new();
            _fieldCache = new();
            _nextId = 0;
        }

        public T GetFieldValue<T>(object obj, string fieldName)
        {
            if (obj == null) return default;

            if (_fieldCache.TryGetValue(obj, out var fields) && 
                fields.TryGetValue(fieldName, out var field))
            {
                lock (_syncLock)
                {
                    if (_trackedSyncVars.TryGetValue(obj, out var values) && 
                        values.TryGetValue(field, out var value))
                    {
                        return (T)value;
                    }
                }
            }
            return default;
        }

        internal void Register(int netId, object obj)
        {
            if (obj == null) return;

            Type type = obj.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            bool hasSyncFields = false;
            Dictionary<string, FieldInfo> fieldMap = new();

            foreach (FieldInfo field in fields)
            {
                if (Attribute.IsDefined(field, typeof(Sync)))
                {
                    hasSyncFields = true;
                    fieldMap[field.Name] = field;
                }
            }
            
            if (hasSyncFields)
            {
                lock (_syncLock)
                {
                    if (!_trackedSyncVars.ContainsKey(obj))
                    {
                        _trackedSyncVars[obj] = new Dictionary<FieldInfo, object>();
                        int id = _nextId++;
                        _objectIds[id] = obj;
                        _fieldCache[obj] = fieldMap;
                        DebugQueue.AddMessage($"Added ObjectState {((NetBehaviour)obj).gameObject.name} with ID {netId}, component {obj.GetType().Name} with ID {id}", DebugQueue.MessageType.State);
                    }
                    foreach (var field in fieldMap.Values)
                    {
                        _trackedSyncVars[obj][field] = field.GetValue(obj);
                    }
                }
            }
        }

        internal Dictionary<int, Dictionary<string, object>> Update()
        {
            Dictionary<int, Dictionary<string, object>> allChanges = new();
            Dictionary<object, Dictionary<FieldInfo, object>> updates = new();

            lock (_syncLock)
            {
                foreach (var obj in _objectIds)
                {
                    var fields = _trackedSyncVars[obj.Value];
                    Dictionary<string, object> changes = new Dictionary<string, object>();

                    foreach (var fieldEntry in fields)
                    {
                        FieldInfo field = fieldEntry.Key;
                        object oldValue = fieldEntry.Value;
                        object newValue = field.GetValue(obj.Value);

                        if (!Equals(oldValue, newValue))
                        {
                            changes[field.Name] = newValue;
                            if (!updates.ContainsKey(obj.Value))
                            {
                                updates[obj.Value] = new Dictionary<FieldInfo, object>();
                            }
                            updates[obj.Value][field] = newValue;
                        }
                    }

                    if (changes.Count > 0)
                    {
                        allChanges[obj.Key] = changes;
                    }
                }

                foreach (var updateEntry in updates)
                {
                    foreach (var fieldUpdate in updateEntry.Value)
                    {
                        _trackedSyncVars[updateEntry.Key][fieldUpdate.Key] = fieldUpdate.Value;
                    }
                }
            }
            
            return allChanges;
        }

        internal void SetChange(int id, Dictionary<string, object> changes, bool isRollback = false)
        {
            if (changes == null || changes.Count == 0) return;

            lock (_syncLock)
            {
                if (_objectIds.TryGetValue(id, out object obj) && 
                    _fieldCache.TryGetValue(obj, out var fields))
                {
                    foreach (var change in changes)
                    {
                        if (fields.TryGetValue(change.Key, out var field))
                        {
                            if (field.GetValue(obj) != change.Value)
                            {
                                field.SetValue(obj, change.Value);
                                if(isRollback)
                                    _trackedSyncVars[obj][field] = change.Value;
                            }
                        }
                    }
                }
                else
                {
                    DebugQueue.AddMessage($"No component {id} found", DebugQueue.MessageType.Warning);
                }
            }
        }
        
        internal void Reconcile(int netId, int id, Dictionary<string, object> changes, DateTime timeStamp)
        {
            if (changes == null || changes.Count == 0) return;

            if (_objectIds.TryGetValue(id, out object obj))
            {
                if (obj is NetBehaviour netBehaviour)
                {
                    NetManager.EnqueueMainThread(() => netBehaviour.OnReconciliation(id, changes, timeStamp));
                }
            }
            else
            {
                DebugQueue.AddMessage($"No object {netId} with component {id} found", DebugQueue.MessageType.Warning);
            }
        }

        public Dictionary<FieldInfo, object> GetFields(object obj)
        {
            if (obj == null) return null;

            lock (_syncLock)
            {
                return _trackedSyncVars.TryGetValue(obj, out var fields) ? fields : null;
            }
        }

        public ObjectState Clone()
        {
            ObjectState clone = new ObjectState();
            Dictionary<int, object> clonedIds = new();

            lock (_syncLock)
            {
                foreach (var obj in _objectIds)
                {
                    var fields = _trackedSyncVars[obj.Value];
                    Dictionary<FieldInfo, object> clonedFields = new Dictionary<FieldInfo, object>();
                    clonedIds[obj.Key] = obj.Value;
                    foreach (var fieldEntry in fields)
                    {
                        clonedFields[fieldEntry.Key] = fieldEntry.Value;
                    }
                    
                    clone._trackedSyncVars[obj.Value] = clonedFields;
                    clone._fieldCache[obj.Value] = _fieldCache[obj.Value];
                }
                clone._objectIds = clonedIds;
            }
            return clone;
        }

        internal void Unregister(object o)
        {
            if (o == null)
            {
                DebugQueue.AddMessage("Attempted to unregister a null object", DebugQueue.MessageType.Warning);
                return;
            }

            DebugQueue.AddMessage($"Unregister {o.GetType().Name} state", DebugQueue.MessageType.State);

            lock (_syncLock)
            {
                _trackedSyncVars.Remove(o);
                _fieldCache.Remove(o);

                int? keyToRemove = null;
                foreach (var pair in _objectIds)
                {
                    if (ReferenceEquals(pair.Value, o))
                    {
                        keyToRemove = pair.Key;
                        break;
                    }
                }

                if (keyToRemove.HasValue)
                {
                    _objectIds.Remove(keyToRemove.Value);
                }
            }
        }

        public bool HasComponent(int componentId)
        {
            return _objectIds.TryGetValue(componentId, out _);
        }

        public bool HasComponent(object obj)
        {
            return _objectIds.ContainsValue(obj);
        }
    }
}
