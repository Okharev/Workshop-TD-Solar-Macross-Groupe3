using Placement;
using UnityEngine;

namespace Enemy
{
    public class EnemyObjectiveTracker : MonoBehaviour
    {
        // La propriété réactive que Movement et Attacker écoutent
        public ReactiveProperty<Transform> CurrentTarget { get; private set; } = new ReactiveProperty<Transform>();

        [SerializeField] private DestructibleObjective _activeObjectiveScript; // Le script de la cible actuelle
        [SerializeField] private DestructibleObjective _backupObjective;       // La base principale (généralement)

        public void Initialize(DestructibleObjective primary, DestructibleObjective backup)
        {
            _backupObjective = backup;

            // On commence par cibler le primaire (Pylône ou Override)
            if (primary != null)
            {
                SetNewTarget(primary);
            }
            else if (backup != null)
            {
                SetNewTarget(backup);
            }
        }

        // Méthode centrale pour changer de cible proprement
        private void SetNewTarget(DestructibleObjective newTarget)
        {
            // 1. Nettoyage de l'ancienne cible (désabonnement)
            if (_activeObjectiveScript != null)
            {
                _activeObjectiveScript.OnDestroyed.RemoveListener(OnTargetDestroyed);
            }

            // 2. Assignation de la nouvelle cible
            _activeObjectiveScript = newTarget;

            if (_activeObjectiveScript != null)
            {
                // Mettre à jour la ReactiveProperty pour que Movement/Attacker réagissent
                CurrentTarget.Value = _activeObjectiveScript.transform;

                // S'abonner à l'événement de mort de CETTE cible spécifique
                _activeObjectiveScript.OnDestroyed.AddListener(OnTargetDestroyed);
            }
            else
            {
                CurrentTarget.Value = null;
            }
        }

        // Déclenché automatiquement quand la cible actuelle meurt
        private void OnTargetDestroyed()
        {
            // Si on visait déjà le backup et qu'il est mort, c'est fini (Game Over ?)
            if (_activeObjectiveScript == _backupObjective)
            {
                Debug.Log($"[Enemy] {name}: Ma cible finale est détruite. Victoire des ennemis ?");
                CurrentTarget.Value = null;
                return;
            }

            // Sinon, on passe au backup (la Base Principale)
            if (_backupObjective != null)
            {
                // Vérification de sécurité au cas où le backup serait déjà mort aussi
                if (_backupObjective.gameObject != null) 
                {
                    Debug.Log($"[Enemy] {name}: Cible détruite ! Redirection vers {_backupObjective.name}");
                    SetNewTarget(_backupObjective);
                }
            }
            else
            {
                FindAnyBackupTarget();
            }
        }

        private void FindAnyBackupTarget()
        {
            var potentialTarget = FindFirstObjectByType<DestructibleObjective>();
            if (potentialTarget)
            {
                SetNewTarget(potentialTarget);
            }
            else
            {
                Debug.Log($"[Enemy] {name}: Plus aucune cible sur la carte !");
                CurrentTarget.Value = null;
            }
        }

        private void OnDestroy()
        {
            // Nettoyage final pour éviter les erreurs de mémoire
            if (_activeObjectiveScript != null)
            {
                _activeObjectiveScript.OnDestroyed.RemoveListener(OnTargetDestroyed);
            }
        }
    }
}