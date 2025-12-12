using System.Collections.Generic;
using UnityEngine;

namespace Towers
{

public class AdvancedWindRotator : MonoBehaviour
    {
        [Header("Configuration Générale")]
        // Si vide, on cherchera les enfants. Utile pour les turbines multi-rotors.
        public List<Transform> rotorsToRotate; 
        
        [Header("Paramètres de Vitesse")]
        public float minSpeed = 20f;
        public float maxSpeed = 300f;
        
        [Header("Physique (Inertie)")]
        [Tooltip("Temps approximatif pour atteindre la vitesse cible. Plus c'est haut, plus c'est lourd.")]
        public float spinUpTime = 2.0f; // Temps pour accélérer
        public float spinDownTime = 4.0f; // Temps pour ralentir (souvent plus long)

        [Header("Variance (Game Feel)")]
        [Range(0f, 0.5f)]
        [Tooltip("Pourcentage de variation entre les rotors (0 = synchro parfaite, 0.5 = très chaotique)")]
        public float randomnessFactor = 0.15f; 

        // Petite classe interne pour stocker l'état individuel de chaque rotor
        private class RotorState
        {
            public Transform transform;
            public float currentVelocity; // Vitesse actuelle de ce rotor spécifique
            public float efficiencyMultiplier; // Facteur aléatoire de vitesse (ex: 0.9 à 1.1)
            public float inertiaMultiplier; // Facteur aléatoire d'inertie
        }

        private List<RotorState> _rotorStates = new List<RotorState>();
        private float _currentWindIntensity = 0f;

        void Start()
        {
            // Initialisation automatique si la liste est vide dans l'inspecteur
            if (rotorsToRotate == null || rotorsToRotate.Count == 0)
            {
                rotorsToRotate = new List<Transform>();
                // Ajoute l'objet lui-même par défaut si on n'a rien spécifié
                rotorsToRotate.Add(this.transform); 
            }

            // Création des états pour chaque rotor
            foreach (var t in rotorsToRotate)
            {
                if (!t) continue;

                RotorState state = new RotorState
                {
                    transform = t,
                    currentVelocity = 0f,
                    // On génère des valeurs aléatoires uniques pour chaque rotor
                    efficiencyMultiplier = Random.Range(1f - randomnessFactor, 1f + randomnessFactor),
                    inertiaMultiplier = Random.Range(1f - randomnessFactor, 1f + randomnessFactor)
                };
                _rotorStates.Add(state);
            }
        }

        void Update()
        {

           // Optimisation: tu pourrais ne faire ça que toutes les X frames si nécessaire
           _currentWindIntensity = GlobalWindManager.Instance.GetWindAtPosition(transform.position);
      
            // Calcul de la vitesse cible de base
            float baseTargetSpeed = Mathf.Lerp(minSpeed, maxSpeed, _currentWindIntensity);

            // 2. Mettre à jour chaque rotor individuellement
            foreach (var rotor in _rotorStates)
            {
                UpdateRotor(rotor, baseTargetSpeed);
            }
        }

        private void UpdateRotor(RotorState rotor, float baseTarget)
        {
            // Applique la variance unique du rotor à la cible
            float myTargetSpeed = baseTarget * rotor.efficiencyMultiplier;

            // Détermine si on accélère ou si on décélère pour choisir l'inertie
            bool isAccelerating = myTargetSpeed > rotor.currentVelocity;
            float smoothTime = isAccelerating ? spinUpTime : spinDownTime;

            // Applique la variance d'inertie
            smoothTime *= rotor.inertiaMultiplier;

            // Mathf.SmoothDamp est EXCELLENT pour l'inertie physique
            float velocityRef = 0f; // Variable technique requise par SmoothDamp, non utilisée ici pour l'état
            
            // Note: Pour une rotation simple, on peut utiliser MoveTowards ou Lerp, 
            // mais SmoothDamp donne cet effet "élastique" et organique.
            // Ici, j'utilise une version simplifiée similaire à un Lerp frame-rate independent pour la vitesse :
            
            rotor.currentVelocity = Mathf.MoveTowards(
                rotor.currentVelocity, 
                myTargetSpeed, 
                (maxSpeed / smoothTime) * Time.deltaTime
            );

            // Rotation effective
            rotor.transform.Rotate(Vector3.up, rotor.currentVelocity * Time.deltaTime);
        }
    }
}