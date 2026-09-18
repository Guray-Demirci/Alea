namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Kirilan bir pulun nereye gidecegi. Dokumandaki iki istisna bu enum ile karsilanir:
    /// Anka Pulu -> HomeEntry (bara degil baslangic hanesine), Fedakar Pul -> Destroyed (yok olur).
    /// </summary>
    public enum CaptureDestination
    {
        Bar = 0,        // Klasik kural: ortaya (bar) gider
        HomeEntry = 1,  // Anka Pulu: kendi baslangic hanesine doner
        Destroyed = 2   // Fedakar Pul / Barut Ficisi: oyundan tamamen cikar
    }
}
