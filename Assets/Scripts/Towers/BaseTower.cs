using System.Collections;
using System.Collections.Generic;
using Economy;
using UnityEngine;

namespace Towers
{
    public enum RotationMode
    {
        [Tooltip("Uses the exact speed set in the inspector. Fire Rate has no effect.")]
        Fixed,

        [Tooltip("Uses inspector speed as a base, but multiplies it by Fire Rate.")]
        ScaledWithStats,

        [Tooltip(
            "Ignores inspector speed. Calculates speed so the tower can perform a 180° turn within one cooldown cycle.")]
        SyncWithReload
    }

    [RequireComponent(typeof(EnergyConsumer), typeof(Collider))]
    public abstract class BaseTower : MonoBehaviour
    {
        [SerializeField] protected EnergyConsumer powerSource;
        [SerializeField] protected TowerBlueprintSo blueprint;

        public Stat damage;
        public Stat range;
        public Stat fireRate;

        [Header("Rotation")] [Tooltip("If true, rotation speed increases as Fire Rate increases.")] [SerializeField]
        protected bool scaleRotationWithFireRate = true;

        [SerializeField] public Transform yPivot;
        [SerializeField] public Transform xPivot;
        [SerializeField] public float yPivotSpeed;
        [SerializeField] public float xPivotSpeed;
        [SerializeField] public float rotationThreshold = 5.0f;
        [SerializeField] public Transform firePoint;
        [SerializeField] public List<UpgradeSo> upgrades = new();

        [Header("Targeting")] [SerializeField] public float targetingUpdateRate = 0.5f;

        [SerializeField] public LayerMask targetLayer;
        [SerializeField] public LayerMask visionBlockerLayer;

        [Header("Debug")] [SerializeField] public Transform currentTarget;

        [SerializeField] protected float fireCountdown;
        [SerializeField] protected bool isBusy;

        [Header("Rotation Logic")] [SerializeField]
        protected RotationMode rotationMode = RotationMode.ScaledWithStats;

        [Tooltip("Only used for 'SyncWithReload'. The angle the tower is expected to cover in one cooldown.")]
        [SerializeField]
        private float referenceTurnAngle = 120f;

        private List<IUpgradeInstance> activeUpgrades = new();

        [Header("Upgrades")] public UpgradeProvider events = new();


        protected virtual void Awake()
        {
            if (!powerSource) powerSource = GetComponent<EnergyConsumer>();

            // Default Layers if not set
            if (targetLayer == 0) targetLayer = LayerMask.GetMask("Enemy");
            if (visionBlockerLayer == 0) visionBlockerLayer = LayerMask.GetMask("Terrain", "PlacementBlockers");

            if (blueprint) ApplyBlueprintStats();
        }

        protected virtual void Start()
        {
            StartCoroutine(TargetUpdateLoop());
        }

        protected virtual void Update()
        {
            // 1. Economy Check
            if (!powerSource.IsPowered) return;

            // 2. Busy Check (e.g. Missile Salvo in progress)
            if (isBusy) return;

            // 3. Target Check
            if (!currentTarget) return;

            // 4. Aim & Fire
            var isAligned = AimAtTarget(currentTarget.position);

            fireCountdown -= Time.deltaTime;

            if (isAligned && fireCountdown <= 0f)
            {
                Fire();
                var rate = fireRate?.Value ?? 1f;
                fireCountdown = 1f / (rate <= 0 ? 0.1f : rate);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, range.Value);


            if (currentTarget) Gizmos.DrawWireSphere(currentTarget.transform.position, 1.0f);

            OnDrawGizmosTower();
        }

        protected float GetScaledRotationSpeed(float baseSpeed)
        {
            if (!scaleRotationWithFireRate || fireRate == null) return baseSpeed;

            return baseSpeed * Mathf.Max(1f, fireRate.Value);
        }

        protected float GetCurrentRotationSpeed(float baseInspectorSpeed)
        {
            var rate = fireRate?.Value ?? 1f;
            if (rate <= 0) rate = 0.1f;

            switch (rotationMode)
            {
                case RotationMode.Fixed:
                    return baseInspectorSpeed;

                case RotationMode.ScaledWithStats:
                    return baseInspectorSpeed * Mathf.Max(1f, rate);

                case RotationMode.SyncWithReload:

                    return referenceTurnAngle * rate;

                default:
                    return baseInspectorSpeed;
            }
        }

        protected IEnumerator TargetUpdateLoop()
        {
            var waiter = new WaitForSeconds(targetingUpdateRate);

            while (true)
            {
                if (powerSource.IsPowered && !isBusy) AcquireTarget();
                yield return waiter;
            }
        }


        protected virtual bool AimAtTarget(Vector3 aimPoint)
        {
            var currentYSpeed = GetCurrentRotationSpeed(yPivotSpeed);
            var currentXSpeed = GetCurrentRotationSpeed(xPivotSpeed);

            if (yPivot == xPivot)
            {
                if (!yPivot) return true;
                var direction = aimPoint - yPivot.position;
                if (direction.sqrMagnitude < 0.0001f) return true;
                var targetRotation = Quaternion.LookRotation(direction);
                yPivot.rotation =
                    Quaternion.RotateTowards(yPivot.rotation, targetRotation, currentYSpeed * Time.deltaTime);
                return Quaternion.Angle(yPivot.rotation, targetRotation) < rotationThreshold;
            }

            var yAligned = true;
            if (yPivot)
            {
                var horizontalDir = Vector3.ProjectOnPlane(aimPoint - yPivot.position, Vector3.up);

                if (horizontalDir.sqrMagnitude > 0.001f)
                {
                    var yTargetRot = Quaternion.LookRotation(horizontalDir);
                    yPivot.rotation =
                        Quaternion.RotateTowards(yPivot.rotation, yTargetRot, currentYSpeed * Time.deltaTime);
                    yAligned = Quaternion.Angle(yPivot.rotation, yTargetRot) < rotationThreshold;
                }
            }

            var xAligned = true;
            if (xPivot)
            {
                var dir = aimPoint - xPivot.position;
                if (dir.sqrMagnitude > 0.001f)
                {
                    var targetRot = Quaternion.LookRotation(dir, yPivot ? yPivot.up : Vector3.up);
                    var localTargetRot = Quaternion.Euler(targetRot.eulerAngles.x, 0f, 0f);
                    xPivot.localRotation =
                        Quaternion.RotateTowards(xPivot.localRotation, localTargetRot, currentXSpeed * Time.deltaTime);
                    xAligned = Quaternion.Angle(xPivot.localRotation, localTargetRot) < rotationThreshold;
                }
            }

            return yAligned && xAligned;
        }

        private void ApplyBlueprintStats()
        {
            if (!blueprint) return;
            damage = new Stat(blueprint.baseDamage);
            range = new Stat(blueprint.baseRange);
            fireRate = new Stat(blueprint.baseFireRate);
        }

        protected abstract void Fire();

        protected abstract void AcquireTarget();

        protected virtual void OnDrawGizmosTower()
        {
        }
    }
}