using UnityEngine;
using System.Collections;

public class LightDestroy : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 3f;

    private Light lightComponent;
    private bool isFading = false;

    private void Awake()
    {
        lightComponent = GetComponent<Light>();
    }

    public void DestroyLight()
    {
        if (isFading) return;

        if (lightComponent != null)
        {
            StartCoroutine(FadeOutAndDestroy());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator FadeOutAndDestroy()
    {
        isFading = true;
        float startIntensity = lightComponent.intensity;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            lightComponent.intensity = Mathf.Lerp(startIntensity, 0f, t);
            yield return null;
        }

        lightComponent.intensity = 0f;
        Destroy(gameObject);
    }
}
