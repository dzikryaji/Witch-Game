using System;
using UnityEngine;

namespace Witch.Presentation.Rewind
{
    [CreateAssetMenu(menuName = "Witch/Presentation/Rewind Symbol Library", fileName = "RewindSymbolLibrary")]
    public sealed class RewindSymbolLibrary : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string symbolKey;
            public Sprite sprite;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Sprite Find(string symbolKey)
        {
            if (string.IsNullOrEmpty(symbolKey)) return null;
            foreach (Entry entry in entries)
            {
                if (string.Equals(entry.symbolKey, symbolKey, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.sprite;
                }
            }
            return null;
        }
    }
}