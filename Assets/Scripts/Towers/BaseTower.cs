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

    [RequireComponent(typeof(Collider), typeof(GrassOccluder), typeof(TowerRangeVisualizer))]
    [RequireComponent(typeof(TowerLevelingManager))]
    public abstract class BaseTower : BuildingEntity
    {
        [SerializeField] protected EnergyConsumer powerSource;

        [SerializeField] public int baseDamage;
        [SerializeField] public float baseRange;
        [SerializeField] public float baseFireRate;

        [SerializeField] public Stat damage;
        [SerializeField] public Stat range;
        [SerializeField] public Stat fireRate;

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

        [Header("Visuals")] [Tooltip("Prefab contenant une sphère avec le Shader de portée")] [SerializeField]
        private GameObject rangeIndicatorPrefab;

        [SerializeField] private TowerLevelingManager _levelingManager;

        [Header("Rotation Logic")] [SerializeField]
        protected RotationMode rotationMode = RotationMode.ScaledWithStats;

        [Tooltip("Only used for 'SyncWithReload'. The angle the tower is expected to cover in one cooldown.")]
        [SerializeField]
        private float referenceTurnAngle = 120f;

        [SerializeReference] private List<IUpgradeInstance> _activeUpgrades = new();

        [Header("Upgrades")] public readonly UpgradeProvider Events = new();

        private GameObject _activeRangeIndicator;

        protected override void Awake()
        {
            base.Awake();

            _levelingManager = GetComponent<TowerLevelingManager>();

            powerSource = GetComponent<EnergyConsumer>();

            powerSource.totalRequirement.BaseValue = energyDrain;


            if (targetLayer == 0) targetLayer = LayerMask.GetMask("EnemyGround");
            if (visionBlockerLayer == 0) visionBlockerLayer = LayerMask.GetMask("Terrain", "PhysicalBlocker");

            ApplyBlueprintStats();
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
                fireCountdown = 1f / (fireRate.Value <= 0 ? 0.1f : fireRate.Value);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawSphere(transform.position, range.Value);

            Gizmos.color = Color.green;
            if (currentTarget) Gizmos.DrawWireSphere(currentTarget.transform.position, 1.0f);

            OnDrawGizmosTower();
        }


        protected float GetScaledRotationSpeed(float baseSpeed)
        {
            if (!scaleRotationWithFireRate || fireRate == null) return baseSpeed;

            return baseSpeed * Mathf.Max(1f, fireRate.Value);
        }

        private float GetCurrentRotationSpeed(float baseInspectorSpeed)
        {
            var ratee = fireRate.Value;
            if (ratee <= 0) ratee = 0.1f;

            return rotationMode switch
            {
                RotationMode.Fixed => baseInspectorSpeed,
                RotationMode.ScaledWithStats => baseInspectorSpeed * Mathf.Max(1f, ratee),
                RotationMode.SyncWithReload => referenceTurnAngle * ratee,
                _ => baseInspectorSpeed
            };
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

        public override void OnSelect()
        {
            base.OnSelect(); // Appelle le code de BuildingEntity (sons, shader de sélection)

            ShowRangeIndicator();
        }

        public override void OnDeselect()
        {
            base.OnDeselect();

            HideRangeIndicator();
        }

        private void ShowRangeIndicator()
        {
            if (rangeIndicatorPrefab == null) return;

            // Si l'indicateur n'existe pas encore, on le crée
            if (_activeRangeIndicator == null)
                _activeRangeIndicator =
                    Instantiate(rangeIndicatorPrefab, transform.position, Quaternion.identity, transform);

            _activeRangeIndicator.SetActive(true);
            UpdateRangeVisualScale();
        }

        private void HideRangeIndicator()
        {
            if (_activeRangeIndicator != null) _activeRangeIndicator.SetActive(false);
        }

        /// <summary>
        ///     Met à jour la taille de la sphère visuelle en fonction de la portée actuelle.
        /// </summary>
        public void UpdateRangeVisualScale()
        {
            if (_activeRangeIndicator == null) return;

            // La portée est un rayon (radius).
            // La sphère primitive d'Unity a un diamètre de 1 unité par défaut.
            // Donc Scale = Radius * 2.
            var diameter = range.Value * 2.0f;
            _activeRangeIndicator.transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        // Si tes stats changent pendant le jeu (ex: upgrade), appelle cette méthode
        protected void OnStatsChanged()
        {
            // Logique pour recalculer les stats...
            if (_activeRangeIndicator && _activeRangeIndicator.activeSelf) UpdateRangeVisualScale();
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
            damage = new Stat(baseDamage);
            range = new Stat(baseRange);
            fireRate = new Stat(baseFireRate);

            damage.Initialize();
            range.Initialize();
            fireRate.Initialize();
        }

        public void RegisterUpgrade(IUpgradeInstance instance, UpgradeSo sourceDefinition)
        {
            // 1. On ajoute l'instance logique à la liste privée (pour le fonctionnement interne)
            _activeUpgrades.Add(instance);

            // 2. On ajoute la définition à la liste publique (pour que tu le voies dans l'Inspecteur Unity)
            if (!upgrades.Contains(sourceDefinition)) upgrades.Add(sourceDefinition);

            // 3. Optionnel : Si l'upgrade modifie des stats, on peut déclencher un recalcul
            OnStatsChanged();
        }

        public override List<InteractionAction> GetInteractions()
        {
            // 1. Récupère les interactions de base (Vendre)
            var actions = base.GetInteractions();

            // 2. Si on a un manager d'upgrade, on ajoute ses options
            if (_levelingManager != null) actions.AddRange(_levelingManager.GetUpgradeInteractions());

            // Note: Si tu veux désactiver le système "nextUpgrade" de BuildingEntity
            // assure-toi que le champ 'nextUpgrade' est vide dans l'inspecteur Unity,
            // ou filtre la liste 'actions' ici.

            return actions;
        }

        protected abstract void Fire();

        protected abstract void AcquireTarget();

        protected virtual void OnDrawGizmosTower()
        {
        }
    }
}