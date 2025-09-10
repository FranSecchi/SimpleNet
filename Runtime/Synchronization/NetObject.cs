using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using SimpleNet.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimpleNet.Synchronization
{
    /// <summary>
    /// Represents a networked object that can be synchronized across the network.
    /// Manages ownership, behaviours, and lifecycle of networked game objects.
    /// </summary>
    public class NetObject
    {
        /// <summary>
        /// The unique network identifier for this object across all connected clients.
        /// </summary>
        public readonly int NetId;
        private int _ownerId;
        private List<NetBehaviour> _behaviours;

        /// <summary>
        /// Gets or sets the ID of the client that owns this networked object.
        /// </summary>
        public int OwnerId
        {
            get => _ownerId;
            set => _ownerId = value;
        }

        /// <summary>
        /// Indicates whether the local client owns this networked object.
        /// </summary>
        public bool Owned => _ownerId == NetManager.ConnectionId();

        /// <summary>
        /// Gets or sets the scene identifier for this networked object.
        /// </summary>
        public string SceneId { get; set; }

        public ObjectState State { get; set; }

        internal NetObject(int netId, NetBehaviour behaviour, int ownerId = -1)
        {
            NetId = netId;
            _ownerId = ownerId;
            _behaviours = behaviour.GetComponents<NetBehaviour>().ToList();

            foreach (var b in _behaviours)
            {
                Register(b);
            }
        }

        /// <summary>
        /// Transfers ownership of this networked object to the specified client.
        /// </summary>
        /// <param name="ownerId">The ID of the client that should become the new owner.</param>
        public void GiveOwner(int ownerId)
        {
            if (_ownerId == ownerId) return;
            if (!NetManager.AllPlayers.Contains(ownerId)) return;
            _ownerId = ownerId;
            
            NetMessage msg = new OwnershipMessage(NetId, ownerId);
            NetManager.Send(msg);
        }

        internal void Register(NetBehaviour obj)
        {
            if (!_behaviours.Contains(obj))
            {
                DebugQueue.AddMessage($"Registering {obj.GetType().Name} to {NetId}.", DebugQueue.MessageType.Warning);
                _behaviours.Add(obj);
            }
            obj.SetNetObject(this);
        }

        internal void Destroy()
        {
            GameObject.Destroy(_behaviours[0].gameObject);
        }

        internal void Enable()
        {
            foreach (var netBehaviour in _behaviours)
            {
                DebugQueue.AddMessage($"Enabling {netBehaviour.GetType().Name} | {NetId}.", DebugQueue.MessageType.Warning);
                if(!netBehaviour.isActiveAndEnabled) NetManager.EnqueueMainThread(() => netBehaviour.enabled = true);
            }
        }

        internal void Disconnect()
        {
            foreach (var netBehaviour in _behaviours)
            {
                netBehaviour.Disconnect();
            }
        }
    }
}
