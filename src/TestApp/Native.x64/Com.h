#pragma once

//23FC427E-088C-472A-BD08-F8185AFDF3BD
const CLSID CLSID_Example = { 0x23FC427E, 0x088C, 0x472A, { 0xBD, 0x08, 0xF8, 0x18, 0x5A, 0xFD, 0xF3, 0xBD } };

MIDL_INTERFACE("C9BB051A-4842-4377-943B-3905A6047E5E")
IExample : public IUnknown
{
public:
    virtual void STDMETHODCALLTYPE Signal(LPCWSTR eventName) = 0;
};

#define IfFailRet(hr) if (FAILED(hr)) \
    return hr

template <typename T>
HRESULT FakeCoCreateInstance(T** ppv)
{
    return CoCreateInstance(CLSID_Example, NULL, CLSCTX_INPROC, __uuidof(T), (void**)ppv);
}