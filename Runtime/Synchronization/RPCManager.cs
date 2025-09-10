using System;
using System.Collections.Generic;
using System.Reflection;
using SimpleNet.Network;
using SimpleNet.Serializer;
using SimpleNet.Messages;
using SimpleNet.Utilities;

namespace SimpleNet.Synchronization
{
    public static class RPCManager
    {
        private static Dictionary<int, List<object>> _rpcTargets = new();
        private static Dictionary<int, Dictionary<string, List<MethodInfo>>> _rpcMethods = new();

        internal static void Init()
        {
            Messager.RegisterHandler<RPCMessage>(CallRPC);
        }
        internal static void Register(int netId, object target)
        {
            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            DebugQueue.AddMessage($"Registering RPCs for {target.GetType().Name} with netId {netId}. Found {methods.Length} methods.", DebugQueue.MessageType.RPC);

            foreach (var method in methods)
            {
                var rpcAttr = method.GetCustomAttribute<NetRPC>();
                if (rpcAttr != null)
                {
                    if (!_rpcMethods.ContainsKey(netId))
                        _rpcMethods.Add(netId, new Dictionary<string, List<MethodInfo>>());
                    if (!_rpcMethods[netId].ContainsKey(method.Name))
                    {
                        _rpcMethods[netId][method.Name] = new List<MethodInfo>();
                    }
                    DebugQueue.AddMessage($"RPC registered {netId} | {method.Name} | Direction: {rpcAttr.Direction}", DebugQueue.MessageType.RPC);

                    _rpcMethods[netId][method.Name].Add(method);
                }
            }

            if (methods.Length <= 0) return;

            if (!_rpcTargets.ContainsKey(netId))
                _rpcTargets[netId] = new List<object>();
            if (!_rpcMethods.ContainsKey(netId))
                _rpcMethods[netId] = new Dictionary<string, List<MethodInfo>>();

            if (_rpcTargets[netId].Contains(target))
                return;

            _rpcTargets[netId].Add(target);
        }

        internal static void Unregister(int netId, object target)
        {
            if (!_rpcTargets.ContainsKey(netId))
                return;

            _rpcTargets[netId].Remove(target);

            foreach (var methodList in _rpcMethods[netId].Values)
            {
                methodList.RemoveAll(m => m.DeclaringType == target.GetType());
            }

            var emptyMethods = new List<string>();
            foreach (var kvp in _rpcMethods[netId])
            {
                if (kvp.Value.Count == 0)
                {
                    emptyMethods.Add(kvp.Key);
                }
            }
            foreach (var methodName in emptyMethods)
            {
                _rpcMethods[netId].Remove(methodName);
            }
            
            if (_rpcTargets[netId].Count == 0)
            {
                _rpcTargets.Remove(netId);
                _rpcMethods.Remove(netId);
            }
        }

        private static void CallRPC(RPCMessage message)
        {
            if(NetManager.IsHost)
            {
                if(message.target == null || message.target.Contains(-1)) 
                    CallRPC(message.ObjectId, message.MethodName, message.Parameters);
                
                DebugQueue.AddMessage($"{message.SenderID} sent RPC {message.MethodName} | {message.ObjectId}", DebugQueue.MessageType.RPC);
                NetManager.Send(message);
            }
            else CallRPC(message.ObjectId, message.MethodName, message.Parameters);
        }

        internal static void CallRPC(int netId, string methodName, object[] parameters)
        {
            if (!_rpcTargets.ContainsKey(netId))
            {
                DebugQueue.AddMessage($"No RPC targets found for netId {netId}", DebugQueue.MessageType.Error);
                return;
            }

            if (!_rpcMethods[netId].ContainsKey(methodName))
            {
                DebugQueue.AddMessage($"No RPC method {methodName} found for netId {netId}", DebugQueue.MessageType.Error);
                return;
            }
            
            foreach (var method in _rpcMethods[netId][methodName])
            {
                try
                {
                    var target = _rpcTargets[netId].Find(t => t.GetType() == method.DeclaringType);
                    if (target != null)
                    {
                        var paramTypes = method.GetParameters();
                        var convertedParams = new object[parameters.Length];
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            var p = parameters[i];
                            if (p == null)
                            {
                                convertedParams[i] = null;
                            }
                            else if (paramTypes[i].ParameterType.IsInstanceOfType(p))
                            {
                                convertedParams[i] = p;
                            }
                            else
                            {
                                var bytes = NetSerializer.Serializer.Serialize(p);
                                convertedParams[i] = NetSerializer.Serializer.Deserialize(bytes, paramTypes[i].ParameterType);
                            }
                        }
                        DebugQueue.AddRPC(methodName, netId, NetManager.ConnectionId());
                        NetManager.EnqueueMainThread(()=>method.Invoke(target, convertedParams));
                    }
                }
                catch (Exception e)
                {   
                    DebugQueue.AddMessage($"Error invoking RPC {methodName}: {e}", DebugQueue.MessageType.Error);
                }
            }
        }

        internal static void SendRPC(int netId, string methodName, params object[] parameters)
        {
            DebugQueue.AddMessage($"Attempting to send RPC {methodName} for netId {netId}", DebugQueue.MessageType.RPC);
            
            if (!_rpcTargets.ContainsKey(netId))
            {
                DebugQueue.AddMessage($"No RPC targets found for netId {netId}", DebugQueue.MessageType.Error);
                return;
            }
            if (!_rpcMethods[netId].ContainsKey(methodName))
            {
                DebugQueue.AddMessage($"No RPC method {methodName} found for netId {netId}. Available methods: {string.Join(", ", _rpcMethods[netId].Keys)}", DebugQueue.MessageType.Error);
                return;
            }
            
            List<int> targetIds = null;
            foreach (var method in _rpcMethods[netId][methodName])
            {
                var rpcAttr = method.GetCustomAttribute<NetRPC>();
                if (rpcAttr != null)
                {
                    if (rpcAttr.Direction == Direction.ServerToClient && !NetManager.IsHost)
                    {
                        DebugQueue.AddMessage($"Cannot send RPC {methodName} - it is server-to-client only", DebugQueue.MessageType.Error);
                        return;
                    }
                    if (rpcAttr.Direction == Direction.ClientToServer && NetManager.IsHost)
                    {
                        DebugQueue.AddMessage($"Cannot send RPC {methodName} - it is client-to-server only", DebugQueue.MessageType.Error);
                        return;
                    }
                    
                    switch(rpcAttr.TargetMode)
                    {
                        case Send.Specific:
                            if (parameters[^1].GetType() != typeof(List<int>))
                            {
                                DebugQueue.AddMessage($"Cannot send RPC {methodName} - the last parameter should be the target clients as a List<int> and is {parameters[^1].GetType()}", DebugQueue.MessageType.Error);
                                return;
                            }
                            targetIds = (List<int>)parameters[^1];
                            if(NetManager.IsHost && targetIds.Contains(-1)) CallRPC(netId, methodName, parameters);
                            break;
                        case Send.Others:
                            targetIds = new List<int>(NetManager.AllPlayers);
                            targetIds.Remove(NetManager.ConnectionId());
                            break;
                        case Send.All:
                            if(NetManager.IsHost) CallRPC(netId, methodName, parameters);
                            break;
                    }
                }
            }
            var message = new RPCMessage(NetManager.ConnectionId(), netId, methodName, targetIds, parameters);
            DebugQueue.AddMessage($"{message.SenderID} sent RPC {methodName} | Obj: {netId}", DebugQueue.MessageType.RPC);

            NetManager.Send(message);
        }
    }
} 