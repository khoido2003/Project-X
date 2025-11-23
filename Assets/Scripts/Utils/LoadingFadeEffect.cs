using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LoadingFadeEffect : SingletonPersistent<LoadingFadeEffect>
{
    public static bool s_canLoad;

    [SerializeField]
    private Image m_loadingBackground;

    [SerializeField]
    [Range(0f, 0.5f)]
    private float m_loadingStepTime;

    [SerializeField]
    [Range(0f, 0.5f)]
    private float m_loadingStepValue;

    private IEnumerator FadeAllEffect()
    {
        yield return StartCoroutine(FadeInEffect());

        yield return new WaitForSeconds(1);

        yield return StartCoroutine(FadeOutEffect());
    }

    private IEnumerator FadeInEffect()
    {
        Color bgColor = m_loadingBackground.color;

        bgColor.a = 0;

        m_loadingBackground.color = bgColor;

        m_loadingBackground.gameObject.SetActive(true);

        while (bgColor.a <= 1)
        {
            yield return new WaitForSeconds(m_loadingStepTime);

            bgColor.a += m_loadingStepValue;

            m_loadingBackground.color = bgColor;
        }

        s_canLoad = true;
    }

    private IEnumerator FadeOutEffect()
    {
        s_canLoad = false;

        Color bgColor = m_loadingBackground.color;

        while (bgColor.a >= 0)
        {
            yield return new WaitForSeconds(m_loadingStepTime);

            bgColor.a -= m_loadingStepValue;

            m_loadingBackground.color = bgColor;
        }

        m_loadingBackground.gameObject.SetActive(false);
    }

    public void FadeIn()
    {
        StartCoroutine(FadeInEffect());
    }

    public void FadeOut()
    {
        StartCoroutine(FadeOutEffect());
    }

    public void FadeAll()
    {
        StartCoroutine(FadeAllEffect());
    }
}
