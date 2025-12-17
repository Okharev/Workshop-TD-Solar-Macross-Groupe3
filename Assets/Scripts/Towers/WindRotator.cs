using System.Collections.Generic;
using UnityEngine;

namespace Towers
{
    public class AdvancedWindRotator : MonoBehaviour
    {
        // --- AJOUT : Définition des axes possibles ---
        public enum Axis { X, Y, Z }

        [Header("Configuration de Rotation")]
        [Tooltip("Choisis l'axe de rotation ici (X, Y ou Z).")]
        public Axis rotationAxis = Axis.Y; // Par défaut sur Y comme avant
        // ---------------------------------------------

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

        private readonly List<RotorState> _rotorStates = new();
        private float _currentWindIntensity;

        private void Start()
        {
            // Initialisation automatique si la liste est vide dans l'inspecteur
            if (rotorsToRotate == null || rotorsToRotate.Count == 0)
            {
                rotorsToRotate = new List<Transform>();
                // Ajoute l'objet lui-même par défaut si on n'a rien spécifié
                rotorsToRotate.Add(transform);
            }

            // Création des états pour chaque rotor
            foreach (var t in rotorsToRotate)
            {
                if (!t) continue;

                var state = new RotorState
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

        private void Update()
        {
            // Optimisation: tu pourrais ne faire ça que toutes les X frames si nécessaire
            // Note: Assure-toi que GlobalWindManager existe dans ta scène, sinon commente cette ligne pour tester
            if (GlobalWindManager.Instance != null)
            {
                _currentWindIntensity = GlobalWindManager.Instance.GetWindAtPosition(transform.position);
            }
            else
            {
                _currentWindIntensity = 0.5f; // Valeur par défaut pour tester sans WindManager
            }

            // Calcul de la vitesse cible de base
            var baseTargetSpeed = Mathf.Lerp(minSpeed, maxSpeed, _currentWindIntensity);

            // 2. Mettre à jour chaque rotor individuellement
            foreach (var rotor in _rotorStates) UpdateRotor(rotor, baseTargetSpeed);
        }

        private void UpdateRotor(RotorState rotor, float baseTarget)
        {
            // Applique la variance unique du rotor à la cible
            var myTargetSpeed = baseTarget * rotor.efficiencyMultiplier;

            // Détermine si on accélère ou si on décélère pour choisir l'inertie
            var isAccelerating = myTargetSpeed > rotor.currentVelocity;
            var smoothTime = isAccelerating ? spinUpTime : spinDownTime;

            // Applique la variance d'inertie
            smoothTime *= rotor.inertiaMultiplier;

            // Mathf.MoveTowards pour une accélération linéaire mais fluide
            rotor.currentVelocity = Mathf.MoveTowards(
                rotor.currentVelocity,
                myTargetSpeed,
                maxSpeed / smoothTime * Time.deltaTime
            );

            // --- MODIFICATION : Sélection du vecteur de rotation ---
            Vector3 rotationVector = Vector3.up; // Valeur par défaut (Y)

            switch (rotationAxis)
            {
                case Axis.X:
                    rotationVector = Vector3.right; // L'axe X correspond à Vector3.right
                    break;
                case Axis.Y:
                    rotationVector = Vector3.up;    // L'axe Y correspond à Vector3.up
                    break;
                case Axis.Z:
                    rotationVector = Vector3.forward; // L'axe Z correspond à Vector3.forward
                    break;
            }

            // Rotation effective avec le vecteur choisi [cite: 1]
            rotor.transform.Rotate(rotationVector, rotor.currentVelocity * Time.deltaTime);
            // -------------------------------------------------------
        }

        // Petite classe interne pour stocker l'état individuel de chaque rotor
        private class RotorState
        {
            public float currentVelocity; 
            public float efficiencyMultiplier; 
            public float inertiaMultiplier; 
            public Transform transform;
        }
    }
}