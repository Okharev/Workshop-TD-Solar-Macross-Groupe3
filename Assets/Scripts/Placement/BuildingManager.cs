using System;
using Buildings;
using Economy;
using Towers;
using UnityEngine;

namespace Placement
{
    [DefaultExecutionOrder(-100)]
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance && Instance != this) Destroy(gameObject);
            else Instance = this;
        }

        public static BuildingEntity CreateBuilding(BuildingEntity data, Vector3 position, Quaternion rotation)
        {
            if (!CurrencyManager.Instance.TrySpend(data.cost))
            {
                Debug.Log("Pas assez de crédits !");
                return null;
            }

            var newObj = Instantiate(data.currentLevelPrefab, position, rotation);


            if (!newObj.TryGetComponent<BuildingEntity>(out var entity))
            {
                Debug.LogWarning($"Le prefab {data.name} n'a pas de composant BuildingEntity !");
                throw new Exception("oops");
            }


            return entity;
        }

        // Logique d'upgrade
        public void UpgradeBuilding(BuildingEntity oldEntity)
        {
            var currentData = oldEntity;
            var nextData = currentData.nextUpgrade;

            if (!nextData) return;

            if (!CurrencyManager.Instance.TrySpend(nextData.upgradeCost))
            {
                Debug.Log("Pas assez d'argent pour améliorer.");
                return;
            }

            // 1. Sauvegarder l'état (position, rotation, etc.)
            var pos = oldEntity.transform.position;
            var rot = oldEntity.transform.rotation;

            // 2. Détruire l'ancien
            Destroy(oldEntity.gameObject);

            // 3. Créer le nouveau
            SpawnEntity(nextData, pos, rot);

            // TODO: Jouer un effet de particules ici
            Debug.Log($"Amélioré en {nextData.displayName}");
        }

        public static void SellBuilding(BuildingEntity entity)
        {
            if (!entity) return;
            CurrencyManager.Instance.Gain(entity.RefundCost);
            Destroy(entity.gameObject);
        }

        // Méthode interne pour instancier proprement
        private BuildingEntity SpawnEntity(BuildingLevelSo data, Vector3 pos, Quaternion rot)
        {
            var newObj = Instantiate(data.currentLevelPrefab, pos, rot);

            // On s'assure que le composant est bien là et on l'initialise
            var entity = newObj.GetComponent<BuildingEntity>();


            return entity;
        }
    }
}