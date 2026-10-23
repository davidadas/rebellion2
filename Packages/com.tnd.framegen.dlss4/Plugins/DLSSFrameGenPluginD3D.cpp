// Unity Native Plugin API copyright © 2015 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses).
//
// Unless expressly provided otherwise, software distributed under this license is provided "AS IS"
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND. See the license for the governing permissions and limitations.

#if defined(__CYGWIN32__)
#define UNITY_INTERFACE_API __stdcall
#define UNITY_INTERFACE_EXPORT __declspec(dllexport)
#elif defined(WIN32) || defined(_WIN32) || defined(__WIN32__) || defined(_WIN64) || defined(WINAPI_FAMILY)
#define UNITY_INTERFACE_API __stdcall
#define UNITY_INTERFACE_EXPORT __declspec(dllexport)
#elif defined(__MACH__) || defined(__ANDROID__) || defined(__linux__) || defined(LUMIN)
#define UNITY_INTERFACE_API
#define UNITY_INTERFACE_EXPORT __attribute__((visibility("default")))
#else
#define UNITY_INTERFACE_API
#define UNITY_INTERFACE_EXPORT
#endif

// This source plugin intentionally has no executable code. Its matching filename registers the
// provider DLL in Unity's preloaded-plugin list for standalone builds without locking it in the Editor.
