using System;

namespace IEYTD_Mod2Code
{
    [Serializable]
    public class ModSaveData
    {
        public bool LevelComplete = false;
        public float LastTimeSeconds = -1f;
        public float BestTimeSeconds = -1f;
        public bool[] SouvenirsFound = new bool[6];
    }
}
