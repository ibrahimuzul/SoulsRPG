using System;
using System.Collections;
using UnityEngine;
using SoulsRPG.Core;
using SoulsRPG.Player;
using SoulsRPG.Save;
using SoulsRPG.UI;

namespace SoulsRPG.Items
{
    /// <summary>
    /// Hızlı item slotundaki itemı kullanır (şimdilik Can İksiri).
    /// Kol: Kare (DualShock/DualSense, JoystickButton0) — Klavye: R
    ///
    /// Souls kuralları:
    ///  - Adet kullanım başında düşer
    ///  - İçme süresince yavaş yürünür; saldırı, yuvarlanma, blok ve koşu yapılamaz
    ///  - Etki anından (effectTime) önce sendelersen (poise kırılırsa) iksir boşa gider
    /// PlayerStats.Awake tarafından oyuncuya otomatik eklenir. Kontrol ayarlarına (PlayerInputHandler) dokunmaz.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(ItemBox))]
    public class PlayerItemUser : MonoBehaviour
    {
        [Header("Hızlı Slot")]
        [Tooltip("Kare tuşuyla kullanılan item")]
        [SerializeField] private string quickItemId = ItemDatabase.HealingPotionId;

        [Header("Tuşlar")]
        [SerializeField] private KeyCode gamepadButton = KeyCode.JoystickButton0; // DualShock/DualSense: Kare
        [SerializeField] private KeyCode keyboardKey = KeyCode.R;

        public string QuickItemId => quickItemId;
        public bool IsUsing => useRoutine != null;

        /// <summary>Item yokken ya da kullanılamazken tuşa basıldı (HUD kısa bir uyarı gösterir).</summary>
        public event Action<string> OnUseDenied;
        /// <summary>Hızlı slot değişti (ileride item seçimi için).</summary>
        public event Action<string> OnQuickItemChanged;

        private PlayerStats stats;
        private PlayerLocomotion locomotion;
        private ItemBox box;
        private Coroutine useRoutine;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            locomotion = GetComponent<PlayerLocomotion>();
            box = GetComponent<ItemBox>();
        }

        private void Start()
        {
            QuickItemHUD.Ensure(this, box);
        }

        private void Update()
        {
            if (stats == null || stats.isDead) return;
            if (locomotion != null && locomotion.IsResting) return; // bonfire menüsünde kare tuşu menüye ait

            if (Input.GetKeyDown(gamepadButton) || Input.GetKeyDown(keyboardKey))
            {
                TryUseQuickItem();
            }
        }

        public void SetQuickItem(string itemId)
        {
            if (quickItemId == itemId) return;
            quickItemId = itemId;
            OnQuickItemChanged?.Invoke(itemId);
        }

        public bool TryUseQuickItem() => TryUse(quickItemId);

        public bool TryUse(string itemId)
        {
            var def = ItemDatabase.Get(itemId);
            if (def == null || def.category != ItemCategory.Consumable) return false;
            if (useRoutine != null) return false;
            if (locomotion != null && !locomotion.CanUseItem) return false;

            if (!box.TryConsume(itemId))
            {
                OnUseDenied?.Invoke(itemId); // bitti: kullanılamaz
                return false;
            }

            useRoutine = StartCoroutine(UseRoutine(def));
            return true;
        }

        private IEnumerator UseRoutine(ItemDefinition def)
        {
            if (locomotion != null) locomotion.SetUsingItem(true, def.moveSpeedWhileUsing);

            // Adet düştü; oyundan çıkılırsa geri gelmesin
            SaveManager.SavePlayer(stats);

            // 1. İçme (etki anına kadar): sendelenirse veya ölürse iksir boşa gider
            float t = 0f;
            while (t < def.effectTime)
            {
                if (IsInterrupted())
                {
                    Finish();
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }

            // 2. Etki
            if (def.healAmount > 0) stats.Heal(def.healAmount);
            SpawnUseEffect(def);
            Audio.SoulsAudioManager.GetOrCreate().PlayBonfireLevelUp(transform.position + Vector3.up);
            GameLog.Info($"[ItemUser] {def.displayName} kullanıldı (+{def.healAmount} can). Kalan: {box.GetCount(def.id)}");

            // 3. Toparlanma (şişeyi indirme)
            while (t < def.useDuration)
            {
                if (IsInterrupted()) break;
                t += Time.deltaTime;
                yield return null;
            }

            Finish();
        }

        private bool IsInterrupted() =>
            stats.isDead || (locomotion != null && locomotion.IsStaggered);

        private void Finish()
        {
            if (locomotion != null) locomotion.SetUsingItem(false);
            useRoutine = null;
        }

        private void OnDisable()
        {
            if (useRoutine != null)
            {
                StopCoroutine(useRoutine);
                Finish();
            }
        }

        /// <summary>Oyuncunun etrafında yükselen sıcak ışık parçacıkları + kısa ışık parlaması.</summary>
        private void SpawnUseEffect(ItemDefinition def)
        {
            var go = new GameObject("ItemUseEffect");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.2f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.duration = 0.8f;
            main.startLifetime = 1.0f;
            main.startSpeed = 1.6f;
            main.startSize = 0.08f;
            main.startColor = def.effectColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            RuntimeShaders.ApplyParticleMaterial(ps, Color.white);
            ps.Play();

            var lightGo = new GameObject("ItemUseLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 1.2f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = def.effectColor;
            light.range = 4f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;
            StartCoroutine(FadeLight(light, 0.6f));
        }

        private static IEnumerator FadeLight(Light light, float duration)
        {
            float start = light.intensity, t = 0f;
            while (t < duration && light != null)
            {
                t += Time.deltaTime;
                light.intensity = Mathf.Lerp(start, 0f, t / duration);
                yield return null;
            }
            if (light != null) Destroy(light.gameObject);
        }
    }
}
