using System;
using System.Collections.Generic;

namespace FadedDreams.Core
{
    [Serializable]
    public class SaveData
    {
        public string lastScene;
        public string lastCheckpoint;
        public int highestChapterUnlocked = 1;
        public HashSet<string> discoveredCheckpoints = new HashSet<string>();
    }
}
