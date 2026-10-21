#include <windows.h>

#include <filesystem>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    using UnityMainFunction = int(WINAPI *)(HINSTANCE, const wchar_t *, wchar_t *, int);

    HMODULE g_frameGenerationProvider = nullptr;
    HMODULE g_unityPlayer = nullptr;

    void ReportFailure(const wchar_t *message)
    {
        OutputDebugStringW(message);
    }

    int LaunchUnity(
        HINSTANCE instance,
        const wchar_t *productName,
        wchar_t *commandLine,
        int showCommand)
    {
        wchar_t executablePathBuffer[MAX_PATH] = {};
        if (GetModuleFileNameW(nullptr, executablePathBuffer, MAX_PATH) == 0)
        {
            ReportFailure(L"[Frame Generation] Could not resolve the player directory.\n");
            return -1;
        }

        wchar_t proxyPathBuffer[MAX_PATH] = {};
        if (
            GetModuleFileNameW(
                reinterpret_cast<HMODULE>(&__ImageBase),
                proxyPathBuffer,
                MAX_PATH)
            == 0)
        {
            ReportFailure(L"[Frame Generation] Could not resolve the bootstrap location.\n");
            return -1;
        }

        const std::filesystem::path executablePath(executablePathBuffer);
        const std::filesystem::path playerDirectory =
            std::filesystem::path(proxyPathBuffer).parent_path();
        const std::filesystem::path dataDirectory =
            playerDirectory / (executablePath.stem().wstring() + L"_Data");
        const std::filesystem::path providerPath =
            dataDirectory / L"Plugins" / L"x86_64" / L"DLSSFrameGenPluginD3D.dll";

        g_frameGenerationProvider = LoadLibraryExW(
            providerPath.c_str(),
            nullptr,
            LOAD_WITH_ALTERED_SEARCH_PATH);
        if (g_frameGenerationProvider == nullptr)
        {
            ReportFailure(L"[Frame Generation] Could not preload the DLSS provider.\n");
        }

        const std::filesystem::path unityPlayerPath = playerDirectory / L"UnityPlayerCore.dll";
        g_unityPlayer = LoadLibraryExW(
            unityPlayerPath.c_str(),
            nullptr,
            LOAD_WITH_ALTERED_SEARCH_PATH);
        if (g_unityPlayer == nullptr)
        {
            ReportFailure(L"[Frame Generation] Could not load UnityPlayerCore.dll.\n");
            return -1;
        }

        const auto unityMain = reinterpret_cast<UnityMainFunction>(
            GetProcAddress(g_unityPlayer, "UnityMain2"));
        if (unityMain == nullptr)
        {
            ReportFailure(L"[Frame Generation] UnityPlayerCore.dll does not export UnityMain2.\n");
            return -1;
        }

        return unityMain(instance, productName, commandLine, showCommand);
    }
}

extern "C" __declspec(dllexport) int WINAPI UnityMain(
    HINSTANCE instance,
    HINSTANCE,
    wchar_t *commandLine,
    int showCommand)
{
    return LaunchUnity(instance, nullptr, commandLine, showCommand);
}

extern "C" __declspec(dllexport) int WINAPI UnityMain2(
    HINSTANCE instance,
    const wchar_t *productName,
    wchar_t *commandLine,
    int showCommand)
{
    return LaunchUnity(instance, productName, commandLine, showCommand);
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
        DisableThreadLibraryCalls(instance);

    return TRUE;
}
