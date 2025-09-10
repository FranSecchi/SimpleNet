using System.Collections.Generic;
using SimpleNet.Network;
using UnityEngine;
using SimpleNet.Messages;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using SimpleNet.Utilities;

namespace SimpleNet.Synchronization
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class NetRigidbody2D : NetBehaviour
    {
        // [Header("Synchronization Settings")]
        // [SerializeField] private bool _syncPosition = true;
        // [SerializeField] private bool _syncRotation = true;
        // [SerializeField] private bool _syncVelocity = true;
        // [SerializeField] private bool _syncAngularVelocity = true;
        // [SerializeField] private bool _syncProperties = true;
        //
        // [Header("Desync Thresholds")]
        // [SerializeField] private float _positionThreshold = 0.01f;
        // [SerializeField] private float _rotationThreshold = 0.01f;
        // [SerializeField] private float _velocityThreshold = 0.1f;
        // [SerializeField] private float _angularVelocityThreshold = 0.1f;
        //
        // [Sync] private float _positionX;
        // [Sync] private float _positionY;
        // [Sync] private float _rotation;
        // [Sync] private float _velocityX;
        // [Sync] private float _velocityY;
        // [Sync] private float _angularVelocity;
        // [Sync] private float _mass;
        // [Sync] private float _drag;
        // [Sync] private float _angularDrag;
        // [Sync] private float _gravityScale;
        // [Sync] private bool _isKinematic;
        //
        // // Interpolation settings
        // [SerializeField] private float _interpolationBackTime = 0.1f;
        // [SerializeField] private float _interpolationSpeed = 10f;
        //
        // private bool _isSynchronized = true;
        // private Rigidbody2D _rigidbody;
        // private Vector2 _targetPosition;
        // private float _targetRotation;
        // private Vector2 _targetVelocity;
        // private float _targetAngularVelocity;
        // private bool _hasTargetState;
        //
        // public bool SyncPosition
        // {
        //     get => _syncPosition;
        //     set => _syncPosition = value;
        // }
        //
        // public bool SyncRotation
        // {
        //     get => _syncRotation;
        //     set => _syncRotation = value;
        // }
        //
        // public bool SyncVelocity
        // {
        //     get => _syncVelocity;
        //     set => _syncVelocity = value;
        // }
        //
        // public bool SyncAngularVelocity
        // {
        //     get => _syncAngularVelocity;
        //     set => _syncAngularVelocity = value;
        // }
        //
        // public bool SyncProperties
        // {
        //     get => _syncProperties;
        //     set => _syncProperties = value;
        // }
        //
        // public override void PausePrediction()
        // {
        //     _isSynchronized = false;
        //     _hasTargetState = false;
        //     
        //     if (_syncPosition)
        //     {
        //         _positionX = _rigidbody.position.x;
        //         _positionY = _rigidbody.position.y;
        //     }
        //     if (_syncRotation)
        //     {
        //         _rotation = _rigidbody.rotation;
        //     }
        //     if (_syncVelocity)
        //     {
        //         _velocityX = _rigidbody.velocity.x;
        //         _velocityY = _rigidbody.velocity.y;
        //     }
        //     if (_syncAngularVelocity)
        //     {
        //         _angularVelocity = _rigidbody.angularVelocity;
        //     }
        //     if (_syncProperties)
        //     {
        //         _mass = _rigidbody.mass;
        //         _drag = _rigidbody.drag;
        //         _angularDrag = _rigidbody.angularDrag;
        //         _gravityScale = _rigidbody.gravityScale;
        //         _isKinematic = _rigidbody.isKinematic;
        //     }
        // }
        //
        // public override void ResumePrediction()
        // {
        //     _isSynchronized = true;
        // }
        //
        // protected override void OnNetSpawn()
        // {
        //     if (!NetManager.Active || !NetManager.Running)
        //         return;
        //     _rigidbody = GetComponent<Rigidbody2D>();
        //
        //     if (_syncPosition)
        //     {
        //         _positionX = _rigidbody.position.x;
        //         _positionY = _rigidbody.position.y;
        //     }
        //     if (_syncRotation)
        //     {
        //         _rotation = _rigidbody.rotation;
        //     }
        //     if (_syncVelocity)
        //     {
        //         _velocityX = _rigidbody.velocity.x;
        //         _velocityY = _rigidbody.velocity.y;
        //     }
        //     if (_syncAngularVelocity)
        //     {
        //         _angularVelocity = _rigidbody.angularVelocity;
        //     }
        //     if (_syncProperties)
        //     {
        //         _mass = _rigidbody.mass;
        //         _drag = _rigidbody.drag;
        //         _angularDrag = _rigidbody.angularDrag;
        //         _gravityScale = _rigidbody.gravityScale;
        //         _isKinematic = _rigidbody.isKinematic;
        //     }
        //
        //     _targetPosition = _rigidbody.position;
        //     _targetRotation = _rigidbody.rotation;
        //     _targetVelocity = _rigidbody.velocity;
        //     _targetAngularVelocity = _rigidbody.angularVelocity;
        //     _hasTargetState = true;
        // }
        //
        // protected override void OnStateReconcile(Dictionary<string, object> changes)
        // {
        //     if (_syncPosition)
        //     {
        //         if (changes.ContainsKey("_positionX")) _positionX = (float)changes["_positionX"];
        //         if (changes.ContainsKey("_positionY")) _positionY = (float)changes["_positionY"];
        //     }
        //     if (_syncRotation)
        //     {
        //         if (changes.ContainsKey("_rotation")) _rotation = (float)changes["_rotation"];
        //     }
        //     if (_syncVelocity)
        //     {
        //         if (changes.ContainsKey("_velocityX")) _velocityX = (float)changes["_velocityX"];
        //         if (changes.ContainsKey("_velocityY")) _velocityY = (float)changes["_velocityY"];
        //     }
        //     if (_syncAngularVelocity)
        //     {
        //         if (changes.ContainsKey("_angularVelocity")) _angularVelocity = (float)changes["_angularVelocity"];
        //     }
        //     if (_syncProperties)
        //     {
        //         if (changes.ContainsKey("_mass")) _mass = (float)changes["_mass"];
        //         if (changes.ContainsKey("_drag")) _drag = (float)changes["_drag"];
        //         if (changes.ContainsKey("_angularDrag")) _angularDrag = (float)changes["_angularDrag"];
        //         if (changes.ContainsKey("_gravityScale")) _gravityScale = (float)changes["_gravityScale"];
        //         if (changes.ContainsKey("_isKinematic")) _isKinematic = (bool)changes["_isKinematic"];
        //     }
        //
        //     _targetPosition = new Vector2(_positionX, _positionY);
        //     _targetRotation = _rotation;
        //     _targetVelocity = new Vector2(_velocityX, _velocityY);
        //     _targetAngularVelocity = _angularVelocity;
        //     _hasTargetState = true;
        // }
        //
        // protected override bool IsDesynchronized(Dictionary<string, object> changes)
        // {
        //     if (!isOwned) return false;
        //
        //     if (_syncPosition)
        //     {
        //         if (changes.ContainsKey("_positionX") && Mathf.Abs((float)changes["_positionX"] - _rigidbody.position.x) > _positionThreshold) return true;
        //         if (changes.ContainsKey("_positionY") && Mathf.Abs((float)changes["_positionY"] - _rigidbody.position.y) > _positionThreshold) return true;
        //     }
        //     if (_syncRotation)
        //     {
        //         if (changes.ContainsKey("_rotation") && Mathf.Abs((float)changes["_rotation"] - _rigidbody.rotation) > _rotationThreshold) return true;
        //     }
        //     if (_syncVelocity)
        //     {
        //         if (changes.ContainsKey("_velocityX") && Mathf.Abs((float)changes["_velocityX"] - _rigidbody.velocity.x) > _velocityThreshold) return true;
        //         if (changes.ContainsKey("_velocityY") && Mathf.Abs((float)changes["_velocityY"] - _rigidbody.velocity.y) > _velocityThreshold) return true;
        //     }
        //     if (_syncAngularVelocity)
        //     {
        //         if (changes.ContainsKey("_angularVelocity") && Mathf.Abs((float)changes["_angularVelocity"] - _rigidbody.angularVelocity) > _angularVelocityThreshold) return true;
        //     }
        //
        //     return false;
        // }
        //
        // protected override void Predict(float deltaTime)
        // {
        //     if (_syncPosition)
        //     {
        //         _positionX = _rigidbody.position.x;
        //         _positionY = _rigidbody.position.y;
        //     }
        //     if (_syncRotation)
        //     {
        //         _rotation = _rigidbody.rotation;
        //     }
        //     if (_syncVelocity)
        //     {
        //         _velocityX = _rigidbody.velocity.x;
        //         _velocityY = _rigidbody.velocity.y;
        //     }
        //     if (_syncAngularVelocity)
        //     {
        //         _angularVelocity = _rigidbody.angularVelocity;
        //     }
        //     if (_syncProperties)
        //     {
        //         _mass = _rigidbody.mass;
        //         _drag = _rigidbody.drag;
        //         _angularDrag = _rigidbody.angularDrag;
        //         _gravityScale = _rigidbody.gravityScale;
        //         _isKinematic = _rigidbody.isKinematic;
        //     }
        // }
        //
        // private void FixedUpdate()
        // {
        //     if (!NetManager.Active || !NetManager.Running || !_isSynchronized)
        //         return;
        //
        //     if (!isOwned)
        //     {
        //         if (NetObject?.State != null)
        //         {
        //             if (_syncPosition)
        //             {
        //                 _positionX = GetFieldValue<float>("_positionX");
        //                 _positionY = GetFieldValue<float>("_positionY");
        //             }
        //             if (_syncRotation)
        //             {
        //                 _rotation = GetFieldValue<float>("_rotation");
        //             }
        //             if (_syncVelocity)
        //             {
        //                 _velocityX = GetFieldValue<float>("_velocityX");
        //                 _velocityY = GetFieldValue<float>("_velocityY");
        //             }
        //             if (_syncAngularVelocity)
        //             {
        //                 _angularVelocity = GetFieldValue<float>("_angularVelocity");
        //             }
        //             if (_syncProperties)
        //             {
        //                 _mass = GetFieldValue<float>("_mass");
        //                 _drag = GetFieldValue<float>("_drag");
        //                 _angularDrag = GetFieldValue<float>("_angularDrag");
        //                 _gravityScale = GetFieldValue<float>("_gravityScale");
        //                 _isKinematic = GetFieldValue<bool>("_isKinematic");
        //             }
        //
        //             _targetPosition = new Vector2(_positionX, _positionY);
        //             _targetRotation = _rotation;
        //             _targetVelocity = new Vector2(_velocityX, _velocityY);
        //             _targetAngularVelocity = _angularVelocity;
        //             _hasTargetState = true;
        //         }
        //
        //         if (_hasTargetState)
        //         {
        //             if (_syncPosition)
        //                 _rigidbody.position = Vector2.Lerp(_rigidbody.position, _targetPosition, Time.fixedDeltaTime * _interpolationSpeed);
        //             if (_syncRotation)
        //                 _rigidbody.rotation = Mathf.Lerp(_rigidbody.rotation, _targetRotation, Time.fixedDeltaTime * _interpolationSpeed);
        //             if (_syncVelocity)
        //                 _rigidbody.velocity = Vector2.Lerp(_rigidbody.velocity, _targetVelocity, Time.fixedDeltaTime * _interpolationSpeed);
        //             if (_syncAngularVelocity)
        //                 _rigidbody.angularVelocity = Mathf.Lerp(_rigidbody.angularVelocity, _targetAngularVelocity, Time.fixedDeltaTime * _interpolationSpeed);
        //         }
        //     }
        //     else
        //     {
        //         if (_syncPosition)
        //         {
        //             _positionX = _rigidbody.position.x;
        //             _positionY = _rigidbody.position.y;
        //         }
        //         if (_syncRotation)
        //         {
        //             _rotation = _rigidbody.rotation;
        //         }
        //         if (_syncVelocity)
        //         {
        //             _velocityX = _rigidbody.velocity.x;
        //             _velocityY = _rigidbody.velocity.y;
        //         }
        //         if (_syncAngularVelocity)
        //         {
        //             _angularVelocity = _rigidbody.angularVelocity;
        //         }
        //         if (_syncProperties)
        //         {
        //             _mass = _rigidbody.mass;
        //             _drag = _rigidbody.drag;
        //             _angularDrag = _rigidbody.angularDrag;
        //             _gravityScale = _rigidbody.gravityScale;
        //             _isKinematic = _rigidbody.isKinematic;
        //         }
        //     }
        // }
    }
}