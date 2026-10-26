using HarmonyLib;
using UnityEngine;

namespace PathOfKungfuPlugin;

/// <summary>
/// The item tooltip is placed so its top edge sits at the trigger's y, or, when the panel is taller than the space below that, at trigger y + panel height
/// (so it grows upward). English text wraps to more lines than Chinese, so a long tooltip (a skill book's chapter list) runs off the top of the screen.
/// After the game positions it, shift it down until its top is on screen (and back up if that pushes the bottom off).
/// </summary>
[HarmonyPatch(typeof(TipShowWinNew), nameof(TipShowWinNew.SetUiData))]
internal static class TipScreenClampPatch
{
    private static readonly Vector3[] Corners = new Vector3[4];

    private static void Postfix(TipShowWinNew __instance, GameObject ___panel_tip1, GameObject ___panel_tipRight)
    {
        if (___panel_tip1 == null) return;

        var rect = (RectTransform)___panel_tip1.transform;
        var canvas = rect.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        // Screen-space top and bottom of the panel(s), measured through the canvas so a scaled or camera canvas still works.
        var top = float.MinValue;
        var bottom = float.MaxValue;
        Measure(rect, camera, ref top, ref bottom);
        if (___panel_tipRight != null && ___panel_tipRight.activeInHierarchy)
            Measure((RectTransform)___panel_tipRight.transform, camera, ref top, ref bottom);

        // Lift an off-screen bottom, then pull down an off-screen top: the top wins when the panel is taller than the screen.
        var shift = bottom < 0f ? -bottom : 0f;
        if (top + shift > Screen.height) shift = Screen.height - top;
        if (Mathf.Approximately(shift, 0f)) return;

        var pivot = __instance.transform;
        var screen = RectTransformUtility.WorldToScreenPoint(camera, pivot.position);
        var canvasRect = canvas != null ? (RectTransform)canvas.transform : (RectTransform)pivot;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screen + new Vector2(0f, shift), camera, out var moved);
        pivot.position = new Vector3(pivot.position.x, moved.y, pivot.position.z);
    }

    private static void Measure(RectTransform rect, Camera camera, ref float top, ref float bottom)
    {
        rect.GetWorldCorners(Corners);
        foreach (var corner in Corners)
        {
            var y = RectTransformUtility.WorldToScreenPoint(camera, corner).y;
            if (y > top) top = y;
            if (y < bottom) bottom = y;
        }
    }
}
