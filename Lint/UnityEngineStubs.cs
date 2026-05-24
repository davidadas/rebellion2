using System;
using System.Collections;

namespace UnityEngine
{
    public class Object
    {
        public static void DontDestroyOnLoad(Object target)
        {
            _ = target;
        }

        public static void Destroy(Object obj)
        {
            _ = obj;
        }

        public static bool operator ==(Object left, Object right)
        {
            return ReferenceEquals(left, right);
        }

        public static bool operator !=(Object left, Object right)
        {
            return !ReferenceEquals(left, right);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; set; }

        public Transform transform { get; set; }
    }

    public class Behaviour : Component { }

    public class MonoBehaviour : Behaviour
    {
        protected Coroutine StartCoroutine(IEnumerator routine)
        {
            _ = routine;
            return null;
        }

        protected void StopAllCoroutines() { }
    }

    public class GameObject : Object
    {
        public Transform transform { get; set; }
    }

    public class Transform : Component
    {
        public Transform parent { get; set; }

        public void SetParent(Transform parent)
        {
            _ = parent;
        }

        public void SetParent(Transform parent, bool worldPositionStays)
        {
            _ = parent;
            _ = worldPositionStays;
        }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }

        public Vector2 anchorMax { get; set; }

        public Vector2 offsetMin { get; set; }

        public Vector2 offsetMax { get; set; }

        public Vector2 anchoredPosition { get; set; }

        public Vector2 sizeDelta { get; set; }

        public Vector2 pivot { get; set; }

        public Vector3 localScale { get; set; }

        public Rect rect { get; set; }
    }

    public class Coroutine { }

    public class AudioClip : Object { }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }

        public bool loop { get; set; }

        public float volume { get; set; }

        public bool isPlaying { get; set; }

        public void Play() { }

        public void Stop() { }

        public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
        {
            _ = clip;
            _ = volumeScale;
        }
    }

    public class Sprite : Object { }

    public class TextAsset : Object
    {
        public string text { get; set; }

        public byte[] bytes { get; set; }
    }

    public struct Rect
    {
        public float width { get; set; }

        public float height { get; set; }

        public Vector2 min { get; set; }

        public Vector2 max { get; set; }
    }

    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => new Vector2(0f, 0f);

        public static Vector2 one => new Vector2(1f, 1f);
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z = 0f)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 one => new Vector3(1f, 1f, 1f);
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header)
        {
            _ = header;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max)
        {
            _ = min;
            _ = max;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType)
        {
            _ = loadType;
        }
    }

    public enum RuntimeInitializeLoadType
    {
        AfterAssembliesLoaded,
    }

    public static class Application
    {
        public static string persistentDataPath { get; set; } = string.Empty;

        public static void Quit(int exitCode)
        {
            _ = exitCode;
        }
    }

    public static class Debug
    {
        public static void Log(object message)
        {
            _ = message;
        }

        public static void LogWarning(object message)
        {
            _ = message;
        }

        public static void LogError(object message)
        {
            _ = message;
        }

        public static void LogException(Exception exception)
        {
            _ = exception;
        }
    }

    public static class JsonUtility
    {
        public static T FromJson<T>(string json)
        {
            _ = json;
            return default;
        }

        public static string ToJson(object obj, bool prettyPrint = false)
        {
            _ = obj;
            _ = prettyPrint;
            return string.Empty;
        }
    }

    public static class Mathf
    {
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }

    public static class Resources
    {
        public static T Load<T>(string path)
            where T : Object
        {
            _ = path;
            return null;
        }

        public static T[] LoadAll<T>(string path)
            where T : Object
        {
            _ = path;
            return Array.Empty<T>();
        }
    }

    public static class Time
    {
        public static float unscaledDeltaTime { get; set; }
    }
}

namespace UnityEngine.Video
{
    public class VideoClip : UnityEngine.Object { }
}
