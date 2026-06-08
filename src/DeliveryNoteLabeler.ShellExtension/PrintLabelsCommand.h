#pragma once

#include <windows.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <strsafe.h>
#include <new>
#include <string>
#include <vector>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "ole32.lib")

extern HMODULE g_module;

// {E7B4C9A2-6F1D-4E8B-9C3A-2D5F8E1B4C07}
extern "C" const CLSID CLSID_PrintLabelsCommand =
{ 0xe7b4c9a2, 0x6f1d, 0x4e8b, { 0x9c, 0x3a, 0x2d, 0x5f, 0x8e, 0x1b, 0x4c, 0x07 } };

template <typename T>
void SafeRelease(T** ppT)
{
    if (*ppT)
    {
        (*ppT)->Release();
        *ppT = nullptr;
    }
}

inline bool EndsWithPdf(PCWSTR path)
{
    if (!path)
    {
        return false;
    }

    const size_t length = wcslen(path);
    if (length < 4)
    {
        return false;
    }

    return _wcsicmp(path + length - 4, L".pdf") == 0;
}

inline bool GetHostInstallDirectory(wchar_t* directory, DWORD directoryLength)
{
    if (!directory || directoryLength == 0)
    {
        return false;
    }

    directory[0] = L'\0';
    if (g_module)
    {
        GetModuleFileNameW(g_module, directory, directoryLength);
    }
    else if (!GetModuleFileNameW(GetModuleHandleW(L"DeliveryNoteLabelerShell.dll"), directory, directoryLength))
    {
        return false;
    }

    PathRemoveFileSpecW(directory);
    return directory[0] != L'\0';
}

inline std::wstring GetHostExecutablePath()
{
    wchar_t modulePath[MAX_PATH] = {};
    if (!GetHostInstallDirectory(modulePath, ARRAYSIZE(modulePath)))
    {
        return L"DeliveryNoteLabeler.exe";
    }

    std::wstring exePath = modulePath;
    exePath += L"\\DeliveryNoteLabeler.exe";
    return exePath;
}

inline std::wstring GetHostIconPath()
{
    wchar_t modulePath[MAX_PATH] = {};
    if (!GetHostInstallDirectory(modulePath, ARRAYSIZE(modulePath)))
    {
        return GetHostExecutablePath() + L",0";
    }

    std::wstring iconPath = modulePath;
    iconPath += L"\\DeliveryNoteLabeler.ico";
    if (GetFileAttributesW(iconPath.c_str()) != INVALID_FILE_ATTRIBUTES)
    {
        return iconPath;
    }

    return GetHostExecutablePath() + L",0";
}

inline bool CollectPdfPaths(IShellItemArray* items, std::vector<std::wstring>& paths)
{
    paths.clear();
    if (!items)
    {
        return false;
    }

    DWORD count = 0;
    if (FAILED(items->GetCount(&count)))
    {
        return false;
    }

    for (DWORD index = 0; index < count; ++index)
    {
        IShellItem* item = nullptr;
        if (FAILED(items->GetItemAt(index, &item)))
        {
            continue;
        }

        PWSTR path = nullptr;
        const HRESULT pathResult = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
        if (FAILED(pathResult) || !path)
        {
            if (path)
            {
                CoTaskMemFree(path);
                path = nullptr;
            }

            if (FAILED(item->GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, &path)) || !path)
            {
                if (path)
                {
                    CoTaskMemFree(path);
                }

                continue;
            }
        }

        if (EndsWithPdf(path))
        {
            paths.emplace_back(path);
        }

        CoTaskMemFree(path);

        item->Release();
    }

    return !paths.empty();
}

namespace
{
CRITICAL_SECTION g_selectionLock;
std::vector<std::wstring> g_cachedSelection;
bool g_selectionLockReady = false;

void EnsureSelectionLock()
{
    if (!g_selectionLockReady)
    {
        InitializeCriticalSection(&g_selectionLock);
        g_selectionLockReady = true;
    }
}

inline void StoreCachedSelection(const std::vector<std::wstring>& paths)
{
    EnsureSelectionLock();
    EnterCriticalSection(&g_selectionLock);
    g_cachedSelection = paths;
    LeaveCriticalSection(&g_selectionLock);
}

inline bool LoadCachedSelection(std::vector<std::wstring>& paths)
{
    EnsureSelectionLock();
    EnterCriticalSection(&g_selectionLock);
    paths = g_cachedSelection;
    LeaveCriticalSection(&g_selectionLock);
    return !paths.empty();
}

inline bool ResolvePdfPaths(IShellItemArray* items, std::vector<std::wstring>& paths)
{
    if (CollectPdfPaths(items, paths))
    {
        StoreCachedSelection(paths);
        return true;
    }

    return LoadCachedSelection(paths);
}

inline bool WritePathListFile(const std::vector<std::wstring>& paths, std::wstring& outPath)
{
    wchar_t tempDir[MAX_PATH] = {};
    if (GetTempPathW(ARRAYSIZE(tempDir), tempDir) == 0)
    {
        return false;
    }

    wchar_t tempFile[MAX_PATH] = {};
    if (GetTempFileNameW(tempDir, L"DNL", 0, tempFile) == 0)
    {
        return false;
    }

    DeleteFileW(tempFile);

    outPath = tempFile;
    outPath += L".pdflist";

    HANDLE file = CreateFileW(
        outPath.c_str(),
        GENERIC_WRITE,
        FILE_SHARE_READ,
        nullptr,
        CREATE_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    if (file == INVALID_HANDLE_VALUE)
    {
        return false;
    }

    for (const auto& pdfPath : paths)
    {
        const int utf8Length = WideCharToMultiByte(CP_UTF8, 0, pdfPath.c_str(), -1, nullptr, 0, nullptr, nullptr);
        if (utf8Length <= 1)
        {
            continue;
        }

        std::string utf8(static_cast<size_t>(utf8Length - 1), '\0');
        WideCharToMultiByte(CP_UTF8, 0, pdfPath.c_str(), -1, utf8.data(), utf8Length, nullptr, nullptr);
        utf8.push_back('\n');

        DWORD written = 0;
        WriteFile(file, utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr);
    }

    CloseHandle(file);
    return true;
}
}

inline bool LaunchLabeler(const std::vector<std::wstring>& paths)
{
    if (paths.empty())
    {
        return false;
    }

    const std::wstring exePath = GetHostExecutablePath();
    if (GetFileAttributesW(exePath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        return false;
    }

    std::wstring listFile;
    if (!WritePathListFile(paths, listFile))
    {
        return false;
    }

    std::wstring commandLine = L"\"" + exePath + L"\" --open-from \"" + listFile + L"\"";

    STARTUPINFOW startupInfo = { sizeof(startupInfo) };
    startupInfo.dwFlags = STARTF_USESHOWWINDOW;
    startupInfo.wShowWindow = SW_SHOWNORMAL;

    PROCESS_INFORMATION processInfo = {};
    std::vector<wchar_t> buffer(commandLine.begin(), commandLine.end());
    buffer.push_back(L'\0');

    if (!CreateProcessW(
            nullptr,
            buffer.data(),
            nullptr,
            nullptr,
            FALSE,
            0,
            nullptr,
            nullptr,
            &startupInfo,
            &processInfo))
    {
        DeleteFileW(listFile.c_str());
        return false;
    }

    CloseHandle(processInfo.hThread);
    CloseHandle(processInfo.hProcess);
    return true;
}
