using System.Collections.Generic;
using UnityEngine;

namespace SoulsRPG.Boss
{
    /// <summary>
    /// Boss 2. Faza geçtiğinde (Phase 2) silah ve gövdede beliren alev/gazap görsel efektlerini yönetir.
    /// </summary>
    public class BossPhaseAura : MonoBehaviour
    {
        [Header("Aura Visual Settings")]
        [SerializeField] private Color emissionColor = new Color(1.0f, 0.25f, 0.05f) * 2.5f;
        [SerializeField] private bool isActiveOnStart = false;

        private readonly List<Material> modifiedMaterials = new List<Material>();
        private TrailRenderer weaponTrail;
        private Light auraLight;
        private bool isAuraActive = false;

        private void Start()
        {
            if (isActiveOnStart)
            {
                ActivatePhase2Aura();
            }
        }

        public void ActivatePhase2Aura()
        {
            if (isAuraActive) return;
            isAuraActive = true;

            // 1. Silah ve gövde materyallerine crimson emission ekle
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                foreach (var mat in r.materials)
                {
                    if (mat != null && mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", emissionColor);
                        modifiedMaterials.Add(mat);
                    }
                }
            }

            // 2. Silahın ucuna TrailRenderer ekle
            var mace = transform.Find("KnightVisual/root/pelvis/spine_01/spine_02/spine_03/clavicle_r/upperarm_r/lowerarm_r/hand_r/Mace_Equipped") 
                       ?? FindChildRecursive(transform, "Mace_Equipped");

            if (mace != null)
            {
                weaponTrail = mace.gameObject.AddComponent<TrailRenderer>();
                weaponTrail.time = 0.35f;
                weaponTrail.startWidth = 0.35f;
                weaponTrail.endWidth = 0.05f;

                var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Standard");
                var trailMat = new Material(shader);
                trailMat.color = new Color(1.0f, 0.3f, 0.05f, 0.8f);
                if (trailMat.HasProperty("_EmissionColor"))
                {
                    trailMat.EnableKeyword("_EMISSION");
                    trailMat.SetColor("_EmissionColor", emissionColor);
                }
                weaponTrail.material = trailMat;

                // 3. Hafif alev ışığı (Fiery point light)
                var lightGo = new GameObject("BossAuraLight");
                lightGo.transform.parent = mace;
                lightGo.transform.localPosition = Vector3.up * 0.5f;
                auraLight = lightGo.AddComponent<Light>();
                auraLight.type = LightType.Point;
                auraLight.color = new Color(1.0f, 0.4f, 0.1f);
                auraLight.range = 6.0f;
                auraLight.intensity = 2.0f;
            }
        }

        private Transform FindChildRecursive(Transform current, string name)
        {
            if (current.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                var found = FindChildRecursive(current.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void OnDestroy()
        {
            foreach (var mat in modifiedMaterials)
            {
                if (mat != null) Destroy(mat);
            }
        }
    }
}