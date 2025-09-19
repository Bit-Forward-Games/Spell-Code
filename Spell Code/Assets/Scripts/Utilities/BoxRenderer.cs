using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public static class BoxRenderer
{
    // ===== | Variables | =====
    public static bool RenderBoxes = false;
    private static Texture2D _texture;

    public const bool UseNewInputs = true;
    public static InputDevice player1Device;
    public static InputDevice player2Device;

    static BoxRenderer()
    {
        //Debug.Log("BoxRenderer initialized");
        _texture = new Texture2D(1, 1);
        _texture.SetPixel(0, 0, Color.white);
        _texture.Apply();
    }

    // ===== | Methods | =====
    public static void DrawBox(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, _texture);
        GUI.color = Color.white;
    }

    public static void DrawBox(Vector2 position, float width, float height, Color color)
    {
        // Convert world space position and dimensions to screen space
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(position);
        screenPosition.y = Screen.height - screenPosition.y;  // Adjust for GUI's top-left origin

        // Convert world space size to screen space
        Vector3 screenBottomRight = Camera.main.WorldToScreenPoint(new Vector3(position.x, position.y) + new Vector3(width, -height, 0));
        screenBottomRight.y = Screen.height - screenBottomRight.y;

        // Calculate screen-space width and height
        float screenWidth = Mathf.Abs(screenBottomRight.x - screenPosition.x);
        float screenHeight = Mathf.Abs(screenBottomRight.y - screenPosition.y);

        // Set color and draw the texture
        GUI.color = color;
        Rect rect = new Rect(screenPosition.x, screenPosition.y, screenWidth, screenHeight);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        // Reset GUI color
        GUI.color = Color.white;
    }
}
