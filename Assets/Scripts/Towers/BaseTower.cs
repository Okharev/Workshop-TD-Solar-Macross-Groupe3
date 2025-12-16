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

        [Tooltip("Ignores inspector speed. Calculates speed so the tower can perform a 180° turn within one cooldown cycle.")]
        SyncWithReload
    }

    [RequireComponent(typeof(Collider), typeof(GrassOccluder), typeof(TowerRangeVisualizer))]
    [RequireComponent(typeof(TowerLevelingManager))]
    public abstract class BaseTower : BuildingEntity
    {
        [Header("--- Base Tower Settings ---")]
        [SerializeField] protected EnergyConsumer powerSource;

        [Header("Combat Stats")]
        [SerializeField] public int baseDamage;
        [SerializeField] public float baseRange;
        [SerializeField] public float baseFireRate;

        // Stats modifiables (par les upgrades)
        [SerializeField] public Stat damage;
        [SerializeField] public Stat range;
        [SerializeField] public Stat fireRate;

        [Header("Rotation Logic")] 
        [Tooltip("If true, rotation speed increases as Fire Rate increases.")] 
        [SerializeField] protected bool scaleRotationWithFireRate = true;

        [SerializeField] protected RotationMode rotationMode = RotationMode.ScaledWithStats;
        
        [Tooltip("Only used for 'SyncWithReload'. The angle the tower is expected to cover in one cooldown.")]
        [SerializeField] private float referenceTurnAngle = 120f;

        [SerializeField] public Transform yPivot;
        [SerializeField] public Transform xPivot;
        [SerializeField] public float yPivotSpeed;
        [SerializeField] public float xPivotSpeed;
        [SerializeField] public float rotationThreshold = 5.0f;

        [Header("Firing Setup")]
        [SerializeField] public Transform firePoint;
        
        [Tooltip("VFX qui apparait au bout du canon lors du tir")]
        [SerializeField] protected GameObject muzzleFlashVFX;
        
        [Tooltip("VFX qui apparait sur l'ennemi touché (pour les armes Hitscan/Raycast uniquement)")]
        [SerializeField] protected GameObject impactVFX;

        [Header("Targeting")] 
        [SerializeField] public float targetingUpdateRate = 0.5f;
        [SerializeField] public LayerMask targetLayer;
        [SerializeField] public LayerMask visionBlockerLayer;
        [SerializeField] public Transform currentTarget;

        [Header("State")]
        [SerializeField] protected float fireCountdown;
        [SerializeField] protected bool isBusy;

        [Header("Visuals & UI")] 
        [Tooltip("Prefab contenant une sphère avec le Shader de portée")] 
        [SerializeField] private GameObject rangeIndicatorPrefab;

        // --- Systèmes Internes ---
        [SerializeField] private TowerLevelingManager _levelingManager;
        [SerializeField] public List<UpgradeSo> upgrades = new();
        [SerializeReference] private List<IUpgradeInstance> _activeUpgrades = new();
        
        // Système d'événements pour les upgrades
        public readonly UpgradeProvider Events = new();

        private GameObject _activeRangeIndicator;

        protected override void Awake()
        {
            base.Awake(); // Initialise _totalInvested et autres logiques de BuildingEntity

            _levelingManager = GetComponent<TowerLevelingManager>();
            powerSource = GetComponent<EnergyConsumer>();

            // Initialise la consommation d'énergie basée sur la config
            if (powerSource)
                powerSource.totalRequirement.BaseValue = energyDrain;

            // Valeurs par défaut pour les masques
            if (targetLayer == 0) targetLayer = LayerMask.GetMask("EnemyGround");
            if (visionBlockerLayer == 0) visionBlockerLayer = LayerMask.GetMask("Terrain", "PhysicalBlocker");

            ApplyBlueprintStats();
        }

        protected virtual void Start()
        {
            // Note: base.Start() appelle PlayVFX(buildVFX) dans BuildingEntity
            base.Start(); 
            StartCoroutine(TargetUpdateLoop());
        }

        protected virtual void Update()
        {
            // 1. Economy Check (Pas d'énergie = Pas de tir)
            if (powerSource && !powerSource.IsPowered) return;

            // 2. Busy Check (Ex: Une animation spéciale est en cours)
            if (isBusy) return;

            // 3. Target Check
            if (!currentTarget) return;

            // 4. Aim & Fire
            // On tourne vers la cible
            var isAligned = AimAtTarget(currentTarget.position);

            fireCountdown -= Time.deltaTime;

            // Si on est aligné et que le cooldown est fini
            if (isAligned && fireCountdown <= 0f)
            {
                // La méthode Fire() doit être implémentée par les enfants (Canon, Laser, etc.)
                // C'est à l'enfant d'appeler PlayShootVFX() à l'intérieur de Fire()
                Fire();
                
                // Reset du cooldown (1 / FireRate)
                fireCountdown = 1f / (fireRate.Value <= 0 ? 0.1f : fireRate.Value);
            }
        }

        // --- GESTION DES VFX ---

        /// <summary>
        /// A appeler dans l'override de Fire() pour jouer l'effet de tir au bout du canon.
        /// </summary>
        protected void PlayShootVFX()
        {
            if (muzzleFlashVFX && firePoint)
            {
                // Utilise la méthode PlayVFX de BuildingEntity
                PlayVFX(muzzleFlashVFX, firePoint.position, firePoint.rotation);
            }
        }

        /// <summary>
        /// A appeler quand vous touchez une cible (ex: Raycast hit) pour jouer l'effet d'impact.
        /// </summary>
        public void SpawnImpactVFX(Vector3 position, Vector3 normal)
        {
            if (impactVFX)
            {
                Quaternion rot = Quaternion.LookRotation(normal);
                PlayVFX(impactVFX, position, rot);
            }
        }

        // --- LOGIQUE DE CIBLAGE ET ROTATION ---

        protected IEnumerator TargetUpdateLoop()
        {
            var waiter = new WaitForSeconds(targetingUpdateRate);
            while (true)
            {
                // On ne cherche une cible que si on a du courant
                if (powerSource && powerSource.IsPowered && !isBusy) AcquireTarget();
                yield return waiter;
            }
        }

        protected virtual bool AimAtTarget(Vector3 aimPoint)
        {
            var currentYSpeed = GetCurrentRotationSpeed(yPivotSpeed);
            var currentXSpeed = GetCurrentRotationSpeed(xPivotSpeed);

            // Cas où un seul objet gère X et Y (ex: une tourelle boule)
            if (yPivot == xPivot)
            {
                if (!yPivot) return true;
                var direction = aimPoint - yPivot.position;
                if (direction.sqrMagnitude < 0.0001f) return true;
                
                var targetRotation = Quaternion.LookRotation(direction);
                yPivot.rotation = Quaternion.RotateTowards(yPivot.rotation, targetRotation, currentYSpeed * Time.deltaTime);
                
                return Quaternion.Angle(yPivot.rotation, targetRotation) < rotationThreshold;
            }

            // Gestion de l'axe Y (Rotation horizontale)
            var yAligned = true;
            if (yPivot)
            {
                var horizontalDir = Vector3.ProjectOnPlane(aimPoint - yPivot.position, Vector3.up);

                if (horizontalDir.sqrMagnitude > 0.001f)
                {
                    var yTargetRot = Quaternion.LookRotation(horizontalDir);
                    yPivot.rotation = Quaternion.RotateTowards(yPivot.rotation, yTargetRot, currentYSpeed * Time.deltaTime);
                    yAligned = Quaternion.Angle(yPivot.rotation, yTargetRot) < rotationThreshold;
                }
            }

            // Gestion de l'axe X (Rotation verticale / Elevation)
            var xAligned = true;
            if (xPivot)
            {
                var dir = aimPoint - xPivot.position;
                if (dir.sqrMagnitude > 0.001f)
                {
                    // On calcule la rotation locale pour l'élévation
                    var targetRot = Quaternion.LookRotation(dir, yPivot ? yPivot.up : Vector3.up);
                    var localTargetRot = Quaternion.Euler(targetRot.eulerAngles.x, 0f, 0f);
                    
                    xPivot.localRotation = Quaternion.RotateTowards(xPivot.localRotation, localTargetRot, currentXSpeed * Time.deltaTime);
                    xAligned = Quaternion.Angle(xPivot.localRotation, localTargetRot) < rotationThreshold;
                }
            }

            return yAligned && xAligned;
        }

        // --- CALCULS DE STATS ---

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

        private void ApplyBlueprintStats()
        {
            damage = new Stat(baseDamage);
            range = new Stat(baseRange);
            fireRate = new Stat(baseFireRate);

            damage.Initialize();
            range.Initialize();
            fireRate.Initialize();
        }

        // --- INTERACTION & SELECTION ---

        public override void OnSelect()
        {
            base.OnSelect(); // Appelle BuildingEntity (Heatmap, etc.)
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

            if (_activeRangeIndicator == null)
                _activeRangeIndicator = Instantiate(rangeIndicatorPrefab, transform.position, Quaternion.identity, transform);

            _activeRangeIndicator.SetActive(true);
            UpdateRangeVisualScale();
        }

        private void HideRangeIndicator()
        {
            if (_activeRangeIndicator != null) _activeRangeIndicator.SetActive(false);
        }

        public void UpdateRangeVisualScale()
        {
            if (_activeRangeIndicator == null) return;

            // Scale = Radius * 2
            var diameter = range.Value * 2.0f;
            _activeRangeIndicator.transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        protected void OnStatsChanged()
        {
            // Met à jour le visuel si la portée a changé via une upgrade
            if (_activeRangeIndicator && _activeRangeIndicator.activeSelf) UpdateRangeVisualScale();
        }

        // --- UPGRADES ---

        public void RegisterUpgrade(IUpgradeInstance instance, UpgradeSo sourceDefinition)
        {
            _activeUpgrades.Add(instance);
            if (!upgrades.Contains(sourceDefinition)) upgrades.Add(sourceDefinition);
            OnStatsChanged();
        }

        public override List<InteractionAction> GetInteractions()
        {
            // Récupère Vendre/Upgrade de base
            var actions = base.GetInteractions();

            // Ajoute les interactions spécifiques au leveling manager
            if (_levelingManager != null) actions.AddRange(_levelingManager.GetUpgradeInteractions());

            return actions;
        }

        // --- ABSTRACTS & DEBUG ---

        protected abstract void Fire();
        protected abstract void AcquireTarget();

        protected virtual void OnDrawGizmosTower() { }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            if (range != null) Gizmos.DrawSphere(transform.position, range.Value);
            else Gizmos.DrawSphere(transform.position, baseRange);

            Gizmos.color = Color.green;
            if (currentTarget) Gizmos.DrawWireSphere(currentTarget.transform.position, 1.0f);

            OnDrawGizmosTower();
        }
    }
}