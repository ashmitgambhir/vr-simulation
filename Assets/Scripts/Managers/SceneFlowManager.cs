using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRSimulation
{
    public class SceneFlowManager : MonoBehaviour
    {
        public void Initialize()
        {
            Debug.Log("Scene flow manager initialized.");
        }

        public void LoadModule(int moduleId)
        {
            Debug.Log($"Loading module {moduleId}");
        }

        public void RestartExperience()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
