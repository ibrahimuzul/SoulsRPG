using UnityEngine;

namespace SoulsRPG.Combat
{
    /// <summary>
    /// Dark Souls vuruş kutularını (Hitbox) ve fiziksel temas noktalarını (Contact Points)
    /// hem Scene hem Game ekranında canlı olarak görselleştiren yardımcı sistem.
    /// </summary>
    public static class CombatDebugVisualizer
    {
        public static void SpawnContactPoint(Vector3 hitPoint, Vector3 hitDirection, bool isBlock = false, string label = null)
        {
            // 1. Oyun Dünyasında 3D Parlayan Temas Küresi
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = isBlock ? "BlockContactMarker" : "HitContactMarker";
            marker.transform.position = hitPoint;
            marker.transform.localScale = Vector3.one * 0.18f;

            // Fizik çarpışmasını kaldır
            var col = marker.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // Renk ve parlaklık
            var rend = marker.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") 
                                  ?? Shader.Find("Unlit/Color") 
                                  ?? Shader.Find("Standard");
                Material mat = new Material(unlitShader);
                Color c = isBlock ? new Color(0.2f, 0.85f, 1.0f, 0.95f) : new Color(1.0f, 0.2f, 0.2f, 0.95f);
                mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                rend.material = mat;
            }

            // 2. 3D Koordinat ve Temas Etiketi
            GameObject textObj = new GameObject("ContactText");
            textObj.transform.SetParent(marker.transform, false);
            textObj.transform.localPosition = Vector3.up * 0.25f;

            var tm = textObj.AddComponent<TextMesh>();
            string title = isBlock ? "🛡️ BLOK TEMASI" : "⚔️ VURUŞ TEMASI";
            if (!string.IsNullOrEmpty(label)) title = label;
            tm.text = $"{title}\n({hitPoint.x:F2}, {hitPoint.y:F2}, {hitPoint.z:F2})";
            tm.fontSize = 24;
            tm.characterSize = 0.05f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = isBlock ? Color.cyan : Color.yellow;
            tm.fontStyle = FontStyle.Bold;

            textObj.AddComponent<FaceCameraBillboard>();
            marker.AddComponent<ContactMarkerFader>();

            // 3. Debug çizgileri (Scene ve Game view Gizmos için)
            Color lineColor = isBlock ? Color.cyan : Color.red;
            Debug.DrawRay(hitPoint, Vector3.up * 0.4f, lineColor, 2.5f);
            Debug.DrawRay(hitPoint, Vector3.down * 0.4f, lineColor, 2.5f);
            Debug.DrawRay(hitPoint, Vector3.left * 0.4f, lineColor, 2.5f);
            Debug.DrawRay(hitPoint, Vector3.right * 0.4f, lineColor, 2.5f);
            Debug.DrawRay(hitPoint, hitDirection * 0.7f, Color.yellow, 2.5f);
        }

        public static void DrawDebugBox(Vector3 center, Vector3 size, Quaternion rotation, Color color)
        {
            Vector3 extents = size * 0.5f;

            Vector3[] points = new Vector3[]
            {
                center + rotation * new Vector3(-extents.x, -extents.y, -extents.z),
                center + rotation * new Vector3( extents.x, -extents.y, -extents.z),
                center + rotation * new Vector3( extents.x, -extents.y,  extents.z),
                center + rotation * new Vector3(-extents.x, -extents.y,  extents.z),
                center + rotation * new Vector3(-extents.x,  extents.y, -extents.z),
                center + rotation * new Vector3( extents.x,  extents.y, -extents.z),
                center + rotation * new Vector3( extents.x,  extents.y,  extents.z),
                center + rotation * new Vector3(-extents.x,  extents.y,  extents.z)
            };

            // Alt taban
            Debug.DrawLine(points[0], points[1], color);
            Debug.DrawLine(points[1], points[2], color);
            Debug.DrawLine(points[2], points[3], color);
            Debug.DrawLine(points[3], points[0], color);

            // Üst tavan
            Debug.DrawLine(points[4], points[5], color);
            Debug.DrawLine(points[5], points[6], color);
            Debug.DrawLine(points[6], points[7], color);
            Debug.DrawLine(points[7], points[4], color);

            // 4 dikey sütun
            Debug.DrawLine(points[0], points[4], color);
            Debug.DrawLine(points[1], points[5], color);
            Debug.DrawLine(points[2], points[6], color);
            Debug.DrawLine(points[3], points[7], color);
        }
    }

    /// <summary>
    /// Temas noktası küresini hafifçe yukarı süzüp 2.5 saniye sonra yok eden bileşen
    /// </summary>
    public class ContactMarkerFader : MonoBehaviour
    {
        private float lifeTime = 2.5f;
        private float timer = 0f;
        private Renderer rend;
        private Color initialColor;

        private void Start()
        {
            rend = GetComponent<Renderer>();
            if (rend != null) initialColor = rend.material.color;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            transform.position += Vector3.up * (0.06f * Time.deltaTime);

            if (rend != null && rend.material != null)
            {
                float alpha = Mathf.Clamp01(1f - (timer / lifeTime));
                Color c = initialColor;
                c.a = alpha;
                rend.material.color = c;
            }

            if (timer >= lifeTime)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Metnin daima kameraya bakmasını sağlayan yardımcı bileşen
    /// </summary>
    public class FaceCameraBillboard : MonoBehaviour
    {
        private UnityEngine.Camera cam;

        private void Start()
        {
            cam = UnityEngine.Camera.main;
        }

        private void LateUpdate()
        {
            if (cam == null) cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }
        }
    }
}
