using UnityEngine;

namespace VRSimulation
{
    public class ExperienceBootstrap : MonoBehaviour
    {
        [SerializeField] private bool showStartupMessage = true;

        private SaveManager saveManager;
        private SettingsManager settingsManager;
        private UIManager uiManager;
        private AudioManager audioManager;
        private InteractionManager interactionManager;
        private SceneFlowManager sceneFlowManager;
        private PlayerController playerController;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            saveManager = GetComponent<SaveManager>() ?? gameObject.AddComponent<SaveManager>();
            settingsManager = GetComponent<SettingsManager>() ?? gameObject.AddComponent<SettingsManager>();
            uiManager = GetComponent<UIManager>() ?? gameObject.AddComponent<UIManager>();
            audioManager = GetComponent<AudioManager>() ?? gameObject.AddComponent<AudioManager>();
            interactionManager = GetComponent<InteractionManager>() ?? gameObject.AddComponent<InteractionManager>();
            sceneFlowManager = GetComponent<SceneFlowManager>() ?? gameObject.AddComponent<SceneFlowManager>();
            playerController = GetComponent<PlayerController>() ?? gameObject.AddComponent<PlayerController>();

            InitializeManagers();
        }

        private void Start()
        {
            if (showStartupMessage)
            {
                uiManager.SetMessage("VR experience scaffold ready");
            }
        }

        private void InitializeManagers()
        {
            saveManager.Initialize();
            settingsManager.Initialize();
            uiManager.Initialize();
            audioManager.Initialize();
            interactionManager.Initialize();
            sceneFlowManager.Initialize();
            playerController.Initialize();
        }

        public SaveManager SaveManager => saveManager;
        public SettingsManager SettingsManager => settingsManager;
        public UIManager UIManager => uiManager;
    }
}
