using System.Collections;
using UnityEngine;

namespace Economy
{
    public class EnergyDebugUI : MonoBehaviour
    {
        [Header("Settings")] 
        public bool showDebug = true;
        public KeyCode toggleKey = KeyCode.F3;
        public float verticalOffset = 2.5f;

        [Header("Update Frequency")]
        [Tooltip("How often to refresh the list of buildings (seconds)")]
        public float refreshRate = 0.5f;

        // Cache
        private EnergyProducer[] producers = new EnergyProducer[0];
        private EnergyConsumer[] consumers = new EnergyConsumer[0];
        private UnityEngine.Camera mainCam;
        private GUIStyle styleProducer;
        private GUIStyle styleConsumer;

        private void Awake()
        {
            mainCam = UnityEngine.Camera.main;
            StartCoroutine(CacheObjectsRoutine());
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) showDebug = !showDebug;
        }

        private IEnumerator CacheObjectsRoutine()
        {
            var wait = new WaitForSeconds(refreshRate);
            while (true)
            {
                if (showDebug)
                {
                    // FindObjectsByType automatically excludes inactive objects by default,
                    // but we still need the runtime checks in OnGUI for the time between refreshes.
                    producers = FindObjectsByType<EnergyProducer>(FindObjectsSortMode.None);
                    consumers = FindObjectsByType<EnergyConsumer>(FindObjectsSortMode.None);
                }
                yield return wait;
            }
        }

        private void OnGUI()
        {
            if (!showDebug || mainCam == null) return;

            if (styleProducer == null) SetupStyles();

            DrawProducers();
            DrawConsumers();
        }

        private void DrawProducers()
        {
            foreach (var p in producers)
            {
                // FIX: Check !isActiveAndEnabled to hide UI instantly when object is disabled
                // (waiting for the next Cache refresh takes too long)
                if (p == null || !p.isActiveAndEnabled) continue;

                Vector3 screenPos = GetScreenPosition(p.transform.position);
                if (screenPos.z < 0) continue; 

                int current = p.currentLoad; 
                int available = p.GetAvailableEnergy();
                int total = current + available;

                string text = p.isMobileGenerator ? "GENERATOR" : "DISTRICT";
                text += $"\nLoad: {current}/{total}";
            
                GUI.color = available == 0 ? Color.red : Color.cyan;

                DrawLabel(screenPos, text, styleProducer);
            }
        }

        private void DrawConsumers()
        {
            foreach (var c in consumers)
            {
                // FIX: Check !isActiveAndEnabled
                if (c == null || !c.isActiveAndEnabled) continue;

                Vector3 screenPos = GetScreenPosition(c.transform.position);
                if (screenPos.z < 0) continue;

                // FIX: Actually read the IsPowered property from the Consumer
                bool isPowered = c.IsPowered; 
                int req = c.GetEnergyRequirement();

                string text = isPowered ? "POWERED" : "NO POWER";
                text += $"\nReq: {req}";

                GUI.color = isPowered ? Color.green : Color.red;

                DrawLabel(screenPos, text, styleConsumer);
            }
        }

        private void DrawLabel(Vector3 screenPos, string text, GUIStyle style)
        {
            float width = 100f;
            float height = 50f;
            // Invert Y for GUI coordinates
            Rect rect = new Rect(screenPos.x - (width / 2), Screen.height - screenPos.y - (height / 2), width, height);

            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, text, style);
        }

        private Vector3 GetScreenPosition(Vector3 worldPos)
        {
            return mainCam.WorldToScreenPoint(worldPos + Vector3.up * verticalOffset);
        }

        private void SetupStyles()
        {
            styleProducer = new GUIStyle(GUI.skin.label);
            styleProducer.alignment = TextAnchor.MiddleCenter;
            styleProducer.fontStyle = FontStyle.Bold;
            styleProducer.fontSize = 12;

            styleConsumer = new GUIStyle(styleProducer);
        }
    }
}