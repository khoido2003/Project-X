using System.Collections;
using TMPro;
using UnityEngine;

public class TypescriptEffect : MonoBehaviour
{
    [SerializeField]
    private string _text;

    [SerializeField]
    private float _typescriptWaitingTime;

    [SerializeField]
    private float _scaleAnimationTime;

    [SerializeField]
    private AnimationCurve _scaleCurve;

    private TextMeshProUGUI _textUI;

    private void OnEnable()
    {
        _textUI = GetComponent<TextMeshProUGUI>();

        StartCoroutine(Effect());
    }

    private IEnumerator Effect()
    {
        while (true)
        {
            string animText = string.Empty;
            _textUI.text = animText;

            for (int i = 0; i < _text.Length; i++)
            {
                animText += _text[i];
                _textUI.text = animText;
                yield return new WaitForSeconds(_typescriptWaitingTime);
            }

            float curveDeltaTime = 0;
            Vector2 initialScale = new(1, 1);

            Vector2 scaleValues = initialScale;

            while (curveDeltaTime <= _scaleAnimationTime)
            {
                curveDeltaTime += Time.deltaTime;

                float scaleCurve = _scaleCurve.Evaluate(curveDeltaTime);
                scaleValues = new Vector2(scaleCurve, scaleCurve);

                transform.localScale = scaleValues;
                yield return new WaitForEndOfFrame();
            }
        }
    }
}
