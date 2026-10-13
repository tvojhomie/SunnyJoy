using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EggCatcher
{
    public sealed partial class EggCatcherApp
    {
        private RectTransform CreateModal(string title, float height)
        {
            CloseModal();
            var overlay = new GameObject("EggCatcherDialog", typeof(RectTransform), typeof(Image));
            modal = overlay;
            overlay.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0, .12f, .22f, .65f);
            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(EggCatcherSafeArea));
            safe.transform.SetParent(overlay.transform, false);
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(safe.transform, false);
            var cardRect = (RectTransform)card.transform;
            cardRect.sizeDelta = new Vector2(920, height);
            var image = card.GetComponent<Image>();
            image.sprite = Get<Image>("CreateProfilePanel", "Panel").sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            MakeLabel("Title", card.transform, title, new Vector2(0, height / 2 - 100), new Vector2(820, 100), 65);
            return cardRect;
        }

        private void ShowNotice(string title, string message, string closeLabel = "OK")
        {
            var card = CreateModal(title, 600);
            MakeLabel("Message", card, message, new Vector2(0, 10), new Vector2(800, 190), 43);
            MakeButton("Close", card, closeLabel, new Vector2(0, -185), new Vector2(740, 135), CloseModal);
        }

        private void ShowSettings()
        {
            RefreshSettings();
            ShowScreen("SettingPanel");
        }

        private Text MakeLabel(string name, Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = go.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = Ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        private Button MakeButton(string name, Transform parent, string label, Vector2 position, Vector2 size, UnityAction action)
        {
            var button = Instantiate(Get<Button>("CreateProfilePanel", "StartBtn"), parent);
            button.name = name;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
            button.onClick.AddListener(() => gameAudio?.PlayUi());
            button.interactable = true;
            var text = button.GetComponentInChildren<Text>();
            text.text = label;
            text.fontSize = 53;
            text.color = Color.white;
            button.gameObject.SetActive(true);
            return button;
        }

        private void CloseModal()
        {
            if (modal != null) { modal.SetActive(false); Destroy(modal); }
            modal = null;
        }
    }
}
