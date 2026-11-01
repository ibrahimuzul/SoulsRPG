using UnityEngine;

namespace SoulsRPG.Environment
{
    /// <summary>
    /// Meşale ve mangal ateşlerinin gerçekçi titreşimini simüle eder.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class TorchFlicker : MonoBehaviour
    {
        [Header("Işık Şiddeti Aralığı")]
        [SerializeField] private float minIntensity = 1.8f;
        [SerializeField] private float maxIntensity = 2.8f;

        [Header("Titreşim Hızı")]
        [SerializeField] private float flickerSpeed = 9f;

        private Light torchLight;
        private float noiseOffset;

        private void Awake()
        {
            torchLight = GetComponent<Light>();
            noiseOffset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (torchLight == null) return;
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, noiseOffset);
            torchLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
        }
    }
}
