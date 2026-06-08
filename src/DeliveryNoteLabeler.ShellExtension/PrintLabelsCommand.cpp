#include "PrintLabelsCommand.h"

static long g_moduleRefCount = 0;
HMODULE g_module = nullptr;

static void DllAddRef()
{
    InterlockedIncrement(&g_moduleRefCount);
}

static void DllRelease()
{
    InterlockedDecrement(&g_moduleRefCount);
}

class PrintLabelsExplorerCommand final : public IExplorerCommand
{
public:
    PrintLabelsExplorerCommand() : refCount_(1)
    {
        DllAddRef();
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv)
        {
            return E_POINTER;
        }

        *ppv = nullptr;
        if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, __uuidof(IExplorerCommand)))
        {
            *ppv = static_cast<IExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return static_cast<ULONG>(InterlockedIncrement(&refCount_));
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG count = static_cast<ULONG>(InterlockedDecrement(&refCount_));
        if (count == 0)
        {
            delete this;
        }

        return count;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* name) override
    {
        return SHStrDupW(L"Print Labels", name);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* icon) override
    {
        return SHStrDupW(GetHostIconPath().c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* infoTip) override
    {
        *infoTip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* commandName) override
    {
        if (!commandName)
        {
            return E_POINTER;
        }

        *commandName = CLSID_PrintLabelsCommand;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL okToBeSlow, EXPCMDSTATE* commandState) override
    {
        if (!commandState)
        {
            return E_POINTER;
        }

        if (!okToBeSlow)
        {
            *commandState = ECS_DISABLED;
            return E_PENDING;
        }

        std::vector<std::wstring> paths;
        const bool hasPaths = CollectPdfPaths(items, paths);
        if (hasPaths)
        {
            StoreCachedSelection(paths);
        }

        *commandState = hasPaths ? ECS_ENABLED : ECS_HIDDEN;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        std::vector<std::wstring> paths;
        if (!ResolvePdfPaths(items, paths))
        {
            return E_FAIL;
        }

        return LaunchLabeler(paths) ? S_OK : HRESULT_FROM_WIN32(GetLastError());
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (!flags)
        {
            return E_POINTER;
        }

        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** enumCommands) override
    {
        if (enumCommands)
        {
            *enumCommands = nullptr;
        }

        return E_NOTIMPL;
    }

private:
    ~PrintLabelsExplorerCommand()
    {
        DllRelease();
    }

    long refCount_;
};

class PrintLabelsClassFactory final : public IClassFactory
{
public:
    PrintLabelsClassFactory() : refCount_(1)
    {
        DllAddRef();
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv)
        {
            return E_POINTER;
        }

        *ppv = nullptr;
        if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IClassFactory))
        {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return static_cast<ULONG>(InterlockedIncrement(&refCount_));
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG count = static_cast<ULONG>(InterlockedDecrement(&refCount_));
        if (count == 0)
        {
            delete this;
        }

        return count;
    }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override
    {
        if (outer)
        {
            return CLASS_E_NOAGGREGATION;
        }

        PrintLabelsExplorerCommand* command = new (std::nothrow) PrintLabelsExplorerCommand();
        if (!command)
        {
            return E_OUTOFMEMORY;
        }

        const HRESULT hr = command->QueryInterface(riid, ppv);
        command->Release();
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock)
        {
            DllAddRef();
        }
        else
        {
            DllRelease();
        }

        return S_OK;
    }

private:
    using PFNCREATEINSTANCE = HRESULT(WINAPI*)(REFIID, void**);

    ~PrintLabelsClassFactory()
    {
        DllRelease();
    }

    long refCount_;
};

extern "C" HRESULT STDAPICALLTYPE DllGetClassObject(REFCLSID clsid, REFIID riid, void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }

    *ppv = nullptr;
    if (!IsEqualCLSID(clsid, CLSID_PrintLabelsCommand))
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    PrintLabelsClassFactory* factory = new (std::nothrow) PrintLabelsClassFactory();
    if (!factory)
    {
        return E_OUTOFMEMORY;
    }

    const HRESULT hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}

extern "C" HRESULT STDAPICALLTYPE DllCanUnloadNow()
{
    return g_moduleRefCount == 0 ? S_OK : S_FALSE;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }

    return TRUE;
}
