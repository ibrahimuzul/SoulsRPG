using UnityEngine;

namespace SoulsRPG.Items
{
    /// <summary>
    /// İkonu atanmamış itemlar için kodla basit ikon çizer (gerçek ikonlar gelene kadar yer tutucu).
    /// Consumable: şişe silueti (gövde + boyun + mantar), item'ın efekt renginde iksirle dolu.
    /// </summary>
    public static class ItemIconFactory
    {
        private const int Size = 128;

        public static Sprite CreateIcon(ItemDefinition def)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Icon_" + def.id,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[Size * Size];
            Color liquid = def.effectColor;
            Color glass = new Color(0.75f, 0.85f, 0.9f, 0.35f);
            Color outline = new Color(0.08f, 0.07f, 0.06f, 1f);
            Color cork = new Color(0.45f, 0.3f, 0.18f, 1f);
            Color highlight = new Color(1f, 1f, 1f, 0.55f);

            Vector2 bodyCenter = new Vector2(Size * 0.5f, Size * 0.38f);
            float bodyR = Size * 0.3f;
            float neckHalfW = Size * 0.09f, neckBottom = Size * 0.62f, neckTop = Size * 0.8f;
            float corkTop = Size * 0.9f;
            float liquidLevel = Size * 0.5f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float dBody = Vector2.Distance(p, bodyCenter);
                    bool inBody = dBody <= bodyR;
                    bool inNeck = Mathf.Abs(p.x - bodyCenter.x) <= neckHalfW && p.y >= neckBottom - 6f && p.y <= neckTop;
                    bool inCork = Mathf.Abs(p.x - bodyCenter.x) <= neckHalfW * 1.15f && p.y > neckTop && p.y <= corkTop;
                    bool onBodyEdge = Mathf.Abs(dBody - bodyR) <= 3f && !(p.y > bodyCenter.y + bodyR - 8f && Mathf.Abs(p.x - bodyCenter.x) <= neckHalfW);
                    bool onNeckEdge = (Mathf.Abs(Mathf.Abs(p.x - bodyCenter.x) - neckHalfW) <= 2.5f && p.y >= neckBottom - 6f && p.y <= neckTop);

                    Color c = Color.clear;
                    if (inBody || inNeck)
                    {
                        c = p.y <= liquidLevel ? liquid : glass;
                        // Parlama
                        if (Vector2.Distance(p, bodyCenter + new Vector2(-bodyR * 0.4f, bodyR * 0.35f)) < bodyR * 0.16f) c = Color.Lerp(c, highlight, 0.8f);
                    }
                    if (inCork) c = cork;
                    if (onBodyEdge || onNeckEdge) c = outline;

                    pixels[y * Size + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
