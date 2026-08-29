#define WIN32_LEAN_AND_MEAN
#include <windows.h>

static void append_ascii(char *dst, int cap, int *pos, const char *src)
{
    while (*src && *pos < cap - 1) dst[(*pos)++] = *src++;
    dst[*pos] = 0;
}

static void append_utf8(char *dst, int cap, int *pos, const wchar_t *src)
{
    if (*pos >= cap - 1) return;
    int avail = cap - *pos - 1;
    int n = WideCharToMultiByte(CP_UTF8, 0, src, -1, dst + *pos, avail, 0, 0);
    if (n > 0) *pos += n - 1;
    dst[*pos] = 0;
}

static void write_marker(const wchar_t *file_name, const char *phase)
{
    wchar_t exe[32768];
    DWORD n = GetModuleFileNameW(NULL, exe, (DWORD)(sizeof(exe) / sizeof(exe[0])));
    if (n == 0 || n >= (DWORD)(sizeof(exe) / sizeof(exe[0]) - 1)) return;

    wchar_t *slash = exe + n;
    while (slash > exe && slash[-1] != L'\\' && slash[-1] != L'/') slash--;
    *slash = 0;

    wchar_t marker[32768];
    lstrcpyW(marker, exe);
    lstrcatW(marker, file_name);

    HANDLE h = CreateFileW(marker, GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                           NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) return;

    wchar_t full_exe[32768];
    DWORD full_n = GetModuleFileNameW(NULL, full_exe, (DWORD)(sizeof(full_exe) / sizeof(full_exe[0])));
    if (full_n == 0) full_exe[0] = 0;

    char text[65536];
    int pos = 0;
    text[0] = 0;
    append_ascii(text, (int)sizeof(text), &pos, "NSC UltimateStormAPI runtime probe\r\nphase=");
    append_ascii(text, (int)sizeof(text), &pos, phase);
    append_ascii(text, (int)sizeof(text), &pos, "\r\nexe=");
    append_utf8(text, (int)sizeof(text), &pos, full_exe);
    append_ascii(text, (int)sizeof(text), &pos, "\r\n");

    DWORD written = 0;
    WriteFile(h, text, (DWORD)pos, &written, NULL);
    FlushFileBuffers(h);
    CloseHandle(h);
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(instance);
        write_marker(L"nsc_api_probe_dllmain.txt", "DllMain");
    }
    return TRUE;
}

__declspec(dllexport) void InitializePlugin(void)
{
    write_marker(L"nsc_api_probe_initialized.txt", "InitializePlugin");
}

/* Export the optional UltimateStormAPI plugin hooks as harmless no-ops.
   This keeps the probe compatible with loaders that enumerate the same
   interface as bundled plugins such as CPKLoader.dll. */
__declspec(dllexport) void GameLoop(void) {}
__declspec(dllexport) void InitializeCommands(void) {}
__declspec(dllexport) void InitializeHooks(void) {}
__declspec(dllexport) void InitializeLuaCommands(void) {}
__declspec(dllexport) void ParseApiFiles(void) {}
