using UnityEngine;
using SoulsRPG.Core;
using SoulsRPG.Player;

namespace SoulsRPG.Environment
{
    /// <summary>
    /// Ruh Lekesi: oyuncu öldüğü yerde taşıdığı ruhları bırakır. Tekrar ölmeden bu noktaya
    /// ulaşırsa ruhlarını geri alır; yolda yeniden ölürse bu ruhlar sonsuza dek kaybolur.
    /// </summary>
    public class Bloodstain : MonoBehaviour
    {
        [SerializeField] private float pickupRadius = 1.4f;
        [SerializeField] private Color glowColor = new Color(0.35f, 1.0f, 0.55f);

        private static Bloodstain current;

        public static Bloodstain Current => current;

        public static Bloodstain Spawn(Vector3 position)
        {
            Clear();

            // Yere oturt (ölüm noktası havadaysa)
            if (Physics.Raycast(position + Vector3.up * 1.0f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
            }

            var go = new GameObject("Bloodstain");
            go.transform.position = position;
            current = go.AddComponent<Bloodstain>();
            return current;
        }

        public static void Clear()
        {
            if (current != null)
            {
                Destroy(current.gameObject);
                current = null;
            }
        }

        private void Start()
        {
            BuildVisuals();
        }

        private void BuildVisuals()
        {
            // Parlayan küçük çekirdek
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "BloodstainCore";
            core.transform.SetParent(transform, false);
            core.transform.localPosition = Vector3.up * 0.15f;
            core.transform.localScale = Vector3.one * 0.18f;
            var col = core.GetComponent<Collider>();
            if (col != null) Destroy(col);
            core.GetComponent<Renderer>().material = RuntimeShaders.CreateMaterial(glowColor, RuntimeShaders.UnlitColor, RuntimeShaders.Standard);

            // Yeşil ışık
            var lightGo = new GameObject("BloodstainLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.4f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = glowColor;
            light.range = 3.0f;
            light.intensity = 1.6f;

            // Yukarı süzülen parçacıklar
            var psGo = new GameObject("BloodstainWisps");
            psGo.transform.SetParent(transform, false);
            psGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = psGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.4f;
            main.startSpeed = 0.6f;
            main.startSize = 0.07f;
            main.startColor = glowColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;
            RuntimeShaders.ApplyParticleMaterial(ps, Color.white);
            ps.Play();
        }

        private void Update()
        {
            var stats = PlayerStats.Instance;
            if (stats == null || stats.isDead) return;

            Vector3 delta = stats.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= pickupRadius * pickupRadius && Mathf.Abs(stats.transform.position.y - transform.position.y) < 2.5f)
            {
                int recovered = stats.RecoverBloodstain();
                if (recovered > 0)
                {
                    Audio.SoulsAudioManager.GetOrCreate().PlayBonfireLevelUp(transform.position);
                    GameLog.Info($"[Bloodstain] {recovered:N0} ruh geri alındı");
                }
                current = null;
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
