namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Klasik tavla pulu. Hicbir yetenek arayuzu uygulamaz — kurallarin "referans" davranisidir.
    /// Ozel pullarin dogru calisip calismadigi hep bununla karsilastirilarak test edilir.
    /// Ayrica Sisli Hane'ye giren her ozel pul, o hanede durdugu surece davranis olarak
    /// tam olarak bu sinifa denk duser.
    /// </summary>
    public class DefaultChecker : BaseChecker
    {
        public const string Id = "checker_default";

        public override string TypeId => Id;

        public DefaultChecker(int ownerId) : base(ownerId)
        {
        }
    }
}
