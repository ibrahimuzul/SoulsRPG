using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SoulsRPG.Items;

namespace SoulsRPG.UI
{
    /// <summary>
    /// Sol alttaki hızlı item slotu: item ikonu, yanında kalan adet, altında adı.
    /// Adet 0 olunca ikon sönükleşir; boşken kullanmaya çalışınca slot kısa sallanır.
    /// Oyunun HUD canvas'ına (Core) koddan kurulur.
    /// </summary>
    public class QuickItemHUD : MonoBehaviour
    {
        private static QuickItemHUD instance;

        private PlayerItemUser user;
        private ItemBox box;

        private RectTransform root;
        private Image iconImage;
        private Image frameImage;
        private Text countText;
        private Text nameText;
        private Coroutine shakeRoutine;
        private Vector2 basePosition;

        private static readonly Color FrameColor = new Color(0.06f, 0.06f, 0.08f, 0.85f);
        private static readonly Color FrameDeniedColor = new Color(0.45f, 0.08f, 0.06f, 0.9f);
        private static readonly Color CountColor = new Color(0.98f, 0.92f, 0.75f);
        private static readonly Color CountEmptyColor = new Color(0.85f, 0.25f, 0.2f);

        public static void Ensure(PlayerItemUser user, ItemBox box)
        {
            if (instance == null)
            {
                var canvas = HudCanvas.Get();
                var go = new GameObject("QuickItemSlot");
                go.transform.SetParent(canvas.transform, false);
                instance = go.AddComponent<QuickItemHUD>();
                instance.Build();
            }
            instance.Bind(user, box);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            instance = null;
        }

        private void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            root = gameObject.AddComponent<RectTransform>();
            root.anchorMin = root.anchorMax = Vector2.zero; // sol alt
            root.pivot = Vector2.zero;
            basePosition = new Vector2(40f, 48f);
            root.anchoredPosition = basePosition;
            root.sizeDelta = new Vector2(84f, 84f);

            // Çerçeve
            frameImage = gameObject.AddComponent<Image>();
            frameImage.color = FrameColor;
            frameImage.raycastTarget = false;
            var outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.62f, 0.38f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            // İkon
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(8f, 8f);
            iconRect.offsetMax = new Vector2(-8f, -8f);
            iconImage = iconGo.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            // Adet (ikonun sağ yanında, büyük)
            var countGo = new GameObject("Count");
            countGo.transform.SetParent(transform, false);
            var countRect = countGo.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(0f, 0f);
            countRect.anchoredPosition = new Vector2(8f, -2f);
            countRect.sizeDelta = new Vector2(60f, 44f);
            countText = countGo.AddComponent<Text>();
            countText.font = font;
            countText.fontSize = 34;
            countText.fontStyle = FontStyle.Bold;
            countText.alignment = TextAnchor.LowerLeft;
            countText.color = CountColor;
            countText.raycastTarget = false;
            var countShadow = countGo.AddComponent<Outline>();
            countShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            countShadow.effectDistance = new Vector2(2f, -2f);

            // Ad (slotun altında)
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(0f, 0f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -4f);
            nameRect.sizeDelta = new Vector2(220f, 22f);
            nameText = nameGo.AddComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 15;
            nameText.alignment = TextAnchor.UpperLeft;
            nameText.color = new Color(0.9f, 0.85f, 0.7f, 0.9f);
            nameText.raycastTarget = false;
            var nameShadow = nameGo.AddComponent<Outline>();
            nameShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        }

        private void Bind(PlayerItemUser newUser, ItemBox newBox)
        {
            Unbind();
            user = newUser;
            box = newBox;
            if (box != null) box.OnItemCountChanged += HandleCountChanged;
            if (user != null)
            {
                user.OnUseDenied += HandleUseDenied;
                user.OnQuickItemChanged += HandleQuickItemChanged;
            }
            Refresh();
        }

        private void Unbind()
        {
            if (box != null) box.OnItemCountChanged -= HandleCountChanged;
            if (user != null)
            {
                user.OnUseDenied -= HandleUseDenied;
                user.OnQuickItemChanged -= HandleQuickItemChanged;
            }
        }

        private void OnDestroy()
        {
            Unbind();
            if (instance == this) instance = null;
        }

        private void HandleCountChanged(string id, int count)
        {
            if (user != null && id == user.QuickItemId) Refresh();
        }

        private void HandleQuickItemChanged(string id) => Refresh();

        private void HandleUseDenied(string id)
        {
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(ShakeRoutine());
        }

        private void Refresh()
        {
            if (user == null || box == null) return;
            var def = ItemDatabase.Get(user.QuickItemId);
            bool hasItem = def != null;
            gameObject.SetActive(hasItem);
            if (!hasItem) return;

            int count = box.GetCount(def.id);
            iconImage.sprite = ItemDatabase.GetIcon(def);
            iconImage.color = count > 0 ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.45f);
            countText.text = count.ToString();
            countText.color = count > 0 ? CountColor : CountEmptyColor;
            nameText.text = def.displayName;
        }

        private IEnumerator ShakeRoutine()
        {
            float t = 0f;
            frameImage.color = FrameDeniedColor;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                float offset = Mathf.Sin(t * 70f) * 6f * (1f - t / 0.3f);
                root.anchoredPosition = basePosition + new Vector2(offset, 0f);
                yield return null;
            }
            root.anchoredPosition = basePosition;
            frameImage.color = FrameColor;
            shakeRoutine = null;
        }
    }
}
