using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Tavla.View
{
    /// <summary>
    /// Development-only informational logging.
    ///
    /// The methods are <see cref="ConditionalAttribute"/>: in a release player the compiler removes the
    /// whole call INCLUDING the evaluation of its arguments, so interpolated strings built for these
    /// logs cost nothing in shipping builds. Warnings and errors still go through <see cref="Debug"/>
    /// directly because they must stay visible in player logs.
    /// </summary>
    internal static class GameLog
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message)
        {
            Debug.Log(message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message, UnityEngine.Object context)
        {
            Debug.Log(message, context);
        }
    }
}
