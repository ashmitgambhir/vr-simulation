using UnityEngine;

namespace VRSimulation
{
    public class SettingsManager : MonoBehaviour
    {
        private SettingsData currentSettings = new SettingsData();

        public SettingsData Settings => currentSettings;

        public void Initialize()
        {
            currentSettings.masterVolume = PlayerPrefs.GetFloat("settings.masterVolume", 0.8f);
            currentSettings.musicVolume = PlayerPrefs.GetFloat("settings.musicVolume", 0.4f);
            currentSettings.voiceVolume = PlayerPrefs.GetFloat("settings.voiceVolume", 1.0f);
            currentSettings.comfortMode = PlayerPrefs.GetInt("settings.comfortMode", 1) == 1;
            currentSettings.snapTurning = PlayerPrefs.GetInt("settings.snapTurning", 1) == 1;
            currentSettings.smoothTurning = PlayerPrefs.GetInt("settings.smoothTurning", 0) == 1;
            currentSettings.leftHanded = PlayerPrefs.GetInt("settings.leftHanded", 0) == 1;
            currentSettings.subtitles = PlayerPrefs.GetInt("settings.subtitles", 1) == 1;
            currentSettings.standingMode = PlayerPrefs.GetInt("settings.standingMode", 1) == 1;
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("settings.masterVolume", currentSettings.masterVolume);
            PlayerPrefs.SetFloat("settings.musicVolume", currentSettings.musicVolume);
            PlayerPrefs.SetFloat("settings.voiceVolume", currentSettings.voiceVolume);
            PlayerPrefs.SetInt("settings.comfortMode", currentSettings.comfortMode ? 1 : 0);
            PlayerPrefs.SetInt("settings.snapTurning", currentSettings.snapTurning ? 1 : 0);
            PlayerPrefs.SetInt("settings.smoothTurning", currentSettings.smoothTurning ? 1 : 0);
            PlayerPrefs.SetInt("settings.leftHanded", currentSettings.leftHanded ? 1 : 0);
            PlayerPrefs.SetInt("settings.subtitles", currentSettings.subtitles ? 1 : 0);
            PlayerPrefs.SetInt("settings.standingMode", currentSettings.standingMode ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void ApplySettings(SettingsData settings)
        {
            currentSettings = settings;
            SaveSettings();
        }
    }
}
