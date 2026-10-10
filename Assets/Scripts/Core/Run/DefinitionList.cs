using System;
using System.Collections.Generic;

namespace Tavla.Core.Run
{
    /// <summary>Shared validation of the lists that run definitions are built from.</summary>
    internal static class DefinitionList
    {
        /// <summary>
        /// Copies the list and rejects null entries. The copy is what keeps a definition immutable: the caller
        /// can no longer change a list that has already been validated.
        /// </summary>
        /// <exception cref="ArgumentNullException">The list itself is null.</exception>
        /// <exception cref="ArgumentException">An entry is null.</exception>
        public static T[] Copy<T>(IReadOnlyList<T> items, string paramName) where T : class
        {
            if (items == null)
                throw new ArgumentNullException(paramName);

            T[] copy = new T[items.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = items[i] ?? throw new ArgumentException($"Entry {i} is null.", paramName);

            return copy;
        }
    }
}
