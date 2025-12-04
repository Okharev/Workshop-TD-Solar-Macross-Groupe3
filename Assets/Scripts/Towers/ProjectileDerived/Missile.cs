using System;
using System.Collections.Generic;
using Enemy;
using UnityEditor.PackageManager;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Towers
{
    [RequireComponent(typeof(Rigidbody))]
    public class HomingMissile : BaseProjectile
    {
        [Header("Ballistics & Movement")] [SerializeField]
        private float speed = 30f;

        [SerializeField] private float rotateSpeed = 300.0f;
        [SerializeField] private float waypointReachedThreshold = 1.5f;
        [SerializeField] [Range(0f, 2f)] private float leadTargetMultiplier = 0.5f;

        [Header("Warhead")] [SerializeField] private float maxLifetime = 8f;

        [SerializeField] public int damage;
        [SerializeField] public float explosionRange;

        [Header("Re-targeting")] [SerializeField]
        private bool canRetarget = true;

        [SerializeField] private float retargetingRange = 20f;
        [SerializeField] private float retargetingCooldown = 0.25f;

        [Header("Aerodynamics (Wobble)")] [SerializeField]
        private float wobbleMagnitude = 5.0f;

        [SerializeField] private float wobbleFrequency = 4f;

        private readonly Queue<Vector3> _flightPath = new();

        // Internal State
        private MissileState _currentState;
        private Vector3 _currentWaypoint;
        private int _enemyLayer;
        private Transform _finalTarget;

        private float _lifetimeTimer;
        private float _perlinSeedX, _perlinSeedY;
        private Rigidbody _rb;
        private Rigidbody _targetRb;
        private float _timeSinceLastRetargetCheck;
        private float _waypointReachedThresholdSqr;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            _enemyLayer = LayerMask.GetMask("EnemyAir", "EnemyGround");
            _waypointReachedThresholdSqr = waypointReachedThreshold * waypointReachedThreshold;

            _perlinSeedX = Random.Range(0f, 100f);
            _perlinSeedY = Random.Range(0f, 100f);
        }

        private void Update()
        {
            _lifetimeTimer += Time.deltaTime;
            if (_lifetimeTimer > maxLifetime) Destroy(gameObject);
        }

        private void FixedUpdate()
        {
            if (!_finalTarget && _currentState == MissileState.Homing)
                if (!canRetarget || !TryFindNewTarget())
                {
                    _rb.linearVelocity = transform.forward * speed;
                    return;
                }

            switch (_currentState)
            {
                case MissileState.FollowingPath:
                    HandlePathFollowing();
                    break;
                case MissileState.Homing:
                    HandleHoming();
                    break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, explosionRange);
        }

        public void Setup(BaseTower tower)
        {
            source = tower;
        }

        public void Launch(IEnumerable<Vector3> pathPoints, Transform target)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            _flightPath.Clear();
            if (pathPoints != null)
                foreach (var point in pathPoints)
                    _flightPath.Enqueue(point);

            _finalTarget = target;
            if (_finalTarget) _finalTarget.TryGetComponent(out _targetRb);

            _lifetimeTimer = 0f;

            if (_flightPath.Count > 0)
            {
                _currentState = MissileState.FollowingPath;
                _currentWaypoint = _flightPath.Dequeue();
                transform.LookAt(_currentWaypoint);
            }
            else
            {
                _currentState = MissileState.Homing;
                if (_finalTarget) transform.LookAt(_finalTarget);
            }
        }

        protected override bool IsValidHit(Collider hitObject)
        {
            return true;
        }

        protected override void HandleImpact(Collider other)
        {
            // 1. AoE Damage Logic
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionRange, collidersCache, _enemyLayer);
            var validHits = collidersCache.AsSpan(0, hitCount);

            foreach (var col in validHits)
            {
                if (!col.TryGetComponent<HealthComponent>(out var victim)) return;
                source.Events.OnHit?.Invoke(new UpgradeProvider.OnHitData()
                {
                    Origin = gameObject,
                    Target = gameObject
                });


                if (victim.TakeDamage(Mathf.RoundToInt(source.damage.Value)))
                {
                    source.Events.OnKill?.Invoke(new UpgradeProvider.OnKillData()
                    {
                        Origin = gameObject,
                        Target = gameObject
                    });
                }
            }

            Destroy(gameObject);
        }


        private void HandlePathFollowing()
        {
            FlyTowards(_currentWaypoint);

            if ((_currentWaypoint - _rb.position).sqrMagnitude < _waypointReachedThresholdSqr)
            {
                if (_flightPath.Count > 0)
                    _currentWaypoint = _flightPath.Dequeue();
                else
                    _currentState = MissileState.Homing;
            }
        }

        private void HandleHoming()
        {
            if (!_finalTarget) return;

            var aimPos = _finalTarget.position;
            if (leadTargetMultiplier > 0 && _targetRb != null)
            {
                var dist = Vector3.Distance(_rb.position, aimPos);
                aimPos += _targetRb.linearVelocity * (dist / speed * leadTargetMultiplier);
            }

            FlyTowards(aimPos);
        }

        private void FlyTowards(Vector3 targetPos)
        {
            var dir = targetPos - _rb.position;
            if (dir == Vector3.zero) dir = transform.forward;

            var targetRot = Quaternion.LookRotation(dir);

            if (wobbleMagnitude > 0)
            {
                var time = Time.time * wobbleFrequency;
                var x = (Mathf.PerlinNoise(time, _perlinSeedX) - 0.5f) * wobbleMagnitude;
                var y = (Mathf.PerlinNoise(time, _perlinSeedY) - 0.5f) * wobbleMagnitude;
                targetRot = Quaternion.Euler(targetRot.eulerAngles + new Vector3(x, y, 0));
            }

            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRot, rotateSpeed * Time.fixedDeltaTime));
            _rb.linearVelocity = transform.forward * speed;
        }

        private bool TryFindNewTarget()
        {
            _timeSinceLastRetargetCheck += Time.fixedDeltaTime;
            if (_timeSinceLastRetargetCheck < retargetingCooldown) return false;
            _timeSinceLastRetargetCheck = 0f;

            var hitCount = Physics.OverlapSphereNonAlloc(_rb.position, retargetingRange, collidersCache, _enemyLayer);
            if (hitCount == 0) return false;

            Transform closest = null;
            var closestDist = float.MaxValue;
            var hits = collidersCache.AsSpan(0, hitCount);

            foreach (var hit in hits)
            {
                var d = (hit.transform.position - _rb.position).sqrMagnitude;
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = hit.transform;
                }
            }

            if (closest)
            {
                _finalTarget = closest;
                _finalTarget.TryGetComponent(out _targetRb);
                return true;
            }

            return false;
        }

        private enum MissileState
        {
            FollowingPath,
            Homing
        }
    }
}