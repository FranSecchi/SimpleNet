using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Utilities;
using UnityEngine;

namespace SimpleNet.Synchronization
{
    public struct InputCommand
    {
        public int NetId;
        public string MethodName;
        public object[] Parameters;
        public DateTime Timestamp;
    }
    internal static class RollbackManager
    {
        private static float _rollbackWindow = 0.1f;
        private static int _maxStates = 20;
        private static readonly ConcurrentQueue<GameState> _stateHistory = new ConcurrentQueue<GameState>();
        private static readonly ConcurrentQueue<InputCommand> _inputBuffer = new ConcurrentQueue<InputCommand>();
        private static readonly object _stateLock = new object();
        
        internal static Action<GameState> UpdatePrediction;

        internal struct GameState
        {
            public DateTime Timestamp;
            public Dictionary<int, ObjectState> Snapshot;
            public List<InputCommand> Inputs;
        }
        
        internal static void Initialize(float rollbackWindow = 0.1f, int maxStates = 20)
        {
            _rollbackWindow = rollbackWindow;
            _maxStates = maxStates;
            Clear();
            Messager.RegisterHandler<ReconcileMessage>(OnReconcileMessage);
        }

        private static void OnReconcileMessage(ReconcileMessage obj)
        {
            if (_stateHistory.IsEmpty)
                return;

            var targetState = GetStateAtTime(obj.Timestamp);
            if (!targetState.HasValue)
            {
                DebugQueue.AddMessage($"Failed to find state for reconciliation of object {obj.ObjectId} at time {obj.Timestamp:HH:mm:ss.fff}", DebugQueue.MessageType.Rollback);
                return;
            }

            if (!targetState.Value.Snapshot.TryGetValue(obj.ObjectId, out var state))
            {
                DebugQueue.AddMessage($"Object {obj.ObjectId} not found in state snapshot at time {obj.Timestamp:HH:mm:ss.fff}", DebugQueue.MessageType.Rollback);
                return;
            }

            if (!state.HasComponent(obj.ComponentId))
            {
                DebugQueue.AddMessage($"Component {obj.ComponentId} not found in object {obj.ObjectId} at time {obj.Timestamp:HH:mm:ss.fff}", DebugQueue.MessageType.Rollback);
                return;
            }

            var timeDiff = (DateTime.UtcNow - obj.Timestamp).TotalSeconds;
            state.Reconcile(obj.ObjectId, obj.ComponentId, obj.Values, obj.Timestamp);
            
            DebugQueue.AddMessage($"Reconciled object {obj.ObjectId} component {obj.ComponentId} at time {obj.Timestamp:HH:mm:ss.fff} (time diff: {timeDiff:F3}s)", DebugQueue.MessageType.Rollback);
        }

        internal static void Update()
        {
            if(!_stateHistory.IsEmpty && _stateHistory.TryPeek(out var state))
                UpdatePrediction?.Invoke(state);
                
            StoreCurrentState();
            CleanupOldStates();
        }
        
        private static void StoreCurrentState()
        {
            lock (_stateLock)
            {
                var currentTime = DateTime.UtcNow;
                
                while (!_stateHistory.IsEmpty)
                {
                    if (!_stateHistory.TryPeek(out var oldestState)) break;
                    
                    if ((currentTime - oldestState.Timestamp).TotalSeconds > _rollbackWindow)
                    {
                        if (_stateHistory.TryDequeue(out var oldState))
                        {
                            oldState.Snapshot.Clear();
                            oldState.Inputs.Clear();
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                
                if (_stateHistory.Count < _maxStates)
                {
                    var currentState = new GameState
                    {
                        Timestamp = currentTime,
                        Snapshot = new Dictionary<int, ObjectState>(),
                        Inputs = new List<InputCommand>()
                    };
                    
                    foreach (var kvp in StateManager.GetAllStates())
                    {
                        currentState.Snapshot[kvp.Key] = kvp.Value.Clone();
                    }
                    
                    _stateHistory.Enqueue(currentState);
                }
            }
        }

        private static void CleanupOldStates()
        {
            lock (_stateLock)
            {
                if (_stateHistory.IsEmpty) return;
                
                var currentTime = DateTime.UtcNow;
                
                while (!_stateHistory.IsEmpty)
                {
                    if (!_stateHistory.TryPeek(out var oldestState)) break;
                    
                    if ((currentTime - oldestState.Timestamp).TotalSeconds > _rollbackWindow)
                    {
                        if (_stateHistory.TryDequeue(out var oldState))
                        {
                            oldState.Snapshot.Clear();
                            oldState.Inputs.Clear();
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                
                while (!_inputBuffer.IsEmpty)
                {
                    if (!_inputBuffer.TryPeek(out var oldestInput)) break;
                    
                    if ((currentTime - oldestInput.Timestamp).TotalSeconds > _rollbackWindow)
                    {
                        _inputBuffer.TryDequeue(out _);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Records an input command for a networked object, including its method call and parameters.
        /// </summary>
        /// <param name="netId">The network ID of the object receiving the input</param>
        /// <param name="methodName">The name of the method to be called</param>
        /// <param name="parameters">The parameters to be passed to the method</param>
        public static void RecordInput(int netId, string methodName, params object[] parameters)
        {
            var input = new InputCommand
            {
                NetId = netId,
                MethodName = methodName,
                Parameters = parameters,
                Timestamp = DateTime.UtcNow
            };
            
            _inputBuffer.Enqueue(input);
            
            lock (_stateLock)
            {
                if (!_stateHistory.IsEmpty && _stateHistory.TryPeek(out var currentState))
                {
                    currentState.Inputs.Add(input);
                }
            }
        }
        
        /// <summary>
        /// Rolls back the game state to a specific point in time and replays all inputs that occurred after that time.
        /// </summary>
        /// <param name="targetTime">The target time to roll back to</param>
        public static void RollbackToTime(DateTime targetTime)
        {
            var targetState = GetStateAtTime(targetTime);
            if (!targetState.HasValue)
            {
                DebugQueue.AddMessage($"Failed to find state for rollback at time {targetTime:HH:mm:ss.fff}", DebugQueue.MessageType.Rollback);
                return;
            }
            
            DebugQueue.AddMessage($"Starting rollback to {targetTime:HH:mm:ss.fff}", DebugQueue.MessageType.Rollback);
            
            foreach (var kvp in targetState.Value.Snapshot)
            {
                StateManager.RestoreState(kvp.Key, kvp.Value);
            }
            
            var inputsToReplay = GetInputsAtTime(targetTime);
            if (inputsToReplay != null)
            {
                foreach (var input in inputsToReplay)
                {
                    RPCManager.CallRPC(input.NetId, input.MethodName, input.Parameters);
                }
                DebugQueue.AddMessage($"Rollback completed, replaying {inputsToReplay.Count} inputs", DebugQueue.MessageType.Rollback);
            }
        }

        /// <summary>
        /// Rolls back a specific networked object to a given time and applies the provided changes.
        /// </summary>
        /// <param name="netId">The network ID of the object to roll back</param>
        /// <param name="id">The ID of the change to apply</param>
        /// <param name="targetTime">The target time to roll back to</param>
        /// <param name="changes">The changes to apply to the object</param>
        public static void RollbackToTime(int netId, int id, DateTime targetTime, Dictionary<string, object> changes)
        {
            if (_stateHistory.IsEmpty)
            {
                DebugQueue.AddMessage($"Object {netId} failed to roll back to {targetTime:HH:mm:ss.fff} - Invalid state", DebugQueue.MessageType.Rollback);
                return;
            }
            
            var targetState = GetStateAtTime(targetTime);
            if (!targetState.HasValue)
            {
                DebugQueue.AddMessage($"Object {netId} failed to roll back to {targetTime:HH:mm:ss.fff} - No state found", DebugQueue.MessageType.Rollback);
                return;
            }
            
            DebugQueue.AddMessage($"Starting object-specific rollback for {netId} to {targetTime:HH:mm:ss.fff}", DebugQueue.MessageType.Rollback);
            
            if (targetState.Value.Snapshot.TryGetValue(netId, out var state))
            {
                StateManager.RestoreState(netId, state);
                
                var inputsToReplay = GetInputsAtTime(targetTime);
                if (inputsToReplay != null)
                {
                    int replayedInputs = 0;
                    foreach (var input in inputsToReplay)
                    {
                        if (input.NetId == netId)
                        {
                            RPCManager.CallRPC(input.NetId, input.MethodName, input.Parameters);
                            replayedInputs++;
                        }
                    }
                    state.SetChange(id, changes);
                    DebugQueue.AddMessage($"Object {netId} rollback completed, replayed {replayedInputs} inputs", DebugQueue.MessageType.Rollback);
                }
            }
        }
        
        /// <summary>
        /// Clears all stored game states and input buffers.
        /// </summary>
        public static void Clear()
        {
            lock (_stateLock)
            {
                int stateCount = _stateHistory.Count;
                int inputCount = _inputBuffer.Count;
                
                while (!_stateHistory.IsEmpty)
                {
                    if (_stateHistory.TryDequeue(out var state))
                    {
                        state.Snapshot.Clear();
                        state.Inputs.Clear();
                    }
                }
                
                while (!_inputBuffer.IsEmpty)
                {
                    _inputBuffer.TryDequeue(out _);
                }
                
                DebugQueue.AddMessage($"RollbackManager cleared: {stateCount} states and {inputCount} inputs removed", DebugQueue.MessageType.Rollback);
            }
        }

        /// <summary>
        /// Retrieves the game state closest to the specified target time.
        /// </summary>
        /// <param name="targetTime">The target time to find the closest state for</param>
        /// <returns>The closest game state to the target time, or null if no valid state is found</returns>
        public static GameState? GetStateAtTime(DateTime targetTime)
        {
            if (_stateHistory.IsEmpty) return null;
            
            GameState? closestState = null;
            TimeSpan closestDiff = TimeSpan.MaxValue;
            
            foreach (var state in _stateHistory)
            {
                var diff = (state.Timestamp - targetTime).Duration();
                if (diff < closestDiff)
                {
                    closestDiff = diff;
                    closestState = state;
                }
            }
            
            if (closestState == null || closestDiff.TotalSeconds > _rollbackWindow)
            {
                DebugQueue.AddMessage($"Failed to find state to roll back to at time {targetTime:HH:mm:ss.fff} (closest diff: {closestDiff.TotalMilliseconds}ms)", DebugQueue.MessageType.Rollback);
                return null;
            }
            
            return closestState;
        }

        /// <summary>
        /// Retrieves all input commands that occurred at or after the specified target time.
        /// </summary>
        /// <param name="targetTime">The target time to get inputs from</param>
        /// <returns>A list of input commands, or null if no inputs are found</returns>
        public static List<InputCommand> GetInputsAtTime(DateTime targetTime)
        {
            if (_inputBuffer.IsEmpty) return null;
            
            var inputsToReplay = new List<InputCommand>();
            TimeSpan closestDiff = TimeSpan.MaxValue;
            
            foreach (var input in _inputBuffer)
            {
                var diff = (input.Timestamp - targetTime).Duration();
                if (diff < closestDiff)
                {
                    inputsToReplay.Add(input);
                }
            }
            return inputsToReplay;
        }
    }
} 