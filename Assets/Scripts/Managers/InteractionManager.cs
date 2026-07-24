using UnityEngine;

namespace VRSimulation
{
    public class InteractionManager : MonoBehaviour
    {
        public void Initialize()
        {
            Debug.Log("Interaction manager initialized.");
        }

        public void RegisterInteraction(string interactionName)
        {
            Debug.Log($"Interaction registered: {interactionName}");
        }
    }
}
