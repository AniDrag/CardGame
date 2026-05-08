using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadScene : MonoBehaviour
{
    public static LoadScene Instance { get; private set; }

    [SerializeField] private Transform loadingScreen;
    [SerializeField] private Slider loadingBar;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("Multiple instances of LoadScene detected! Destroying duplicate.");
            Destroy(gameObject);
        }

        loadingScreen = GameObject.Find("Canvas-LoadingScreen")?.transform;
        if (loadingScreen == null)
        {
            Debug.LogError(this.gameObject.name + ": Canvas-LoadingScreen not found in the scene! Please ensure there is a GameObject named 'Canvas-LoadingScreen' with a Slider component.");
        }
        else
        {
            
            if (loadingBar == null)
            {
                loadingBar = loadingScreen.GetComponentInChildren<Slider>();
                Debug.LogError("Slider component not found in LoadingScreen! Please ensure there is a Slider component as a child of the LoadingScreen GameObject.");
            }
            loadingScreen.gameObject.SetActive(false);
        }
    }

    public void LOADSCENE(int index)
    {
        StartCoroutine(LoadSceneAsync(index));
    }

    IEnumerator LoadSceneAsync(int index)
    {
        if (loadingScreen != null)
            loadingScreen.gameObject.SetActive(true);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(index);

        while (!asyncLoad.isDone)
        {
            if (loadingBar != null)
            {
                loadingBar.value = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            }
            yield return null;
        }

        yield return new WaitForSeconds(1);

        if (loadingScreen != null)
            loadingScreen.gameObject.SetActive(false);
    }
}