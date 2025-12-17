using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace UI
{
    public class MainMenu : MonoBehaviour
    {   
        private UIDocument _doc;
        private VisualElement _root;

        [Header("Configuration")]
        // Assure-toi que ce nom est EXACTEMENT le même que dans tes fichiers de scène
        [SerializeField] private string sceneToLoad = "LAYOUT"; 
        [SerializeField] private float fadeDuration = 1.0f;

        private VisualElement _fader;
        
        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            
            if (!_doc)
            {
                Debug.LogError("Menu Principal : Aucun UIDocument trouvé !");
                return;
            }

            _root = _doc.rootVisualElement;
            _fader = _root.Q<VisualElement>("Fader");
            
            // IMPORTANT : On s'assure que le Fader ne bloque pas la souris au début
            if (_fader != null)
            {
                _fader.pickingMode = PickingMode.Ignore;
                // On s'assure qu'il est bien transparent au début
                _fader.RemoveFromClassList("fader--active"); 
            }
            else
            {
                Debug.LogError("Menu Principal : L'élément 'Fader' n'a pas été trouvé dans le UXML.");
            }
            
            RegisterButtonCallbacks();
        }
        
        private void OnDisable()
        {
            UnRegisterButtonCallbacks();
        }
        
        private void RegisterButtonCallbacks()
        {
            // Utilisation de la méthode moderne (clicked +=) et vérification null (?)
            _root.Q<Button>("ButtonPlay")?.RegisterCallback<ClickEvent>(evt => PlayButtonClicked());
            _root.Q<Button>("ButtonOptions")?.RegisterCallback<ClickEvent>(evt => OptionsButtonClicked());
            _root.Q<Button>("ButtonCredits")?.RegisterCallback<ClickEvent>(evt => CreditsButtonClicked());
            _root.Q<Button>("ButtonZoo")?.RegisterCallback<ClickEvent>(evt => ZooButtonClicked());
            _root.Q<Button>("ButtonLeave")?.RegisterCallback<ClickEvent>(evt => LeaveButtonClicked());
        }

        private void UnRegisterButtonCallbacks()
        {
            // Pas strictement nécessaire de désinscrire les callbacks anonymes lambda ici 
            // car le VisualElement sera détruit au changement de scène, 
            // mais c'est une bonne habitude pour des UI plus complexes.
        }
        
        private void PlayButtonClicked()
        {
            Debug.Log("Play button clicked! Starting transition...");
            
            if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
            {
                Debug.LogError($"ERREUR : La scène '{sceneToLoad}' ne peut pas être chargée. Vérifie le Build Settings !");
                return;
            }

            StartCoroutine(LoadAsyncScene());
        }
        
        private void OptionsButtonClicked() { }
        private void CreditsButtonClicked() { }

        private void ZooButtonClicked()
        {
            SceneManager.LoadScene("Zoo");
        }

        private void LeaveButtonClicked()
        {
            Application.Quit();
        }

        IEnumerator LoadAsyncScene()
        {
            if (_fader != null)
            {
                // 1. On bloque les clics pour que le joueur ne clique pas 2 fois
                _fader.pickingMode = PickingMode.Position; 
                
                // 2. On lance l'animation visuelle
                _fader.AddToClassList("fader--active");
            }

            // 3. On lance le chargement
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneToLoad);
            asyncLoad.allowSceneActivation = false;

            // 4. On attend l'animation du fade
            yield return new WaitForSeconds(fadeDuration);

            // 5. On attend que la scène soit chargée à 90%
            while (asyncLoad.progress < 0.9f)
            {
                yield return null;
            }

            // 6. On active la scène
            asyncLoad.allowSceneActivation = true;
        }
    }
}