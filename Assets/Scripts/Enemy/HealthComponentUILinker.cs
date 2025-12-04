using Game.UI;
using UnityEngine;

namespace Enemy
{
    [RequireComponent(typeof(HealthComponent))]
    public class HealthComponentUILinker : MonoBehaviour

    {
        private HealthComponent _health;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();  
        }

        private void OnEnable() // Appelé dès que le WaveManager spawn l'objet
        {
            if (EnemyHealthBarSystem.Instance)
            {
                EnemyHealthBarSystem.Instance.RegisterEnemy(this, _health);
            }
        }

        private void OnDisable() // Appelé quand l'ennemi meurt ou est recyclé
        {
            if (EnemyHealthBarSystem.Instance)
            {
                EnemyHealthBarSystem.Instance.UnregisterEnemy(this);
            }
        }
        
        private void OnDestroy() // Appelé quand l'ennemi meurt ou est recyclé
        {
            if (EnemyHealthBarSystem.Instance)
            {
                EnemyHealthBarSystem.Instance.UnregisterEnemy(this);
            }
        }
    }
}