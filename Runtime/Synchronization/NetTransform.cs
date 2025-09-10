using System.Collections.Generic;
using SimpleNet.Network;
using UnityEngine;

namespace SimpleNet.Synchronization
{
    public class NetTransform : NetBehaviour
    {
        [Header("Synchronizes")]
        [SerializeField] private bool syncPosition = true;
        [SerializeField] private bool syncRotation = true;
        [SerializeField] private bool syncScale = true;
        [SerializeField] public float syncPrecision = 0.01f; 

        [Header("Desync Thresholds")]
        [Tooltip("For rollback")]
        [SerializeField] private float positionThreshold = 0.01f;
        [SerializeField] private float rotationThreshold = 0.01f;
        [SerializeField] private float scaleThreshold = 0.01f;

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
        [SerializeField] private float scaleSmoothingFactor = 1f;

        [Sync] private float _positionX;
        [Sync] private float _positionY;
        [Sync] private float _positionZ;
        [Sync] private float _rotationX;
        [Sync] private float _rotationY;
        [Sync] private float _rotationZ;
        [Sync] private float _rotationW;
        [Sync] private float _scaleX;
        [Sync] private float _scaleY;
        [Sync] private float _scaleZ;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _targetScale;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _lastScale;



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

        public bool SyncScale
        {
            get => syncScale;
            set => syncScale = value;
        }

        protected override void OnNetSpawn()
        {
            if (!NetManager.Active || !NetManager.Running)
                return;
            if (!syncPosition && !syncRotation && !syncScale)
            {
                enabled = false;
                return;
            }
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            _lastScale = transform.localScale;
            SetState();
        }

        private void Update()
        {
            if (!NetManager.Active || !NetManager.Running)
                return;

            if (!IsOwned)
            {
                if (syncPosition)
                    _targetPosition = new Vector3(_positionX, _positionY, _positionZ);
    
                if (syncRotation)
                    _targetRotation = new Quaternion(_rotationX, _rotationY, _rotationZ, _rotationW);
    
                if (syncScale)
                    _targetScale = new Vector3(_scaleX, _scaleY, _scaleZ);


                float lerpSpeed = Time.deltaTime * interpolationSpeed;
                float curveValue = interpolationCurve.Evaluate(lerpSpeed);

                if (syncPosition)
                {
                    if (useVelocityBasedInterpolation)
                    {
                        Vector3 velocity = (_targetPosition - _lastPosition) / Time.deltaTime;
                        _targetPosition += velocity * Time.deltaTime;
                    }

                    transform.position = Vector3.Distance(transform.position, _targetPosition) > maxTeleportDistance 
                                            ? _targetPosition 
                                            : Vector3.Lerp(transform.position, _targetPosition, curveValue * positionSmoothingFactor);
                }

                if (syncRotation)
                {
                    if (useVelocityBasedInterpolation)
                    {
                        Quaternion deltaRotation = Quaternion.Inverse(_lastRotation) * _targetRotation;
                        _targetRotation = _lastRotation * Quaternion.Slerp(Quaternion.identity, deltaRotation, Time.deltaTime);
                    }

                    transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, curveValue * rotationSmoothingFactor);
                }

                if (syncScale)
                {
                    if (useVelocityBasedInterpolation)
                    {
                        Vector3 scaleVelocity = (_targetScale - _lastScale) / Time.deltaTime;
                        _targetScale += scaleVelocity * Time.deltaTime;
                    }

                    transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, curveValue * scaleSmoothingFactor);
                }

                _lastPosition = transform.position;
                _lastRotation = transform.rotation;
                _lastScale = transform.localScale;
            }
            else SetState();
        }

        protected override void OnStateReconcile(Dictionary<string, object> changes)
        {
            if (!IsOwned) return;
            
            if (syncPosition)
            {
                if (changes.TryGetValue("_positionX", out var x)) _targetPosition.x = (float)x;
                if (changes.TryGetValue("_positionY", out var y)) _targetPosition.y = (float)y;
                if (changes.TryGetValue("_positionZ", out var z)) _targetPosition.z = (float)z;
            }
            
            if (syncRotation)
            {
                if (changes.TryGetValue("_rotationX", out var x)) _targetRotation.x = (float)x;
                if (changes.TryGetValue("_rotationY", out var y)) _targetRotation.y = (float)y;
                if (changes.TryGetValue("_rotationZ", out var z)) _targetRotation.z = (float)z;
                if (changes.TryGetValue("_rotationW", out var w)) _targetRotation.w = (float)w;
            }
            
            if (syncScale)
            {
                if (changes.TryGetValue("_scaleX", out var x)) _targetScale.x = (float)x;
                if (changes.TryGetValue("_scaleY", out var y)) _targetScale.y = (float)y;
                if (changes.TryGetValue("_scaleZ", out var z)) _targetScale.z = (float)z;
            }
        }

        protected override bool IsDesynchronized(Dictionary<string, object> changes)
        {
            if (!IsOwned) return false;

            if (syncPosition)
            {
                if (changes.ContainsKey("_positionX") && Mathf.Abs((float)changes["_positionX"] - transform.position.x) > positionThreshold) return true;
                if (changes.ContainsKey("_positionY") && Mathf.Abs((float)changes["_positionY"] - transform.position.y) > positionThreshold) return true;
                if (changes.ContainsKey("_positionZ") && Mathf.Abs((float)changes["_positionZ"] - transform.position.z) > positionThreshold) return true;
            }
            
            if (syncRotation)
            {
                if (changes.ContainsKey("_rotationX") && Mathf.Abs((float)changes["_rotationX"] - transform.rotation.x) > rotationThreshold) return true;
                if (changes.ContainsKey("_rotationY") && Mathf.Abs((float)changes["_rotationY"] - transform.rotation.y) > rotationThreshold) return true;
                if (changes.ContainsKey("_rotationZ") && Mathf.Abs((float)changes["_rotationZ"] - transform.rotation.z) > rotationThreshold) return true;
                if (changes.ContainsKey("_rotationW") && Mathf.Abs((float)changes["_rotationW"] - transform.rotation.w) > rotationThreshold) return true;
            }
            
            if (syncScale)
            {
                if (changes.ContainsKey("_scaleX") && Mathf.Abs((float)changes["_scaleX"] - transform.localScale.x) > scaleThreshold) return true;
                if (changes.ContainsKey("_scaleY") && Mathf.Abs((float)changes["_scaleY"] - transform.localScale.y) > scaleThreshold) return true;
                if (changes.ContainsKey("_scaleZ") && Mathf.Abs((float)changes["_scaleZ"] - transform.localScale.z) > scaleThreshold) return true;
            }

            return false;
        }
        
        protected override void OnPausePrediction()
        {
            SetState();
        }
        protected override void Predict(float deltaTime, ObjectState lastState, List<InputCommand> lastInputs)
        {
            if (syncPosition)
            {
                _targetPosition = transform.position;
            }

            if (syncRotation)
            {
                _targetRotation = transform.rotation;
            }

            if (syncScale)
            {
                _targetScale = transform.localScale;
            }
        }


        private void SetState()
        {
            if (syncPosition)
            {
                _positionY = Quantize(transform.position.y);
                _positionX = Quantize(transform.position.x);
                _positionZ = Quantize(transform.position.z);
            
            }
            if (syncRotation)
            {
                _rotationX = Quantize(transform.rotation.x);
                _rotationY = Quantize(transform.rotation.y);
                _rotationZ = Quantize(transform.rotation.z);
                _rotationW = Quantize(transform.rotation.w);
            }
            if (syncScale)
            {
                _scaleX = Quantize(transform.localScale.x);
                _scaleY = Quantize(transform.localScale.y);
                _scaleZ = Quantize(transform.localScale.z);
            }
        }
        private float Quantize(float value)
        {
            return Mathf.Round(value / syncPrecision) * syncPrecision;
        }

    }
}
