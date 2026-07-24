using UnityEngine;

namespace VRSimulation
{
    public class AudioManager : MonoBehaviour
    {
        public void Initialize()
        {
            Debug.Log("Audio manager initialized.");
        }

        public void PlayNarration(string clipName)
        {
            Debug.Log($"Playing narration clip: {clipName}");
        }

        public void SetVolume(float master, float music, float voice)
        {
            Debug.Log($"Volume updated: master={master}, music={music}, voice={voice}");
        }
    }
}
