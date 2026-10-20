using UnityEngine;
using UnityEngine.UI;

namespace GameFactory
{
    [RequireComponent(typeof(Text))]
    public sealed class FactoryLoadingLabel : MonoBehaviour
    {
        private Text label;
        private static readonly string[] Frames = { "Loading.", "Loading..", "Loading..." };
        private void Awake() => label = GetComponent<Text>();
        private void Update() => label.text = Frames[Mathf.FloorToInt(Time.unscaledTime * 2) % Frames.Length];
    }
}
