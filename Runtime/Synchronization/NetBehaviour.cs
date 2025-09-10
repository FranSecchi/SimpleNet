using System;
using System.Collections.Generic;
using SimpleNet.Network;
using SimpleNet.Utilities;
using UnityEngine;

namespace SimpleNet.Synchronization
{
    /// <summary>
    /// Base class for network-enabled behaviours that can be synchronized across the network.
    /// </summary>
    [RequireComponent(typeof(SceneObjectId))]
    public abstract class NetBehaviour : MonoBehaviour
    {
        /// <summary>
        /// The network object associated with this behaviour. Behaviours in the same GameObject return the same NetObject.
        /// </summary>
        [NonSerialized]
        public NetObject NetObject;

        /// <summary>
        /// Gets the network ID of this behaviour's associated network object.
        /// </summary>
        public int NetID => NetObject.NetId;
        
        /// <summary>
        /// Gets whether this behaviour is owned by the local client.
        /// </summary>
        public bool IsOwned => NetObject.Owned;

        /// <summary>
        /// Indicates whether this behaviour has been spawned across the network.
        /// </summary>
        protected bool Spawned;
        
        private bool _registered;
        private bool _isPredicting;
        public bool IsPredicting => _isPredicting;
        /// <summary>
        /// Override - use only for declaring and initializing, network calls are not consistent.
        /// </summary>
        protected virtual void Awake()
        {
            if(GetComponent<SceneObjectId>().SceneId != "") RegisterAsSceneObject();
        }
        private void OnEnable()
        {
            if (NetObject == null)
                return;
            StateManager.Register(NetObject.NetId, this);
            RPCManager.Register(NetObject.NetId, this);
            
            if (NetManager.Rollback && IsOwned)
            {
                RollbackManager.UpdatePrediction += UpdatePrediction;
                if(!_isPredicting) StartPrediction();
            }
            if (!Spawned)
            {
                DebugQueue.AddMessage($"Spawned {GetType().Name} | {gameObject.name}.", DebugQueue.MessageType.Warning);

                Spawned = true;
                OnNetSpawn();
            }
            else 
            {
                OnNetEnable();
            }
        }

        private void OnDisable()
        {
            if (NetObject == null)
                return;
            StateManager.Unregister(NetObject.NetId, this);
            RPCManager.Unregister(NetObject.NetId, this);
            if (NetManager.Rollback && IsOwned && _isPredicting)
            {
                RollbackManager.UpdatePrediction -= UpdatePrediction;
                if(_isPredicting) StopPrediction();
            }
            OnNetDisable();
        }

        private void Start()
        {
            if (!_registered && NetObject == null)
            {
                RegisterAsSceneObject();
            }
            OnNetStart();
        }

        internal void Disconnect()
        {
            if(!NetManager.IsHost)return;
            OnDisconnect();
        }

        /// <summary>
        /// Called the frame after enabling the behaviour. Use it as you would use the default Start event.
        /// </summary>
        protected virtual void OnNetStart(){}

        /// <summary>
        /// Called the first frame the behaviour is enabled. Override this method to add custom enable logic.
        /// </summary>
        protected virtual void OnNetEnable(){ }

        /// <summary>
        /// Called the first frame the behaviour is disabled and on destroying it. Override this method to add custom disable logic.
        /// </summary>
        protected virtual void OnNetDisable(){ }

        /// <summary>
        /// Called once the object is spawned across the network. Enable/disable your NetBehaviours here, use it as any Awake event.
        /// </summary>
        protected virtual void OnNetSpawn(){ }

        /// <summary>
        /// Called when the owner of the behaviour is disconnected, handle here any owner transfer-ship.
        /// </summary>
        protected virtual void OnDisconnect(){}

        /// <summary>
        /// Sends an RPC call.
        /// </summary>
        /// <param name="methodName">The name of the method to call.</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        protected void CallRPC(string methodName, params object[] parameters)
        {
            if (NetObject != null)
            {
                if (_isPredicting)
                {
                    RollbackManager.RecordInput(NetID, methodName, parameters);
                }
                
                RPCManager.SendRPC(NetObject.NetId, methodName, parameters);
            }
        }

        /// <summary>
        /// Transfers ownership of this network object to a specific client.
        /// </summary>
        /// <param name="ownerId">The ID of the client that will own this object.</param>
        /// <param name="ownChildren">Whether to transfer ownership of child network objects as well.</param>
        public void Own(int ownerId, bool ownChildren = false)
        {
            if(NetObject == null) return;
            NetObject.GiveOwner(ownerId);
            if (ownChildren)
            {
                var childs = GetComponentsInChildren<NetBehaviour>();
                foreach (var child in childs)
                {
                    if (child == this) continue;
                    child.NetObject.GiveOwner(ownerId);
                }
            }
        }
        private void RegisterAsSceneObject()
        {
            if (!NetManager.Active || !NetManager.Running || _registered) return;
            
            _registered = true;
            enabled = false;                
            DebugQueue.AddMessage($"Disable {GetType().Name} | {gameObject.name}.", DebugQueue.MessageType.Warning);

            var behaviours = gameObject.GetComponents<NetBehaviour>();
            foreach (var behaviour in behaviours)
            {
                if (behaviour.NetObject != null)
                {
                    NetObject = behaviour.NetObject;
                    NetObject.Register(this);
                    return;
                }
            }
            NetScene.RegisterSceneObject(this);
        }
        internal void SetNetObject(NetObject obj)
        {
            if (obj == null) return;
            NetObject = obj;
            _registered = true;
            enabled = false;
        }
        
        
        private void StartPrediction()
        {
            if (!IsOwned) return;
            _isPredicting = true;
            DebugQueue.AddMessage($"Started prediction for {GetType().Name} | {gameObject.name}", DebugQueue.MessageType.Rollback);
            OnPredictionStart();
        }
        private void UpdatePrediction(RollbackManager.GameState state)
        {
            if (!_isPredicting || !IsOwned) return;
            if (!state.Snapshot.ContainsKey(NetID) || !state.Snapshot[NetID].HasComponent(this)) return;
            
            try
            {
                Predict((state.Timestamp - DateTime.UtcNow).Milliseconds, state.Snapshot[NetID], state.Inputs);
            }
            catch (Exception e)
            {
                DebugQueue.AddMessage($"Prediction failed for {GetType().Name}: {e.Message}", DebugQueue.MessageType.Error);
                StopPrediction();
            }
        }
        
        private void StopPrediction()
        {
            if (!IsOwned) return;
            _isPredicting = false;
            DebugQueue.AddMessage($"Stopped prediction for {GetType().Name} | {gameObject.name}", DebugQueue.MessageType.Rollback);
            OnPredictionStop();
        }
        
        protected virtual void OnPredictionStart() { }
        protected virtual void OnPredictionStop() { }
        protected virtual void OnPausePrediction(){ }
        protected virtual void OnResumePrediction(){ }

        public void PausePrediction()
        {
            if (!IsOwned) return;
            _isPredicting = false;
            OnPausePrediction();
        }

        public void ResumePrediction()
        {
            if (!IsOwned) return;
            _isPredicting = true;
            OnResumePrediction();
        }

        internal void OnReconciliation(int id, Dictionary<string, object> changes, DateTime timestamp)
        {
            if (changes == null || changes.Count == 0) return;

            if (_isPredicting)
            {
                _isPredicting = false;
                DebugQueue.AddMessage($"Reconciliation received for {GetType().Name} | {gameObject.name}", DebugQueue.MessageType.Rollback);
                if (IsDesynchronized(changes))
                {
                    DebugQueue.AddRollback(NetID, timestamp.Second, $"Desync detected for {GetType().Name} | {gameObject.name}");
                    RollbackManager.RollbackToTime(NetID, id, timestamp, changes);
                }
                else
                {
                    OnStateReconcile(changes);
                }
                NetManager.EnqueueMainThread(() => _isPredicting = true);
            }
        }
        
        protected virtual void OnStateReconcile(Dictionary<string, object> changes) { }
        
        protected virtual void Predict(float deltaTime, ObjectState lastState, List<InputCommand> lastInputs){ }

        protected virtual bool IsDesynchronized(Dictionary<string, object> changes){ return false; }
    }
}
