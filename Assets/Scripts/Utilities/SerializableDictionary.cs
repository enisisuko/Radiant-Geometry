using System;
using System.Collections.Generic;
using UnityEngine;

namespace FadedDreams.Utilities
{
    /// <summary>
    /// Minimal serializable dictionary (string->string) for lightweight data.
    /// </summary>
    [Serializable]
    public class SerializableDictionary
    {
        [Serializable] public struct Pair { public string key; public string value; }
        [SerializeField] private List<Pair> data = new List<Pair>();

        public void Set(string key, string value)
        {
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].key == key) { data[i] = new Pair{ key = key, value = value }; return; }
            }
            data.Add(new Pair{ key = key, value = value});
        }

        public bool TryGet(string key, out string value)
        {
            foreach (var p in data) { if (p.key == key) { value = p.value; return true; } }
            value = null; return false;
        }

        public Dictionary<string,string> ToDictionary()
        {
            var d = new Dictionary<string,string>();
            foreach (var p in data) d[p.key] = p.value;
            return d;
        }
    }
}
