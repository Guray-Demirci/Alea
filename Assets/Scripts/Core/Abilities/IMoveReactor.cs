using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Hamle TAMAMLANDIKTAN sonra tepki veren pullar. Dogrulama bittigi icin burada
    /// oyun durumunu degistirmek serbesttir (skor, altin, zar, ekstra kirma...).
    ///
    /// GameState bu hook'u iki gruba cagirir:
    ///   isMover = true  -> hamleyi yapan pulun kendisi (Kasif, Katalizor, Sabitleyici, Bar, Gele, Zaman Buken)
    ///   isMover = false -> hedef hanede ZATEN duran diger pullar (Kusatma, Ikiz)
    /// Boylece "kendi hanesine dost pul geldiginde tetiklenen" pullar icin ayri bir arayuz gerekmez.
    /// </summary>
    public interface IMoveReactor : ICheckerAbility
    {
        void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover);
    }
}
