using System;
using UnityEngine;
[CreateAssetMenu(fileName = "Graphics Settings", menuName = "Settings/Graphics Settings")]
public class GraphicsSettings : ScriptableObject
{
    [Header("Resolution")]
    public int resolutionX = 1920;
    public int resolutionY = 1080;
    public FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow;
    Resolution[] resolutions;

    [Header("Quality")]
    public int shadowQuality = 2; // 0=Low,1=Medium,2=High
    public bool vSync = true;
    public int targetFPS = 60;


    //bool _fullScreen = false;
    //int _fps = 60;
    FullScreenMode screenResizingMode;
    private void Awake()
    {
        resolutions = Screen.resolutions;
        screenResizingMode = Screen.fullScreenMode;
    }
    #region Other math 
    void AutomaticSceenRezolution()
    {
        resolutionX = Screen.width;
        resolutionY = Screen.height;
    }

    #endregion

    #region Public functions
    public void SETT_ResolutionSetting(int index)
    {
        switch (index)
        {
            default:
                AutomaticSceenRezolution();
                break;
            case 1:
                resolutionX = 1920;
                resolutionX = 1080;
                break;
            case 2:
                resolutionX = 1366;
                resolutionX = 768;
                break;
            case 3:
                resolutionX = 1536;
                resolutionX = 864;
                break;
            case 4:
                resolutionX = 1280;
                resolutionX = 720;
                break;
            case 5:
                resolutionX = 1440;
                resolutionX = 900;
                break;
        }
        Screen.SetResolution(resolutionX, resolutionY, screenResizingMode);

    }
    public void SETT_DisplayModeSetting(int index)
    {
        switch (index)
        {
            default:
                screenResizingMode = FullScreenMode.ExclusiveFullScreen; break;
            case 1:
                screenResizingMode = FullScreenMode.FullScreenWindow; break;
            case 2:
                screenResizingMode = FullScreenMode.Windowed; break;
        }
        Screen.fullScreenMode = screenResizingMode;
    }
    public void SETT_FPS(float index)
    {
        if ((int)index < 25)
            Application.targetFrameRate = -1; // Unlimited
        else
            Application.targetFrameRate = (int)index; // set to frame rate
    }
    void SETT_TextureQuality()
    {

    }
    public void SETT_ShadowQuality(int index)
    {
        // Example: Low = disable shadows, Medium = hard shadows, High = soft shadows
        switch (index)
        {
            case 0:
                QualitySettings.shadows = ShadowQuality.Disable; break;
            case 1:
                QualitySettings.shadows = ShadowQuality.HardOnly; break;
            case 2:
                QualitySettings.shadows = ShadowQuality.All; break;
        }
    }
    void SETT_AntiAliasing()
    {
        //FXAA (Fast Approximate Anti-Aliasing) low
        //Anti allising MSAA (Multisample Anti-Aliasing) med
        //TAA (Temporal Anti-Aliasing) high

    }
    void SETT_AmbientOcclusion()
    {

    }
    public void SETT_VSync(int index)
    {
        if (index == 1)
        {
            QualitySettings.vSyncCount = 1;
            Debug.Log("V sync on");
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            Debug.Log("V sync off");
        }

    }
    void SETT_MotionBlurr()
    {

    }
    void SETT_Bloom()
    {

    }
    void SETT_FilmGrain()
    {

    }
    void SETT_CromaticAberration()
    {

    }
    #endregion
}

