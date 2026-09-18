// Managed substitutes for the Unity types needed by geometry and NPC input.
// These do not simulate Unity physics or the PlayerController state machine.
namespace UnityEngine;

public class ScriptableObject { }
public enum FindObjectsSortMode { None }
public class GameObject { public T GetComponent<T>() where T : class => null; }
public class Transform { public Vector3 position; }
public class MonoBehaviour
{
    public GameObject gameObject = new();
    public Transform transform = new();
    protected static T[] FindObjectsByType<T>(FindObjectsSortMode mode) => Array.Empty<T>();
}
[AttributeUsage(AttributeTargets.Class)]
public sealed class CreateAssetMenuAttribute : Attribute
{
    public string fileName;
    public string menuName;
}
public struct Vector2
{
    public float x, y;
    public Vector2(float x, float y) { this.x = x; this.y = y; }
    public static Vector2 zero => new Vector2(0f, 0f);
    public float sqrMagnitude => x * x + y * y;
    public float magnitude => MathF.Sqrt(sqrMagnitude);
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
    public static Vector2 operator *(Vector2 a, float b) => new(a.x * b, a.y * b);
    public static Vector2 operator /(Vector2 a, float b) => new(a.x / b, a.y / b);
    public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
    public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
    public override string ToString() => $"({x:0.###}, {y:0.###})";
}
public struct Vector3
{
    public float x, y, z;
    public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    public static bool operator ==(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
    public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
    public override bool Equals(object obj) => obj is Vector3 other && this == other;
    public override int GetHashCode() => HashCode.Combine(x, y, z);
}
public static class Mathf
{
    public static float Abs(float value) => MathF.Abs(value);
    public static int Abs(int value) => Math.Abs(value);
    public static float Min(float a, float b) => MathF.Min(a, b);
    public static int Min(int a, int b) => Math.Min(a, b);
    public static float Max(float a, float b) => MathF.Max(a, b);
    public static int Max(int a, int b) => Math.Max(a, b);
    public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
    public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    public static float Sqrt(float value) => MathF.Sqrt(value);
    public static int CeilToInt(float value) => (int)MathF.Ceiling(value);
    public static float Sign(float value) => value >= 0 ? 1f : -1f;
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    public static bool Approximately(float a, float b) =>
        Abs(b - a) < Max(0.000001f * Max(Abs(a), Abs(b)), float.Epsilon * 8f);
}

