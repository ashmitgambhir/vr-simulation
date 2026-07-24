using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace VRSimulation
{
    [Serializable]
    public class SaveData
    {
        public UserData user = new UserData();
        public SettingsData settings = new SettingsData();
        public ProgressData progress = new ProgressData();
        public List<ModuleProgressData> modules = new List<ModuleProgressData>();
        public List<QuizResultData> quizzes = new List<QuizResultData>();
        public List<AchievementData> achievements = new List<AchievementData>();
    }

    [Serializable]
    public class UserData
    {
        public string userId = "guest";
        public string username = "Guest";
        public string createdAt = DateTime.UtcNow.ToString("o");
        public string lastPlayed = DateTime.UtcNow.ToString("o");
        public int currentModule = 0;
    }

    [Serializable]
    public class SettingsData
    {
        public float masterVolume = 0.8f;
        public float musicVolume = 0.4f;
        public float voiceVolume = 1.0f;
        public bool comfortMode = true;
        public bool snapTurning = true;
        public bool smoothTurning = false;
        public bool leftHanded = false;
        public bool subtitles = true;
        public bool standingMode = true;
    }

    [Serializable]
    public class ProgressData
    {
        public bool experienceCompleted = false;
        public int percentComplete = 0;
        public List<int> completedModules = new List<int>();
    }

    [Serializable]
    public class ModuleProgressData
    {
        public int moduleId = 0;
        public string moduleName = "Intro";
        public bool completed = false;
        public int score = 0;
        public int attempts = 0;
        public float completionTimeSeconds = 0f;
        public string lastPlayed = DateTime.UtcNow.ToString("o");
    }

    [Serializable]
    public class QuizResultData
    {
        public int quizId = 0;
        public int moduleId = 0;
        public int score = 0;
        public int totalQuestions = 0;
        public int correctAnswers = 0;
        public string completedAt = DateTime.UtcNow.ToString("o");
    }

    [Serializable]
    public class AchievementData
    {
        public int achievementId = 0;
        public string name = "First Steps";
        public bool earned = false;
        public string earnedDate = string.Empty;
    }

    public class SaveManager : MonoBehaviour
    {
        public SaveData SaveState { get; private set; } = new SaveData();

        private string SaveDirectory => Path.Combine(Application.persistentDataPath, "PersistentData");
        private string SavePath => Path.Combine(SaveDirectory, "SaveData.json");
        private string BackupPath => Path.Combine(SaveDirectory, "SaveData_Backup.json");

        public void Initialize()
        {
            EnsureDirectory();

            if (!File.Exists(SavePath))
            {
                SaveState = CreateDefaultSaveData();
                WriteSave();
            }
            else
            {
                LoadGame();
            }
        }

        public void SaveGame()
        {
            SaveState.user.lastPlayed = DateTime.UtcNow.ToString("o");
            WriteSave();
        }

        public void LoadGame()
        {
            if (!File.Exists(SavePath))
            {
                SaveState = CreateDefaultSaveData();
                return;
            }

            try
            {
                var json = File.ReadAllText(SavePath);
                SaveState = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception)
            {
                SaveState = CreateDefaultSaveData();
            }
        }

        public void ResetProgress()
        {
            SaveState.progress = new ProgressData();
            SaveState.modules.Clear();
            SaveState.quizzes.Clear();
            SaveState.achievements.Clear();
            SaveGame();
        }

        public void SaveSettings(SettingsData settings)
        {
            SaveState.settings = settings;
            SaveGame();
        }

        public SettingsData LoadSettings()
        {
            return SaveState.settings;
        }

        public void SaveModule(ModuleProgressData module)
        {
            var existing = SaveState.modules.Find(item => item.moduleId == module.moduleId);
            if (existing != null)
            {
                existing.completed = module.completed;
                existing.score = module.score;
                existing.attempts = module.attempts;
                existing.completionTimeSeconds = module.completionTimeSeconds;
                existing.lastPlayed = module.lastPlayed;
                existing.moduleName = module.moduleName;
            }
            else
            {
                SaveState.modules.Add(module);
            }

            SaveGame();
        }

        public void SaveQuiz(QuizResultData quiz)
        {
            SaveState.quizzes.Add(quiz);
            SaveGame();
        }

        private void WriteSave()
        {
            EnsureDirectory();

            if (File.Exists(SavePath))
            {
                File.Copy(SavePath, BackupPath, true);
            }

            var json = JsonUtility.ToJson(SaveState, true);
            File.WriteAllText(SavePath, json);
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        private SaveData CreateDefaultSaveData()
        {
            return new SaveData();
        }
    }
}
