// A metadata-only loader fixture. Not shipped in the release.
extern "C" __declspec(dllexport) bool ReShadeRegisterAddon(void *, unsigned) { return false; }
extern "C" __declspec(dllexport) void ReShadeUnregisterAddon(void *) {}
extern "C" __declspec(dllexport) void ReShadeRegisterEventForAddon(void *, unsigned, void *) {}
