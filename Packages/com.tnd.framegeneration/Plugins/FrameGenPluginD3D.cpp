// Unity Native Plugin API copyright © 2015 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.

// Unity native plugin API
// Compatible with C99

#if defined(__CYGWIN32__)
#define UNITY_INTERFACE_API __stdcall
#define UNITY_INTERFACE_EXPORT __declspec(dllexport)
#elif defined(WIN32) || defined(_WIN32) || defined(__WIN32__) || defined(_WIN64) || defined(WINAPI_FAMILY)
#define UNITY_INTERFACE_API __stdcall
#define UNITY_INTERFACE_EXPORT __declspec(dllexport)
#elif defined(__MACH__) || defined(__ANDROID__) || defined(__linux__) || defined(LUMIN)
#define UNITY_INTERFACE_API
#define UNITY_INTERFACE_EXPORT __attribute__ ((visibility ("default")))
#else
#define UNITY_INTERFACE_API
#define UNITY_INTERFACE_EXPORT
#endif

// This plugin doesn't actually do anything.
// It only exists to force Unity into writing an entry into the list of pre-loaded plugins at startup.
// The actual plugin is a .dll/.so with the same name as this .cpp file, which is *not* set to Load at Startup in the Unity Editor.
// Using this dummy .cpp file ensures that the actual plugin gets loaded at startup in standalone builds, but its file doesn't get locked by the editor.
