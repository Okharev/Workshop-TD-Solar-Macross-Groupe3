using System.Collections.Generic;
using Enemy;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Towers.ProjectileDerived
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HomingMissile : BaseProjectile
    {
        // Optimization: Static buffer to ensure shared memory across ALL missiles
        // This assumes BaseProjectile.collidersCache might not be static. 
        // If BaseProjectile.collidersCache IS static, you can use that instead.
        private static readonly Collider[] _sharedHitBuffer = new Collider[20];

        [Header("Ballistics & Speed")] [SerializeField]
        private float startSpeed = 15f;

        [SerializeField] private float maxSpeed = 45f;
        [SerializeField] private float acceleration = 25f;

        [Header("Maneuverability")] [SerializeField]
        private float rotateSpeed = 400.0f;

        [SerializeField] private float waypointReachedThreshold = 3.0f;
        [SerializeField] [Range(0f, 2f)] private float leadTargetMultiplier = 0.8f;

        [Header("Banking")] [SerializeField] private float bankingIntensity = 60f;

        [SerializeField] private float bankingLerpSpeed = 4f;

        [Header("Warhead")] [SerializeField] private float maxLifetime = 8f;

        [SerializeField] public float explosionRange;

        [Header("Re-targeting")] [SerializeField]
        private bool canRetarget = true;

        [SerializeField] private float retargetingRange = 40f;
        [SerializeField] private float retargetingCooldown = 0.25f; // Increased default slightly

        [Header("Aerodynamics")] [SerializeField]
        private float wobbleMagnitude = 1.5f;
        
        [SerializeField] private float wobbleFrequency = 8f;

        [SerializeField] public GameObject vfxImpact;
        
        private readonly Queue<Vector3> _flightPath = new();
        private float _currentSpeed;
        private MissileState _currentState;

        private Vector3 _currentWaypoint;
        private int _enemyLayer;

        private Transform _finalTarget;
        private Vector3 _lastKnownPosition;

        private float _lifetimeTimer;
        private float _perlinSeedX, _perlinSeedY;
        private Rigidbody _rb;

        private float _retargetTimer;
        private Rigidbody _targetRb;
        private Transform _transform; // Cached Transform
        private float _waypointReachedThresholdSqr;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _transform = transform; // Cache transform access

            _rb.useGravity = false;
            // Removed CollisionDetectionMode change (Setting this at runtime causes a small spike, better set in Prefab)
            _rb.linearDamping = 0;
            _rb.angularDamping = 0;

            _enemyLayer = LayerMask.GetMask("EnemyAir", "EnemyGround");
            _waypointReachedThresholdSqr = waypointReachedThreshold * waypointReachedThreshold;

            _perlinSeedX = Random.Range(0f, 100f);
            _perlinSeedY = Random.Range(0f, 100f);
        }

        // Consolidated Update logic into FixedUpdate to remove Update() overhead
        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;

            // 1. Lifetime Check
            _lifetimeTimer += dt;
            if (_lifetimeTimer > maxLifetime)
            {
                Destroy(gameObject); // Optimization: Pool this instead of Destroy
                return;
            }

            // 2. Speed
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSpeed, acceleration * dt);

            // 3. Target Memory
            // Optimization: Only access transform once per frame
            var isAlive = IsTargetAlive();
            if (isAlive) _lastKnownPosition = _finalTarget.position;

            // 4. State Machine
            if (_currentState == MissileState.FollowingPath)
                HandlePathFollowing(isAlive);
            else
                HandleHoming(isAlive, dt);
        }

        public void Setup(BaseTower tower)
        {
            source = tower;
        }

        public void Launch(IEnumerable<Vector3> pathPoints, Transform target)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _currentSpeed = startSpeed;

            _flightPath.Clear();
            if (pathPoints != null)
                foreach (var point in pathPoints)
                    _flightPath.Enqueue(point);

            _finalTarget = target;

            if (_finalTarget)
            {
                _finalTarget.TryGetComponent(out _targetRb);
                _lastKnownPosition = _finalTarget.position;
            }
            else
            {
                _lastKnownPosition = _transform.position + _transform.forward * 50f;
            }

            _lifetimeTimer = 0f;

            // OPTIMIZATION: Stagger the retarget check.
            // If 50 missiles spawn, they won't all SphereCast on the same frame later.
            _retargetTimer = Random.Range(0f, retargetingCooldown);

            if (_flightPath.Count > 0)
            {
                _currentState = MissileState.FollowingPath;
                _currentWaypoint = _flightPath.Dequeue();
                _transform.LookAt(_currentWaypoint);
            }
            else
            {
                _currentState = MissileState.Homing;
                if (_finalTarget) _transform.LookAt(_finalTarget);
            }
        }

        protected override bool IsValidHit(Collider hitObject)
        {
            return true;
        }

        protected override void HandleImpact(Collider other)
        {
            var vfx = Instantiate(vfxImpact, other.ClosestPointOnBounds(transform.position), Quaternion.identity);
            Destroy(vfx, 2.0f);

            
            // Use static buffer
            var hitCount =
                Physics.OverlapSphereNonAlloc(_transform.position, explosionRange, _sharedHitBuffer, _enemyLayer);

            var onHitData = new UpgradeProvider.OnHitData { Origin = gameObject, Target = gameObject };
            var onKillData = new UpgradeProvider.OnKillData { Origin = gameObject, Target = gameObject };

            // Optimization: iterate using standard for loop on array, avoids Span overhead (minor) 
            // but safer if not using unsafe code settings
            for (var i = 0; i < hitCount; i++)
            {
                var col = _sharedHitBuffer[i];
                if (!col.TryGetComponent<HealthComponent>(out var victim)) continue;

                source.Events.OnHit?.Invoke(onHitData);
                // Math.RoundToInt optimization
                if (victim.TakeDamage((int)(source.damage.Value + 0.5f))) source.Events.OnKill?.Invoke(onKillData);
            }

            Destroy(gameObject); // Replace with pooling
        }

        private void HandlePathFollowing(bool isTargetAlive)
        {
            MoveMissile((_currentWaypoint - _rb.position).normalized);

            if ((_currentWaypoint - _rb.position).sqrMagnitude < _waypointReachedThresholdSqr)
            {
                if (_flightPath.Count > 0)
                    _currentWaypoint = _flightPath.Dequeue();
                else
                    _currentState = MissileState.Homing;
            }
        }

        private void HandleHoming(bool isTargetAlive, float dt)
        {
            // 1. Live Target
            if (isTargetAlive)
            {
                var aimPos = _lastKnownPosition; // Use cached position

                if (leadTargetMultiplier > 0 && _targetRb)
                {
                    var distance = Vector3.Distance(_rb.position, aimPos);
                    // Optimization: Approx travel time to avoid division by zero
                    if (_currentSpeed > 0.1f)
                    {
                        var travelTime = distance / _currentSpeed;
                        aimPos += _targetRb.linearVelocity * (travelTime * leadTargetMultiplier);
                    }
                }

                MoveMissile((aimPos - _rb.position).normalized);
            }
            // 2. Dead Target -> Retarget
            else if (canRetarget && TryFindNewTarget(dt))
            {
                // New target found, fly there next frame
                // Current frame: keep flying forward or to last known
                MoveMissile((_lastKnownPosition - _rb.position).normalized);
            }
            // 3. Fallback (Dumb Fire)
            else
            {
                var distSqr = (_rb.position - _lastKnownPosition).sqrMagnitude;

                // If close to last known pos (approx 2.0f * 2.0f = 4.0f)
                if (distSqr < 4.0f)
                    MoveMissile(Vector3.down);
                else
                    MoveMissile((_lastKnownPosition - _rb.position).normalized);
            }
        }

        private void MoveMissile(Vector3 desiredDirection)
        {
            if (desiredDirection == Vector3.zero) desiredDirection = _transform.forward;

            var targetRotation = Quaternion.LookRotation(desiredDirection);

            // Wobble
            if (wobbleMagnitude > 0)
            {
                var time = Time.time * wobbleFrequency;
                // Optimization: Perlin is okay, but calculation is kept minimal
                var noiseX = (Mathf.PerlinNoise(time, _perlinSeedX) - 0.5f) * wobbleMagnitude;
                var noiseY = (Mathf.PerlinNoise(time, _perlinSeedY) - 0.5f) * wobbleMagnitude;

                // Rotate vector logic is expensive, adding Euler is cheaper here
                var currentEuler = targetRotation.eulerAngles;
                targetRotation = Quaternion.Euler(currentEuler.x + noiseX, currentEuler.y + noiseY, currentEuler.z);
            }

            // Banking
            var localTargetDir = _transform.InverseTransformDirection(desiredDirection);
            var targetBankAngle = -localTargetDir.x * bankingIntensity;

            // Use local variable for euler access
            var rbEuler = _rb.rotation.eulerAngles;
            var currentBank = Mathf.LerpAngle(rbEuler.z, targetBankAngle, Time.fixedDeltaTime * bankingLerpSpeed);

            // Apply Rotation
            var newRotation = Quaternion.RotateTowards(_rb.rotation, targetRotation, rotateSpeed * Time.fixedDeltaTime);

            // Overwrite Roll
            var finalEuler = newRotation.eulerAngles;
            finalEuler.z = currentBank;

            _rb.MoveRotation(Quaternion.Euler(finalEuler));
            _rb.linearVelocity = _transform.forward * _currentSpeed;
        }

        private bool IsTargetAlive()
        {
            // Optimization: Simplified null check (Unity overrides ==)
            return _finalTarget && _finalTarget.gameObject.activeInHierarchy;
        }

        private bool TryFindNewTarget(float dt)
        {
            _retargetTimer += dt;
            if (_retargetTimer < retargetingCooldown) return false;

            // Reset timer with small variance to prevent re-syncing
            _retargetTimer = Random.Range(0f, 0.05f);

            var hitCount = Physics.OverlapSphereNonAlloc(_rb.position, retargetingRange, _sharedHitBuffer, _enemyLayer);
            if (hitCount == 0) return false;

            Transform closest = null;
            var closestDistSqr = float.MaxValue; // Use Sqr for comparison

            for (var i = 0; i < hitCount; i++)
            {
                var hit = _sharedHitBuffer[i];
                if (!hit.gameObject.activeInHierarchy) continue;

                var d = (hit.transform.position - _rb.position).sqrMagnitude;
                if (d < closestDistSqr)
                {
                    closestDistSqr = d;
                    closest = hit.transform;
                }
            }

            if (closest)
            {
                _finalTarget = closest;
                _finalTarget.TryGetComponent(out _targetRb);
                _lastKnownPosition = _finalTarget.position;
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