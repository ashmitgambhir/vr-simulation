using UnityEngine;

namespace VRSimulation
{
    public class PlayerController : MonoBehaviour
    {
        public void Initialize()
        {
            Debug.Log("Player controller initialized.");
        }

        public void SetHeight(float height)
        {
            Debug.Log($"Player height set to {height}");
        }
    }
}
