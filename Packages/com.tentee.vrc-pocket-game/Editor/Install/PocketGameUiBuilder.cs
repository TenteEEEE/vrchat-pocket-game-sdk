using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

namespace VrcPocketGame.Editor
{
    /// <summary>Editor-only UI primitives. Layout is in canvas pixels, never world metres.</summary>
    public sealed class PocketGameTheme
    {
        public TMP_FontAsset Font;
        public Color Background = new Color32(17, 25, 39, 255);
        public Color Panel = new Color32(28, 42, 60, 255);
        public Color Button = new Color32(46, 67, 88, 255);
        public Color Accent = new Color32(85, 220, 179, 255);
        public Color Text = new Color32(235, 243, 250, 255);
        public Color Muted = new Color32(160, 181, 197, 255);
    }

    public static class PocketGameUiBuilder
    {
        public static Canvas Canvas(Transform parent, string name, Vector2 size, Vector3 position, float pixelScale = .001f)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localScale = Vector3.one * pixelScale;
            ((RectTransform)root.transform).sizeDelta = size;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<VRCUiShape>();
            var collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(size.x, size.y, 6f);
            return canvas;
        }

        public static RectTransform Slot(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        public static Image Panel(Transform parent, string name, Vector2 size, Vector2 position, Color color, bool blockRaycasts = false)
        {
            var rect = Slot(parent, name, size, position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blockRaycasts;
            return image;
        }

        public static TMP_Text Text(Transform parent, string name, string text, Vector2 size, Vector2 position,
            float fontSize, PocketGameTheme theme, bool muted = false, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rect = Slot(parent, name, size, position);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = theme.Font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = muted ? theme.Muted : theme.Text;
            label.alignment = align;
            label.raycastTarget = false;
            label.enableWordWrapping = true;
            return label;
        }

        public static Button Button(Transform parent, string label, Vector2 size, Vector2 position,
            UdonBehaviour target, string eventName, PocketGameTheme theme, bool accent = false)
        {
            var image = Panel(parent, label, size, position, accent ? theme.Accent : theme.Button, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(.85f, 1f, .96f);
            colors.pressedColor = new Color(.55f, .8f, .75f);
            button.colors = colors;
            var text = Text(image.transform, "Label", label, size - new Vector2(12, 6), Vector2.zero,
                19, theme, false, TextAlignmentOptions.Center);
            if (accent) text.color = theme.Background;
            UnityEventTools.AddStringPersistentListener(button.onClick, target.SendCustomEvent, eventName);
            return button;
        }
    }
}
