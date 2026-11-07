using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SoulsRPG.UI;

namespace SoulsRPG.EditorTools
{
    /// <summary>
    /// Teşhis aracı (oyunu değiştirmez): Play sırasında can/stamina barlarının ekrandaki durumunu ve
    /// üstlerine denk gelen tüm UI görüntülerini çizim sırasıyla Console'a yazar.
    /// </summary>
    public static class HudDebugDump
    {
        [MenuItem("SoulsRPG/Debug/HUD Barlarını İncele (Play'de çalıştır)", false, 100)]
        public static void Dump()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("HUD İnceleme", "Bu komutu oyun çalışırken (Play) kullan.", "Tamam");
                return;
            }

            var hud = Object.FindAnyObjectByType<StatBarHUD>();
            if (hud == null) { Debug.LogWarning("[HUD-DUMP] StatBarHUD bulunamadı"); return; }

            var so = new SerializedObject(hud);
            var health = so.FindProperty("healthSlider").objectReferenceValue as Slider;
            var stamina = so.FindProperty("staminaSlider").objectReferenceValue as Slider;

            var sb = new StringBuilder("[HUD-DUMP] Ekran: " + Screen.width + "x" + Screen.height + "\n");
            DumpSlider(sb, "CAN", health);
            DumpSlider(sb, "STAMINA", stamina);

            // Barların ekran alanıyla çakışan tüm görüntüler (çizim sırasına göre)
            Rect area = ScreenRect(health != null ? (RectTransform)health.transform : null);
            if (stamina != null) area = Union(area, ScreenRect((RectTransform)stamina.transform));
            sb.AppendLine($"Bar alanı (ekran px): {area}");
            sb.AppendLine("Çakışan görüntüler [canvas sortingOrder / depth] yol | renk | renderer alpha | ekran alanı:");

            var graphics = Object.FindObjectsByType<Graphic>();
            System.Array.Sort(graphics, (a, b) =>
            {
                int ca = a.canvas != null ? a.canvas.sortingOrder : 0, cb = b.canvas != null ? b.canvas.sortingOrder : 0;
                return ca != cb ? ca.CompareTo(cb) : a.depth.CompareTo(b.depth);
            });
            foreach (var g in graphics)
            {
                if (!g.isActiveAndEnabled) continue;
                var r = ScreenRect(g.rectTransform);
                if (!r.Overlaps(area)) continue;
                float alpha = g.canvasRenderer != null ? g.canvasRenderer.GetAlpha() : 1f;
                var groupAlpha = 1f;
                foreach (var cg in g.GetComponentsInParent<CanvasGroup>()) groupAlpha *= cg.alpha;
                sb.AppendLine($"  [{(g.canvas != null ? g.canvas.sortingOrder : 0)}/{g.depth}] {Path(g.transform)} | {g.GetType().Name} {g.color} | rAlpha {alpha:F2} groupAlpha {groupAlpha:F2} | {r}");
            }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("HUD İnceleme", "Rapor Console'a ([HUD-DUMP]) yazıldı.", "Tamam");
        }

        private static void DumpSlider(StringBuilder sb, string label, Slider s)
        {
            if (s == null) { sb.AppendLine($"{label}: slider yok"); return; }
            var rt = (RectTransform)s.transform;
            sb.AppendLine($"{label}: value {s.value:F1} / min {s.minValue} / max {s.maxValue:F1} (normalized {s.normalizedValue:F2}) dir {s.direction} " +
                          $"| rect {rt.rect.size} sizeDelta {rt.sizeDelta} scale {rt.lossyScale} | ekran {ScreenRect(rt)}");
            if (s.fillRect != null)
            {
                var f = s.fillRect;
                sb.AppendLine($"   fill: {Path(f)} anchorMin {f.anchorMin} anchorMax {f.anchorMax} offMin {f.offsetMin} offMax {f.offsetMax} | ekran {ScreenRect(f)}");
                var parent = f.parent as RectTransform;
                if (parent != null) sb.AppendLine($"   fill area: anchorMin {parent.anchorMin} anchorMax {parent.anchorMax} offMin {parent.offsetMin} offMax {parent.offsetMax} | ekran {ScreenRect(parent)}");
            }
        }

        private static Rect ScreenRect(RectTransform rt)
        {
            if (rt == null) return new Rect();
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        private static string Path(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
