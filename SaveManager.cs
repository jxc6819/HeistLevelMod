using System;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using MelonLoader;

namespace IEYTD_Mod2Code
{
    public static class SaveManager
    {
        public static ModSaveData Current;

        static readonly string SaveDirectory = Path.Combine(Directory.GetCurrentDirectory(), "UserData");
        static readonly string SavePath = Path.Combine(SaveDirectory, "IEYTD_Mod2_Save.json");

        public static void Load()
        {
            try
            {
                if (!Directory.Exists(SaveDirectory))
                    Directory.CreateDirectory(SaveDirectory);

                if (!File.Exists(SavePath))
                {
                    Current = new ModSaveData();
                    Save();
                    MelonLogger.Msg("[SaveManager] No save found. Created new save.");
                    return;
                }

                string json = File.ReadAllText(SavePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    Current = new ModSaveData();
                    Save();
                    MelonLogger.Warning("[SaveManager] Save file was empty. Resetting.");
                    return;
                }

                Current = ParseSave(json);

                if (Current == null)
                {
                    Current = new ModSaveData();
                    Save();
                    MelonLogger.Warning("[SaveManager] Save file could not be parsed. Resetting.");
                    return;
                }

                if (Current.SouvenirsFound == null || Current.SouvenirsFound.Length != 6)
                    Current.SouvenirsFound = new bool[6];

                MelonLogger.Msg("[SaveManager] Save loaded.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[SaveManager] Load failed: " + ex);
                Current = new ModSaveData();
            }
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(SaveDirectory))
                    Directory.CreateDirectory(SaveDirectory);

                if (Current == null)
                    Current = new ModSaveData();

                if (Current.SouvenirsFound == null || Current.SouvenirsFound.Length != 6)
                    Current.SouvenirsFound = new bool[6];

                string json = BuildJson(Current);
                File.WriteAllText(SavePath, json);
                MelonLogger.Msg("[SaveManager] Save written to: " + SavePath);
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[SaveManager] Save failed: " + ex);
            }
        }

        public static void MarkLevelComplete()
        {
            EnsureLoaded();
            Current.LevelComplete = true;
            Save();
        }

        public static void TrySetBestTime(float seconds)
        {
            EnsureLoaded();

            if (seconds <= 0f)
                return;

            if (Current.BestTimeSeconds < 0f || seconds < Current.BestTimeSeconds)
            {
                Current.BestTimeSeconds = seconds;
                Save();
            }
        }

        public static void RecordSpeedrunTime(float seconds)
        {
            EnsureLoaded();

            if (seconds <= 0f)
                return;

            Current.LastTimeSeconds = seconds;

            if (Current.BestTimeSeconds < 0f || seconds < Current.BestTimeSeconds)
            {
                Current.BestTimeSeconds = seconds;
                MelonLogger.Msg("[SaveManager] New best speedrun time: " + FormatTime(seconds));
            }

            Save();
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f)
                return "--:--.---";

            int minutes = (int)(seconds / 60f);
            float sec = seconds - (minutes * 60f);
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + sec.ToString("00.000", CultureInfo.InvariantCulture);
        }

        public static void UnlockSouvenir(int souvenirIndex)
        {
            EnsureLoaded();

            if (souvenirIndex < 0 || souvenirIndex >= 6)
                return;

            if (!Current.SouvenirsFound[souvenirIndex])
            {
                Current.SouvenirsFound[souvenirIndex] = true;
                Save();
            }
        }

        public static bool HasSouvenir(int souvenirIndex)
        {
            EnsureLoaded();

            if (souvenirIndex < 0 || souvenirIndex >= 6)
                return false;

            return Current.SouvenirsFound[souvenirIndex];
        }

        public static int GetSouvenirCount()
        {
            EnsureLoaded();

            int count = 0;
            for (int i = 0; i < Current.SouvenirsFound.Length; i++)
            {
                if (Current.SouvenirsFound[i])
                    count++;
            }

            return count;
        }

        public static void ResetSave()
        {
            Current = new ModSaveData();
            Save();
        }

        static void EnsureLoaded()
        {
            if (Current == null)
                Load();
        }

        static string BuildJson(ModSaveData data)
        {
            string lastTime = data.LastTimeSeconds.ToString(CultureInfo.InvariantCulture);
            string bestTime = data.BestTimeSeconds.ToString(CultureInfo.InvariantCulture);

            return
                "{\n" +
                "  \"LevelComplete\": " + BoolToJson(data.LevelComplete) + ",\n" +
                "  \"LastTimeSeconds\": " + lastTime + ",\n" +
                "  \"BestTimeSeconds\": " + bestTime + ",\n" +
                "  \"SouvenirsFound\": [" +
                    BoolToJson(data.SouvenirsFound[0]) + ", " +
                    BoolToJson(data.SouvenirsFound[1]) + ", " +
                    BoolToJson(data.SouvenirsFound[2]) + ", " +
                    BoolToJson(data.SouvenirsFound[3]) + ", " +
                    BoolToJson(data.SouvenirsFound[4]) + ", " +
                    BoolToJson(data.SouvenirsFound[5]) +
                "]\n" +
                "}";
        }

        static string BoolToJson(bool value)
        {
            return value ? "true" : "false";
        }

        static ModSaveData ParseSave(string json)
        {
            ModSaveData data = new ModSaveData();

            Match levelMatch = Regex.Match(json, "\"LevelComplete\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (levelMatch.Success)
                data.LevelComplete = string.Equals(levelMatch.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);

            Match lastTimeMatch = Regex.Match(json, "\"LastTimeSeconds\"\\s*:\\s*(-?\\d+(\\.\\d+)?)", RegexOptions.IgnoreCase);
            if (lastTimeMatch.Success)
            {
                float parsedLastTime;
                if (float.TryParse(lastTimeMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedLastTime))
                    data.LastTimeSeconds = parsedLastTime;
            }

            Match bestTimeMatch = Regex.Match(json, "\"BestTimeSeconds\"\\s*:\\s*(-?\\d+(\\.\\d+)?)", RegexOptions.IgnoreCase);
            if (bestTimeMatch.Success)
            {
                float parsedBestTime;
                if (float.TryParse(bestTimeMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedBestTime))
                    data.BestTimeSeconds = parsedBestTime;
            }

            data.SouvenirsFound = new bool[6];

            Match souvenirsMatch = Regex.Match(json, "\"SouvenirsFound\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (souvenirsMatch.Success)
            {
                string inner = souvenirsMatch.Groups[1].Value;
                string[] parts = inner.Split(',');

                for (int i = 0; i < 6 && i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    data.SouvenirsFound[i] = string.Equals(part, "true", StringComparison.OrdinalIgnoreCase);
                }
            }

            return data;
        }
    }
}
