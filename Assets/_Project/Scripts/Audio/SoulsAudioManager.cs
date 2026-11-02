using System.Collections.Generic;
using UnityEngine;

namespace SoulsRPG.Audio
{
    /// <summary>
    /// Dark Souls dövüş ses efektlerini (Savurma, Kalkan Engelleme, Darbe/Et Vuruşu, Takla) yöneten merkezi ses yöneticisi.
    /// </summary>
    public class SoulsAudioManager : MonoBehaviour
    {
        public static SoulsAudioManager Instance { get; private set; }

        [Header("Volume Settings (Ses Seviyeleri)")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float swingVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float hitVolume = 0.95f;
        [Range(0f, 1f)] [SerializeField] private float blockVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float rollVolume = 0.75f;

        [Header("Audio Clips (Ses Dosyaları)")]
        [Tooltip("Kılıç/topuz savurma rüzgar sesleri")]
        [SerializeField] private AudioClip[] swingClips;

        [Tooltip("Düşmana veya gövdeye isabet eden et/kemik darbe sesleri")]
        [SerializeField] private AudioClip[] hitClips;

        [Tooltip("Kalkana çarpan çınlayan metal bloklama sesleri")]
        [SerializeField] private AudioClip[] blockClips;

        [Tooltip("Takla / kaçınma rüzgar sesi")]
        [SerializeField] private AudioClip[] rollClips;

        [Header("Boss Audio Clips (Boss Özel Sesleri)")]
        [SerializeField] private AudioClip bossRoarClip;
        [SerializeField] private AudioClip bossSlamClip;
        [SerializeField] private AudioClip bossOverheadClip;

        [Header("Bonfire Audio Clips (Ateş ve Seviye Sesleri)")]
        [SerializeField] private AudioClip bonfireRestClip;
        [SerializeField] private AudioClip bonfireLevelUpClip;

        // 3D Spatial Audio için AudioSource Havuzu (Pool)
        private readonly List<AudioSource> audioSourcePool = new List<AudioSource>();
        private const int PoolInitialSize = 10;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializePool();
            AutoLoadClipsIfEmpty();
        }

        private void InitializePool()
        {
            for (int i = 0; i < PoolInitialSize; i++)
            {
                CreateNewPooledSource();
            }
        }

        private AudioSource CreateNewPooledSource()
        {
            var go = new GameObject("PooledAudioSource");
            go.transform.parent = transform;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0.8f; // %80 3D spatial, %20 direkt
            src.minDistance = 2f;
            src.maxDistance = 35f;
            src.rolloffMode = AudioRolloffMode.Linear;
            audioSourcePool.Add(src);
            return src;
        }

        private AudioSource GetAvailableSource()
        {
            for (int i = 0; i < audioSourcePool.Count; i++)
            {
                if (!audioSourcePool[i].isPlaying)
                {
                    return audioSourcePool[i];
                }
            }
            return CreateNewPooledSource();
        }

        public void PlaySwing(Vector3 position)
        {
            PlayRandomClip(swingClips, position, swingVolume * masterVolume, 0.93f, 1.07f);
        }

        public void PlayHit(Vector3 position)
        {
            PlayRandomClip(hitClips, position, hitVolume * masterVolume, 0.90f, 1.10f);
        }

        public void PlayBlock(Vector3 position)
        {
            PlayRandomClip(blockClips, position, blockVolume * masterVolume, 0.95f, 1.06f);
        }

        public void PlayRoll(Vector3 position)
        {
            PlayRandomClip(rollClips, position, rollVolume * masterVolume, 0.95f, 1.05f);
        }

        public void PlayBossRoar(Vector3 position)
        {
            if (bossRoarClip == null) return;
            var src = GetAvailableSource();
            src.transform.position = position;
            src.clip = bossRoarClip;
            src.volume = masterVolume;
            src.pitch = 1.0f;
            src.Play();
        }

        public void PlayBossSlam(Vector3 position)
        {
            if (bossSlamClip == null) return;
            var src = GetAvailableSource();
            src.transform.position = position;
            src.clip = bossSlamClip;
            src.volume = masterVolume;
            src.pitch = Random.Range(0.96f, 1.04f);
            src.Play();
        }

        public void PlayBossOverhead(Vector3 position)
        {
            if (bossOverheadClip == null) return;
            var src = GetAvailableSource();
            src.transform.position = position;
            src.clip = bossOverheadClip;
            src.volume = masterVolume * 0.95f;
            src.pitch = Random.Range(0.94f, 1.05f);
            src.Play();
        }

        public void PlayBonfireRest(Vector3 position)
        {
            if (bonfireRestClip == null) return;
            var src = GetAvailableSource();
            src.transform.position = position;
            src.clip = bonfireRestClip;
            src.volume = masterVolume * 0.95f;
            src.pitch = 1.0f;
            src.Play();
        }

        public void PlayBonfireLevelUp(Vector3 position)
        {
            if (bonfireLevelUpClip == null) return;
            var src = GetAvailableSource();
            src.transform.position = position;
            src.clip = bonfireLevelUpClip;
            src.volume = masterVolume * 1.0f;
            src.pitch = 1.0f;
            src.Play();
        }

        private void PlayRandomClip(AudioClip[] clips, Vector3 position, float volume, float minPitch, float maxPitch)
        {
            if (clips == null || clips.Length == 0) return;

            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            var src = GetAvailableSource();
            src.transform.position = position;
            src.clip = clip;
            src.volume = volume;
            src.pitch = Random.Range(minPitch, maxPitch); // Tekdüzeliği önlemek için doğal ton çeşitliliği
            src.Play();
        }

        /// <summary>
        /// Sahneye elle atanmamışsa Assets/_Project/Audio altındaki sesleri otomatik yükler.
        /// </summary>
        public void AutoLoadClipsIfEmpty()
        {
#if UNITY_EDITOR
            if (swingClips == null || swingClips.Length == 0)
            {
                swingClips = LoadClipsFromPath("Assets/_Project/Audio/SW_Swing_");
            }
            if (hitClips == null || hitClips.Length == 0)
            {
                hitClips = LoadClipsFromPath("Assets/_Project/Audio/HIT_Impact_");
            }
            if (blockClips == null || blockClips.Length == 0)
            {
                blockClips = LoadClipsFromPath("Assets/_Project/Audio/SHIELD_Block_");
            }
            if (rollClips == null || rollClips.Length == 0)
            {
                rollClips = LoadClipsFromPath("Assets/_Project/Audio/ROLL_Dodge_");
            }
            if (bossRoarClip == null)
            {
                bossRoarClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/BOSS_Roar_PhaseTransition.wav");
            }
            if (bossSlamClip == null)
            {
                bossSlamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/BOSS_GroundSlam_Impact.wav");
            }
            if (bossOverheadClip == null)
            {
                bossOverheadClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/BOSS_Overhead_Smash.wav");
            }
            if (bonfireRestClip == null)
            {
                bonfireRestClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/BONFIRE_Rest.wav");
            }
            if (bonfireLevelUpClip == null)
            {
                bonfireLevelUpClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/BONFIRE_LevelUp.wav");
            }
#endif
        }

#if UNITY_EDITOR
        private static AudioClip[] LoadClipsFromPath(string prefix)
        {
            var list = new List<AudioClip>();
            for (int i = 1; i <= 9; i++)
            {
                string path = $"{prefix}0{i}.wav";
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    list.Add(clip);
                }
            }
            return list.ToArray();
        }
#endif

        /// <summary>
        /// Sahnede SoulsAudioManager yoksa anında oluşturup döner.
        /// </summary>
        public static SoulsAudioManager GetOrCreate()
        {
            if (Instance != null) return Instance;

            var existing = FindAnyObjectByType<SoulsAudioManager>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("--- SOULS AUDIO MANAGER ---");
            Instance = go.AddComponent<SoulsAudioManager>();
            return Instance;
        }
    }
}
