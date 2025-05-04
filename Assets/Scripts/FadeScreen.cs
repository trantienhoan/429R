using System.Collections;
using UnityEngine;

public class FadeScreen : MonoBehaviour
{
    public bool fadeOnStart = true;
    public float fadeDuration = 2f;
    public Color fadeColor = Color.black;
    public string colorPropertyName = "_Color";
    public AnimationCurve fadeCurve = AnimationCurve.Linear(0, 0, 1, 1);

    private Renderer rend;
    private Coroutine fadeRoutine;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (fadeOnStart)
        {
            FadeIn();
        }
    }

    public void FadeIn()
    {
        Fade(1, 0);
    }

    public void FadeOut()
    {
        Fade(0, 1);
    }

    public void Fade(float alphaIn, float alphaOut)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }
        fadeRoutine = StartCoroutine(FadeRoutine(alphaIn, alphaOut, fadeDuration));
    }

    private IEnumerator FadeRoutine(float alphaIn, float alphaOut, float duration)
    {
        float timer = 0;
        Color newColor = fadeColor;

        while (timer <= duration)
        {
            float alpha = Mathf.Lerp(alphaIn, alphaOut, fadeCurve.Evaluate(timer / duration));
            newColor.a = alpha;
            rend.material.SetColor(colorPropertyName, newColor);
            timer += Time.deltaTime;
            yield return null;
        }

        // Ensure the final alpha value is set
        newColor.a = alphaOut;
        rend.material.SetColor(colorPropertyName, newColor);
    }

    public void FadeOutAndIn(float fadeOutDuration, float fadeInDuration)
    {
        StartCoroutine(FadeOutAndInRoutine(fadeOutDuration, fadeInDuration));
    }

    private IEnumerator FadeOutAndInRoutine(float fadeOutDuration, float fadeInDuration)
    {
        yield return FadeRoutine(0, 1, fadeInDuration);
        yield return FadeRoutine(1, 0, fadeOutDuration);
    }
}