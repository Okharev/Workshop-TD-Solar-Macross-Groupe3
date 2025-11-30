using System.Collections;
using UnityEngine;

namespace Economy
{
    public class EnergyDebugUI : MonoBehaviour
    {
        [Header("Settings")] public bool showDebug = true;

        public KeyCode toggleKey = KeyCode.F3;
        public float verticalOffset = 2.5f;

        [Header("Update Frequency")] [Tooltip("How often to refresh the list of buildings (seconds)")]
        public float refreshRate = 0.5f;

        private EnergyConsumer[] consumers = new EnergyConsumer[0];
        private UnityEngine.Camera mainCam;

        // Cache
        private EnergyProducer[] producers = new EnergyProducer[0];
        private GUIStyle styleConsumer;
        private GUIStyle styleProducer;

        private void Awake()
        {
            mainCam = UnityEngine.Camera.main;
            StartCoroutine(CacheObjectsRoutine());
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) showDebug = !showDebug;
        }

        private void OnGUI()
        {
            if (!showDebug || mainCam == null) return;

            if (styleProducer == null) SetupStyles();

            DrawProducers();
            DrawConsumers();
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

        private void DrawProducers()
        {
            foreach (var p in producers)
            {
                if (p == null || !p.isActiveAndEnabled) continue;

                var screenPos = GetScreenPosition(p.transform.position);
                if (screenPos.z < 0) continue;

                var current = p.currentLoad;
                var available = p.GetAvailableEnergy();
                var total = current + available;

                var text = p.isMobileGenerator ? "GENERATOR" : "DISTRICT";
                text += $"\nLoad: {current}/{total}";

                GUI.color = available == 0 ? Color.red : Color.cyan;

                DrawLabel(screenPos, text, styleProducer);
            }
        }

        private void DrawConsumers()
        {
            foreach (var c in consumers)
            {
                if (c == null || !c.isActiveAndEnabled) continue;

                var screenPos = GetScreenPosition(c.transform.position);
                if (screenPos.z < 0) continue;

                var isPowered = c.IsPowered;
                var req = c.GetEnergyRequirement();

                var text = isPowered ? "POWERED" : "NO POWER";
                text += $"\nReq: {req}";

                GUI.color = isPowered ? Color.green : Color.red;

                DrawLabel(screenPos, text, styleConsumer);
            }
        }

        private void DrawLabel(Vector3 screenPos, string text, GUIStyle style)
        {
            var width = 100f;
            var height = 50f;
            // Invert Y for GUI coordinates
            var rect = new Rect(screenPos.x - width / 2, Screen.height - screenPos.y - height / 2, width, height);

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