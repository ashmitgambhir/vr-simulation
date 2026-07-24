using UnityEngine;

namespace VRSimulation
{
    public class UIManager : MonoBehaviour
    {
        private TextMesh statusText;

        public void Initialize()
        {
            if (statusText != null)
            {
                return;
            }

            var textObject = new GameObject("StatusText");
            textObject.transform.SetParent(transform, false);
            textObject.transform.position = new Vector3(0f, 0.7f, 2f);

            statusText = textObject.AddComponent<TextMesh>();
            statusText.text = "Preparing experience";
            statusText.fontSize = 24;
            statusText.anchor = TextAnchor.MiddleCenter;
            statusText.alignment = TextAlignment.Center;
            statusText.color = Color.white;

            var meshRenderer = textObject.GetComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        public void SetMessage(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        public void ShowObjective(string objective)
        {
            SetMessage(objective);
        }
    }
}
