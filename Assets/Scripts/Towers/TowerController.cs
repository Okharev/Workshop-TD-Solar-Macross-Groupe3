using System.Collections.Generic;
using Economy;
using Towers.Architecture.Strategies;
using Towers.TargetingStrategies;
using UnityEngine;

namespace Towers
{
    [SelectionBase]
    public class TowerEntity : MonoBehaviour
    {
        #region --- Configuration ---

        [SerializeField] private TowerBlueprintSo blueprint;

        [SerializeReference] public ITargetingBehaviours targetingStrategy;

        [SerializeReference] public IRotationStrategy rotationStrategy;

        [SerializeReference] public IWeaponStrategy weaponStrategy;

        #endregion

        #region --- Runtime Stats ---

        public Stat damage = new(10f);
        public Stat range = new(15f);
        public Stat fireRate = new(1f);

        #endregion

        #region --- Visuals & References ---

        public Transform firePoint;

        [Header("Pivots (For Rotation)")] public Transform yPivot;

        public Transform xPivot;

        [Header("Feedback")] [SerializeField] private AudioSource audioSource;

        [SerializeField] private ParticleSystem muzzleFlash;

        #endregion

        #region --- State & Events ---

        private readonly bool _hasPower = true; // Default to true so it works if no power system exists
        private IEnergyConsumer _powerSource;

        public readonly UpgradeProvider events = new();

        public Transform currentTarget;
        public Vector3 aimPoint;

        public float FireTimer { get; set; }

        public bool isAligned;

        // Cached Layers
        public LayerMask EnemyLayer { get; private set; }

        // Active Upgrade Logic
        private readonly List<IUpgradeInstance> _activeUpgrades = new();

        #endregion

        #region --- Lifecycle ---

        private void Awake()
        {
            EnemyLayer = LayerMask.GetMask("Enemy");

            targetingStrategy = new TargetClosest();
            rotationStrategy = new RotationDualAxis(90f, 90f, 5f);
            weaponStrategy = new WeaponShotgunRaycast();

            InitializeStats();
        }

        private void Start()
        {
            _powerSource = GetComponent<IEnergyConsumer>();

            if (_powerSource != null)
            {
                // 2. SUBSCRIBE to the event
                _powerSource.OnPowerChanged += HandlePowerChanged;

                // 3. SYNC initial state (in case we spawned late)
                HandlePowerChanged(_powerSource.IsPowered);
            }

            InitializeUpgrades();
        }

        private void HandlePowerChanged(bool hasPower)
        {
            enabled = hasPower; // Ou une variable privée _hasPower
            // Debug.Log($"Tower Power: {hasPower}");
        }


        private void OnDisable()
        {
            if (_powerSource != null) _powerSource.OnPowerChanged -= HandlePowerChanged;

            // CLEANUP: Essential to stop Coroutines and prevent Memory Leaks

            if (targetingStrategy != null)
            {
                targetingStrategy.OnTargetAcquired -= HandleTargetAcquired;
                targetingStrategy.OnTargetLost -= HandleTargetLost;
                targetingStrategy.Dispose(this);
            }

            rotationStrategy?.Dispose(this);

            if (weaponStrategy != null)
            {
                weaponStrategy.OnFired -= HandleFired;
                weaponStrategy.Dispose(this);
            }

            // Disable upgrades (Unsubscribes from events)
            foreach (var upg in _activeUpgrades) upg.Disable();
        }


        private void OnEnable()
        {
            // 1. Initialize Strategies
            // Passing 'this' allows them to start Coroutines or read Stats
            if (targetingStrategy != null)
            {
                targetingStrategy.Initialize(this);
                targetingStrategy.OnTargetAcquired += HandleTargetAcquired;
                targetingStrategy.OnTargetLost += HandleTargetLost;
            }

            rotationStrategy?.Initialize(this);
            // Optional: Hook into rotation events
            if (weaponStrategy != null)
            {
                weaponStrategy.Initialize(this);
                weaponStrategy.OnFired += HandleFired;
            }
        }

        private void Update()
        {
            // --- THE GATEKEEPER ---
            // If we have no power, we do nothing.
            if (!_hasPower) return;

            var dt = Time.deltaTime;

            // 1. Update Rotation
            rotationStrategy?.UpdateRotation(this, dt);

            // 2. Update Weapon
            weaponStrategy?.UpdateWeapon(this, dt);
        }

        #endregion

        #region --- Initialization Logic ---

        // Called automatically by Odin via [OnValueChanged] or in Awake
        public void InitializeStats()
        {
            if (!blueprint) return;

            // Reset stats to base values from SO
            damage = new Stat(blueprint.baseDamage);
            range = new Stat(blueprint.baseRange);
            fireRate = new Stat(blueprint.baseFireRate);

            // Note: If you have existing modifiers, you might want to Reapply them here
        }

        private void InitializeUpgrades()
        {
            if (!blueprint) return;

            // clear old list if pooling
            _activeUpgrades.Clear();
        }

        public void AddUpgrade(UpgradeSo upgradeSo)
        {
            if (!upgradeSo) return;

            // Factory Pattern: Create the runtime logic for this specific upgrade
            var instance = upgradeSo.CreateInstance(this);

            // This usually subscribes to 'events.onHit', 'events.onKill', etc.
            instance.Enable();

            _activeUpgrades.Add(instance);
        }

        #endregion

        #region --- Event Handlers (Feedback) ---

        private void HandleTargetAcquired(Transform target)
        {
            // Example: Play a generic "Target Lock" sound or animation
            // Debug.Log($"Acquired: {target.name}");
        }

        private void HandleTargetLost()
        {
            // Reset aim point or animation
        }

        private void HandleFired()
        {
            // Visual/Audio Feedback
            if (muzzleFlash) muzzleFlash.Play();
            if (audioSource) audioSource.Play();

            // NOTE: The actual gameplay logic (dealing damage) is handled inside 
            // the WeaponStrategy (Fire method) or the Projectile itself.
        }

        #endregion

        #region --- Debug ---

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            // Draw range based on current Stat value (includes upgrades)
            var r = range?.Value ?? 10f;
            Gizmos.DrawWireSphere(transform.position, r);

            if (currentTarget)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(firePoint ? firePoint.position : transform.position, currentTarget.position);
            }
            else if (aimPoint != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(aimPoint, 0.5f);
            }
        }

        private void OnDrawGizmos()
        {
            if (firePoint)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(firePoint.position, firePoint.forward * 3f);

                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(firePoint.position, 0.1f);
            }
        }

        #endregion
    }
}