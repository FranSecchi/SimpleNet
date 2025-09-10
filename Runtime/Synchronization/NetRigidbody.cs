using System.Collections.Generic;
using SimpleNet.Network;
using UnityEngine;
using SimpleNet.Messages;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using SimpleNet.Utilities;

namespace SimpleNet.Synchronization
{
    [RequireComponent(typeof(Rigidbody))]
    public class NetRigidbody : NetBehaviour
    {
        [Header("Synchronization Settings")]
        [SerializeField] private bool syncPosition = true;
        [SerializeField] private bool syncRotation = true;
        [SerializeField] private bool syncVelocity = true;
        [SerializeField] private bool syncProperties = true;
        [SerializeField] public float syncPrecision = 0.01f; 

        [Header("Desync Thresholds")]
        [SerializeField] private float positionThreshold = 0.01f;
        [SerializeField] private float rotationThreshold = 0.01f;
        [SerializeField] private float velocityThreshold = 0.1f;

        [Header("Interpolation Settings")]
        [Space(5)]
        [Tooltip("These apply only on non-owned objects")]
        [SerializeField] private float interpolationSpeed = 10f;
        [SerializeField] private AnimationCurve interpolationCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private float maxTeleportDistance = 10f;
        [SerializeField] private bool useVelocityBasedInterpolation;
        [Tooltip("Smooth")]
        [SerializeField] private float positionSmoothingFactor = 1f;
        [SerializeField] private float rotationSmoothingFactor = 1f;
        [SerializeField] private float velocitySmoothingFactor = 1f;

        [Header("Physics Settings")]
        [SerializeField] private bool predictPhysics = true;
        [SerializeField] private float predictionTime = 0.1f;
        [SerializeField] private int predictionSteps = 3;
        [SerializeField] private bool smoothAngularVelocity = true;
        [SerializeField] private float angularVelocitySmoothing = 0.1f;

        [Sync] private float _positionX;
        [Sync] private float _positionY;
        [Sync] private float _positionZ;
        [Sync] private float _rotationX;
        [Sync] private float _rotationY;
        [Sync] private float _rotationZ;
        [Sync] private float _rotationW;
        [Sync] private float _velocityX;
        [Sync] private float _velocityY;
        [Sync] private float _velocityZ;
        [Sync] private float _angularVelocityX;
        [Sync] private float _angularVelocityY;
        [Sync] private float _angularVelocityZ;
        
        [Sync] private float _mass;
        [Sync] private bool _useGravity;

        private Rigidbody _rigidbody;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _targetVelocity;
        private Vector3 _targetAngularVelocity;
        private List<ForceCommand> _pendingForces = new List<ForceCommand>();
        private bool _wasKinematic;
        private CollisionDetectionMode _wasCollisionDetection;
        private RigidbodyInterpolation _wasInterpolation;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _lastVelocity;
        private Vector3 _lastAngularVelocity;

        private struct ForceCommand
        {
            public Vector3 Force;
            public ForceMode Mode;
            public float DeltaTime;
        }

        public bool SyncPosition
        {
            get => syncPosition;
            set => syncPosition = value;
        }

        public bool SyncRotation
        {
            get => syncRotation;
            set => syncRotation = value;
        }

        public bool SyncVelocity
        {
            get => syncVelocity;
            set => syncVelocity = value;
        }

        public bool SyncProperties
        {
            get => syncProperties;
            set => syncProperties = value;
        }

        protected override void OnNetSpawn()
        {
            if (!NetManager.Active || !NetManager.Running)
                return;
            _rigidbody = GetComponent<Rigidbody>();

            if(!IsOwned)
            {
                _wasKinematic = _rigidbody.isKinematic;
                _wasCollisionDetection = _rigidbody.collisionDetectionMode;
                _wasInterpolation = _rigidbody.interpolation;
                
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                _rigidbody.isKinematic = true;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                
                syncProperties = true;
            }

            _lastPosition = _rigidbody.position;
            _lastRotation = _rigidbody.rotation;
            _lastVelocity = _rigidbody.velocity;
            _lastAngularVelocity = _rigidbody.angularVelocity;

            SetState();

            _targetPosition = _rigidbody.position;
            _targetRotation = _rigidbody.rotation;
            _targetVelocity = _rigidbody.velocity;
            _targetAngularVelocity = _rigidbody.angularVelocity;
        }

        protected override void OnNetEnable()
        {
            if(!IsOwned)
            {
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                _rigidbody.isKinematic = true;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                
                syncProperties = true;
            }
        }

        protected override void OnNetDisable()
        {
            base.OnNetDisable();
            
            if (!IsOwned && _rigidbody != null)
            {
                _rigidbody.isKinematic = _wasKinematic;
                _rigidbody.collisionDetectionMode = _wasCollisionDetection;
                _rigidbody.interpolation = _wasInterpolation;
            }
        }

        /// <summary>
        /// Adds a force to be applied during prediction. This force will be synchronized across the network.
        /// </summary>
        /// <param name="force">The force to apply</param>
        /// <param name="mode">The mode in which to apply the force</param>
        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            if (!IsOwned) return;

            _pendingForces.Add(new ForceCommand
            {
                Force = force,
                Mode = mode,
                DeltaTime = Time.fixedDeltaTime
            });

            _rigidbody.AddForce(force, mode);
        }

        protected override void OnStateReconcile(Dictionary<string, object> changes)
        {
            if (!IsOwned) return;
            
            if (syncPosition)
            {
                if (changes.TryGetValue("_positionX", out object posX)) _targetPosition.x = (float)posX;
                if (changes.TryGetValue("_positionY", out object posY)) _targetPosition.y = (float)posY;
                if (changes.TryGetValue("_positionZ", out object posZ)) _targetPosition.z = (float)posZ;            
            }
            if (syncRotation)
            {
                if (changes.TryGetValue("_rotationX", out object rotX)) _targetRotation.x = (float)rotX;
                if (changes.TryGetValue("_rotationY", out object rotY)) _targetRotation.y = (float)rotY;
                if (changes.TryGetValue("_rotationZ", out object rotZ)) _targetRotation.z = (float)rotZ;
                if (changes.TryGetValue("_rotationW", out object rotW)) _targetRotation.w = (float)rotW;
            }
            if (syncVelocity)
            {
                if (changes.TryGetValue("_velocityX", out object velX)) _targetVelocity.x = (float)velX;
                if (changes.TryGetValue("_velocityY", out object velY)) _targetVelocity.y = (float)velY;
                if (changes.TryGetValue("_velocityZ", out object velZ)) _targetVelocity.z = (float)velZ;
            }
            if (syncProperties)
            {
                if (changes.TryGetValue("_mass", out object mass)) _mass = (float)mass;
                if (changes.TryGetValue("_useGravity", out object useGravity)) _useGravity = (bool)useGravity;
            }
            _pendingForces.Clear();
        }

        protected override bool IsDesynchronized(Dictionary<string, object> changes)
        {
            if (!IsOwned) return false;

            if (syncPosition)
            {
                if (changes.TryGetValue("_positionX", out object posX) && Mathf.Abs((float)posX - _rigidbody.position.x) > positionThreshold) return true;
                if (changes.TryGetValue("_positionY", out object posY) && Mathf.Abs((float)posY - _rigidbody.position.y) > positionThreshold) return true;
                if (changes.TryGetValue("_positionZ", out object posZ) && Mathf.Abs((float)posZ - _rigidbody.position.z) > positionThreshold) return true;
            }
            if (syncRotation)
            {
                if (changes.TryGetValue("_rotationX", out object rotX) && Mathf.Abs((float)rotX - _rigidbody.rotation.x) > rotationThreshold) return true;
                if (changes.TryGetValue("_rotationY", out object rotY) && Mathf.Abs((float)rotY - _rigidbody.rotation.y) > rotationThreshold) return true;
                if (changes.TryGetValue("_rotationZ", out object rotZ) && Mathf.Abs((float)rotZ - _rigidbody.rotation.z) > rotationThreshold) return true;
                if (changes.TryGetValue("_rotationW", out object rotW) && Mathf.Abs((float)rotW - _rigidbody.rotation.w) > rotationThreshold) return true;
            }
            if (syncVelocity)
            {
                if (changes.TryGetValue("_velocityX", out object velX) && Mathf.Abs((float)velX - _rigidbody.velocity.x) > velocityThreshold) return true;
                if (changes.TryGetValue("_velocityY", out object velY) && Mathf.Abs((float)velY - _rigidbody.velocity.y) > velocityThreshold) return true;
                if (changes.TryGetValue("_velocityZ", out object velZ) && Mathf.Abs((float)velZ - _rigidbody.velocity.z) > velocityThreshold) return true;
            }
            return false;
        }

        protected override void Predict(float deltaTime, ObjectState lastState, List<InputCommand> lastInputs)
        {
            if (!IsOwned || !predictPhysics) return;

            float stepTime = predictionTime / predictionSteps;
            Vector3 currentPosition = _rigidbody.position;
            Vector3 currentVelocity = _rigidbody.velocity;
            Quaternion currentRotation = _rigidbody.rotation;
            Vector3 currentAngularVelocity = _rigidbody.angularVelocity;

            for (int i = 0; i < predictionSteps; i++)
            {
                // Predict position
                currentPosition += currentVelocity * stepTime;
                if (_useGravity)
                {
                    currentVelocity += Physics.gravity * stepTime;
                }

                // Predict rotation
                currentRotation = Quaternion.Euler(currentAngularVelocity * (Mathf.Rad2Deg * stepTime)) * currentRotation;

                // Apply forces
                foreach (var force in _pendingForces)
                {
                    switch (force.Mode)
                    {
                        case ForceMode.Force:
                            currentVelocity += force.Force * stepTime / _mass;
                            break;
                        case ForceMode.Acceleration:
                            currentVelocity += force.Force * stepTime;
                            break;
                        case ForceMode.Impulse:
                            currentVelocity += force.Force / _mass;
                            break;
                        case ForceMode.VelocityChange:
                            currentVelocity += force.Force;
                            break;
                    }
                }
            }

            _targetPosition = currentPosition;
            _targetRotation = currentRotation;
            _targetVelocity = currentVelocity;
            _targetAngularVelocity = currentAngularVelocity;
        }

        private void FixedUpdate()
        {
            if (!NetManager.Active || !NetManager.Running)
                return;
            
            if (!IsOwned)
            {
                if (syncPosition)
                    _targetPosition = new Vector3(_positionX, _positionY, _positionZ);
    
                if (syncRotation)
                    _targetRotation = new Quaternion(_rotationX, _rotationY, _rotationZ, _rotationW);

                if (syncVelocity)
                {
                    _targetVelocity = new Vector3(_velocityX, _velocityY, _velocityZ);
                    if (smoothAngularVelocity)
                    {
                        Vector3 newAngularVelocity = new Vector3(_angularVelocityX, _angularVelocityY, _angularVelocityZ);
                        _targetAngularVelocity = Vector3.Lerp(_lastAngularVelocity, newAngularVelocity, angularVelocitySmoothing);
                    }
                    else
                    {
                        _targetAngularVelocity = new Vector3(_angularVelocityX, _angularVelocityY, _angularVelocityZ);
                    }
                }
                
                float lerpSpeed = Time.deltaTime * interpolationSpeed;
                float curveValue = interpolationCurve.Evaluate(lerpSpeed);
                
                if (syncPosition)
                {
                    if (useVelocityBasedInterpolation)
                    {
                        Vector3 velocity = (_targetPosition - _lastPosition) / Time.deltaTime;
                        _targetPosition += velocity * Time.deltaTime;
                    }

                    if (Vector3.Distance(_rigidbody.position, _targetPosition) > maxTeleportDistance)
                    {
                        _rigidbody.MovePosition(_targetPosition);
                    }
                    else
                    {
                        _rigidbody.MovePosition(Vector3.Lerp(_rigidbody.position, _targetPosition, curveValue * positionSmoothingFactor));
                    }
                }
                
                if (syncRotation)
                {
                    if (useVelocityBasedInterpolation)
                    {
                        Quaternion deltaRotation = Quaternion.Inverse(_lastRotation) * _targetRotation;
                        _targetRotation = _lastRotation * Quaternion.Slerp(Quaternion.identity, deltaRotation, Time.deltaTime);
                    }

                    _rigidbody.MoveRotation(Quaternion.Slerp(_rigidbody.rotation, _targetRotation, curveValue * rotationSmoothingFactor));
                }

                if (syncVelocity)
                {
                    if (useVelocityBasedInterpolation)
                    {
                        Vector3 velocityDelta = (_targetVelocity - _lastVelocity) / Time.deltaTime;
                        _targetVelocity += velocityDelta * Time.deltaTime;
                    }

                    _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, _targetVelocity, curveValue * velocitySmoothingFactor);
                    _rigidbody.angularVelocity = _targetAngularVelocity;
                }

                _lastPosition = _rigidbody.position;
                _lastRotation = _rigidbody.rotation;
                _lastVelocity = _rigidbody.velocity;
                _lastAngularVelocity = _rigidbody.angularVelocity;
            }
            else
            {
                SetState();
            }
        }

        private void SetState()
        {
            if (syncPosition)
            {
                _positionX = Quantize(_rigidbody.position.x);
                _positionY = Quantize(_rigidbody.position.y);
                _positionZ = Quantize(_rigidbody.position.z);
            }
            if (syncRotation)
            {
                _rotationX = Quantize(_rigidbody.rotation.x);
                _rotationY = Quantize(_rigidbody.rotation.y);
                _rotationZ = Quantize(_rigidbody.rotation.z);
                _rotationW = Quantize(_rigidbody.rotation.w);
            }
            if (syncVelocity)
            {
                _velocityX = Quantize(_rigidbody.velocity.x);
                _velocityY = Quantize(_rigidbody.velocity.y);
                _velocityZ = Quantize(_rigidbody.velocity.z);
                _angularVelocityX = Quantize(_rigidbody.angularVelocity.x);
                _angularVelocityY = Quantize(_rigidbody.angularVelocity.y);
                _angularVelocityZ = Quantize(_rigidbody.angularVelocity.z);
            }
            if (syncProperties)
            {
                _mass = _rigidbody.mass;
                _useGravity = _rigidbody.useGravity;
            }
        }
        private float Quantize(float value)
        {
            return Mathf.Round(value / syncPrecision) * syncPrecision;
        }
    }
} 