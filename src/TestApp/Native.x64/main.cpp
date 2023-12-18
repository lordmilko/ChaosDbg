#include <functional>
#include <Windows.h>
#include <atlbase.h>
#include "Com.h"

template <typename T>
HRESULT Test(std::function<HRESULT(T*)> callback)
{
    CComPtr<T> ptr;
    IfFailRet(FakeCoCreateInstance(&ptr));

    IfFailRet(callback(ptr));

    return S_OK;
}

enum class NativeTestType
{
    Com = 1
};

LPWSTR g_EventName = nullptr;

void BindLifetimeToParentProcess();

int wmain(int argc, wchar_t* argv[])
{
    if (argc < 3)
        return E_FAIL;

    CoInitialize(NULL);

    NativeTestType type = (NativeTestType) _wtoi(argv[1]);
    g_EventName = argv[2];

    BindLifetimeToParentProcess();

    printf("main thread: %d\n", GetCurrentThreadId());

    IfFailRet(Test<IExample>([](IExample* ptr) {
        //Doesn't seem like breakpoints in lambdas work

        //It is recommended to use _beginthreadex over CreateThread
        HANDLE hThread = (HANDLE) _beginthreadex(
            nullptr,
            0,
            [](void* p) -> unsigned int {
                printf("remote thread: %d\n", GetCurrentThreadId());

                ((IExample*)p)->Signal(g_EventName);
                return 0;
            },
            ptr,
            0,
            0
        );

        WaitForSingleObject(hThread, INFINITE);

        return 0;
    }));

    return S_OK;
}

void BindLifetimeToParentProcess()
{
    WCHAR buffer[10];

    if (GetEnvironmentVariable(L"CHAOSDBG_TEST_PARENT_PID", buffer, ARRAYSIZE(buffer)))
    {
        int parentPid = _wtoi(buffer);

        HANDLE hProcess = OpenProcess(SYNCHRONIZE, FALSE, parentPid);

        if (hProcess)
        {
            _beginthreadex(
                nullptr,
                0,
                [](void* p) -> unsigned int {
                    WaitForSingleObject(p, INFINITE);
                    CloseHandle(p);

                    ExitProcess(0);
                },
                hProcess,
                0,
                0
            );
        }
    }
}