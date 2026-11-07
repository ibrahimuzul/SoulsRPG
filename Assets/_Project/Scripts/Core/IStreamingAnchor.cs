namespace SoulsRPG.Core
{
    /// <summary>
    /// Bölge sınırı hesaplanırken konumu mutlaka kutuya dahil edilecek objeler (bonfire, boss, düşmanlar).
    /// Bunların görseli/collider'ı oyun sırasında oluşabildiği için sadece collider'lara bakmak yetmez.
    /// </summary>
    public interface IStreamingAnchor { }
}
