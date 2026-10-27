#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <map>
#include <set>
#include <string>
#include <vector>

// ReShade's exported registration API. No graphics callbacks or game-code
// detours are needed: the window's timer runs on Unity's main thread.
extern "C" __declspec(dllexport) const char *NAME = "Trailmakers Centre Screen UI";
extern "C" __declspec(dllexport) const char *DESCRIPTION =
    "Centres HUD, menus, inventory and dialogs without changing the Surround resolution.";

using Method = const void *;
using Object = void *;
struct Vec2 { float x, y; };
struct Rect { float x, y, width, height; };
struct SavedRect { void *handle; Vec2 minimum, maximum; bool seen; };
struct MapProbe { void *handle; Method visible; void *mapUiField; };
struct SavedMapInput { void *handle; Vec2 resolution; bool seen; };

static HMODULE addonModule;
static HWND gameWindow;
static WNDPROC originalWindowProc;
static UINT controlMessage;
static UINT_PTR timerId;
static bool enabled = true, ready, mapWasOpen;
static std::atomic_bool stopped{false};
static wchar_t configPath[MAX_PATH], logPath[MAX_PATH];
static std::map<Object, SavedRect> saved;
static std::vector<MapProbe> mapProbes;
static std::map<Object, SavedMapInput> mapInputs;
static DWORD lastMapScan, lastLayoutScan;
static bool mapCacheReady, layoutScanned;

static void Log(const char *message) {
    FILE *file = nullptr;
    if (_wfopen_s(&file, logPath, L"a") == 0 && file) {
        SYSTEMTIME t{}; GetLocalTime(&t);
        fprintf(file, "%04u-%02u-%02u %02u:%02u:%02u %s\n", t.wYear,
            t.wMonth, t.wDay, t.wHour, t.wMinute, t.wSecond, message);
        fclose(file);
    }
}

struct Il2Cpp {
    void *(*domain_get)();
    void *(*thread_current)();
    void **(*domain_get_assemblies)(void *, size_t *);
    void *(*assembly_get_image)(void *);
    void *(*class_from_name)(void *, const char *, const char *);
    const void *(*class_get_method_from_name)(void *, const char *, int);
    void *(*class_get_field_from_name)(void *, const char *);
    void *(*object_get_class)(Object);
    void (*field_get_value)(Object, void *, void *);
    void (*field_set_value)(Object, void *, void *);
    const void *(*class_get_type)(void *);
    Object (*type_get_object)(const void *);
    Object (*runtime_invoke)(Method, Object, void **, Object *);
    void *(*object_unbox)(Object);
    uint32_t (*array_length)(Object);
    void *(*gchandle_new)(Object, bool);
    Object (*gchandle_get_target)(void *);
    void (*gchandle_free)(void *);
    int32_t (*string_length)(Object);
    const wchar_t *(*string_chars)(Object);
    Object (*string_new)(const char *);

    Object scalerType, mapComponentType, mapViewType;
    void *scalingTransformsField;
    void *listItemsField, *listSizeField;
    void *mapComponentUiField, *mapViewUiField, *mapResolutionField, *mapRectField;
    Method findObjects, getParent, getName, getMin, getMax, setMin, setMax;
    Method getWidth, getHeight, isAlive, mapIsVisible, isActiveAndEnabled, getRect;
} api{};

static Object Invoke(Method method, Object target, void **arguments = nullptr) {
    Object exception = nullptr;
    Object result = api.runtime_invoke(method, target, arguments, &exception);
    if (exception) throw "Unity reported a managed exception; layout adjustment stopped.";
    return result;
}

static void *FindClass(const char *space, const char *name) {
    size_t count = 0;
    auto assemblies = api.domain_get_assemblies(api.domain_get(), &count);
    for (size_t i = 0; i < count; ++i)
        if (void *klass = api.class_from_name(api.assembly_get_image(assemblies[i]), space, name))
            return klass;
    return nullptr;
}

static Method FindMethod(void *klass, const char *name, int count) {
    Method method = klass ? api.class_get_method_from_name(klass, name, count) : nullptr;
    if (!method) throw "A required Unity method was unavailable; no layout change applied.";
    return method;
}

static bool ResolveApi() {
    HMODULE module = GetModuleHandleW(L"GameAssembly.dll");
    if (!module) return false;
#define LOAD_API(member) api.member = reinterpret_cast<decltype(api.member)>(GetProcAddress(module, "il2cpp_" #member)); if (!api.member) return false
    LOAD_API(domain_get); LOAD_API(thread_current); LOAD_API(domain_get_assemblies);
    LOAD_API(assembly_get_image); LOAD_API(class_from_name);
    LOAD_API(class_get_method_from_name); LOAD_API(class_get_field_from_name);
    LOAD_API(object_get_class);
    LOAD_API(field_get_value); LOAD_API(field_set_value); LOAD_API(class_get_type); LOAD_API(type_get_object);
    LOAD_API(runtime_invoke); LOAD_API(object_unbox); LOAD_API(array_length);
    LOAD_API(gchandle_new); LOAD_API(gchandle_get_target); LOAD_API(gchandle_free);
    LOAD_API(string_length); LOAD_API(string_chars);
    LOAD_API(string_new);
#undef LOAD_API
    if (!api.thread_current() || !api.domain_get()) return false;
    void *scaler = FindClass("", "SafeFrameScaler");
    void *object = FindClass("UnityEngine", "Object");
    void *rect = FindClass("UnityEngine", "RectTransform");
    void *screen = FindClass("UnityEngine", "Screen");
    void *transform = FindClass("UnityEngine", "Transform");
    if (!scaler || !object || !rect || !screen || !transform) return false;
    api.scalingTransformsField = api.class_get_field_from_name(scaler, "ScalingTransforms");
    if (!api.scalingTransformsField) throw "SafeFrameScaler.ScalingTransforms was unavailable.";
    api.scalerType = api.type_get_object(api.class_get_type(scaler));
    api.findObjects = FindMethod(object, "FindObjectsOfType", 2);
    api.getParent = FindMethod(transform, "get_parent", 0);
    api.getName = FindMethod(object, "get_name", 0);
    api.isAlive = FindMethod(object, "op_Implicit", 1);
    api.getMin = FindMethod(rect, "get_anchorMin", 0);
    api.getMax = FindMethod(rect, "get_anchorMax", 0);
    api.setMin = FindMethod(rect, "set_anchorMin", 1);
    api.setMax = FindMethod(rect, "set_anchorMax", 1);
    api.getRect = FindMethod(rect, "get_rect", 0);
    api.getWidth = FindMethod(screen, "get_width", 0);
    api.getHeight = FindMethod(screen, "get_height", 0);
    void *mapComponent = FindClass("", "MapUIComponent");
    void *mapView = FindClass("", "MapView");
    void *mapUi = FindClass("", "MapUI");
    void *behaviour = FindClass("UnityEngine", "Behaviour");
    api.isActiveAndEnabled = behaviour ? api.class_get_method_from_name(behaviour, "get_isActiveAndEnabled", 0) : nullptr;
    api.mapIsVisible = mapComponent ? api.class_get_method_from_name(mapComponent, "IsVisible", 0) : nullptr;
    api.mapComponentUiField = mapComponent ? api.class_get_field_from_name(mapComponent, "m_mapUI") : nullptr;
    api.mapViewUiField = mapView ? api.class_get_field_from_name(mapView, "m_mapUI") : nullptr;
    api.mapResolutionField = mapUi ? api.class_get_field_from_name(mapUi, "m_targetResolution") : nullptr;
    api.mapRectField = mapUi ? api.class_get_field_from_name(mapUi, "rectTransform") : nullptr;
    if (api.isActiveAndEnabled) {
        if (mapComponent && api.mapIsVisible)
            api.mapComponentType = api.type_get_object(api.class_get_type(mapComponent));
        if (mapView) api.mapViewType = api.type_get_object(api.class_get_type(mapView));
    }
    Log(api.mapComponentType && api.mapViewType
        ? "Map visibility detection ready for the in-game and menu maps."
        : "Some map visibility metadata is unavailable; automatic full-width maps may be incomplete.");
    if (!api.mapResolutionField || !api.mapRectField || !api.mapComponentUiField || !api.mapViewUiField)
        Log("Some map coordinate metadata is unavailable; beacon alignment may be incomplete.");
    Log("Unity UI methods resolved by name on its window/main thread.");
    return true;
}

static bool Alive(Object object) {
    if (!object) return false;
    void *arguments[] = { object };
    return *static_cast<bool *>(api.object_unbox(Invoke(api.isAlive, nullptr, arguments)));
}

static Vec2 GetVec(Method getter, Object object) {
    return *static_cast<Vec2 *>(api.object_unbox(Invoke(getter, object)));
}

static void SetVec(Method setter, Object object, Vec2 value) {
    void *arguments[] = { &value };
    Invoke(setter, object, arguments);
}

static std::wstring Name(Object object) {
    Object text = Invoke(api.getName, object);
    return text ? std::wstring(api.string_chars(text), api.string_length(text)) : L"";
}

static bool CentreRoot(Object rect, const std::wstring &owner) {
    Object parent = Invoke(api.getParent, rect);
    const std::wstring parentName = parent ? Name(parent) : L"";
    // Keep full-screen effects and world-space widgets at their original size.
    if (owner == L"BuilderUICanvas" || owner == L"WorldIconHandlerUI" ||
        owner == L"Virtual Pointer" || owner == L"PFB_TrailmakersVideoPlayer") return false;
    if (parentName == L"BG canvas" || parentName == L"CanvasBackground" ||
        parentName == L"VideoCanvas" || parentName == L"Background Content Canvas" ||
        parentName == L"PFB_CameraEdge_ImpactEffect") return false;
    if (owner.find(L"Transition") != std::wstring::npos &&
        parentName != L"CanvasContent" && parentName != L"Instructions") return false;
    return true;
}

static void Restore() {
    for (auto &entry : saved) {
        Object rect = api.gchandle_get_target(entry.second.handle);
        if (Alive(rect)) {
            SetVec(api.setMin, rect, entry.second.minimum);
            SetVec(api.setMax, rect, entry.second.maximum);
        }
        api.gchandle_free(entry.second.handle);
    }
    if (!saved.empty()) Log("Original UI anchors restored.");
    saved.clear();
}

static void CollectMaps(Object type, Method visible, void *mapUiField, std::vector<MapProbe> &probes) {
    if (!type) return;
    bool includeInactive = true;
    void *arguments[] = { type, &includeInactive };
    Object array = Invoke(api.findObjects, nullptr, arguments);
    if (!array) return;
    void *handle = api.gchandle_new(array, false);
    try {
        const size_t count = api.array_length(array);
        if (count > 4096) throw "Unexpected map component count; layout adjustment stopped.";
        for (size_t i = 0; i < count; ++i) {
            array = api.gchandle_get_target(handle);
            if (!array) throw "Unity released the temporary map list unexpectedly.";
            Object component = reinterpret_cast<Object *>(array)[4 + i];
            if (!Alive(component)) continue;
            probes.push_back(MapProbe{nullptr, visible, mapUiField});
            probes.back().handle = api.gchandle_new(component, false);
        }
    } catch (...) { api.gchandle_free(handle); throw; }
    api.gchandle_free(handle);
}

static void ClearMapCache() {
    for (auto &probe : mapProbes) if (probe.handle) api.gchandle_free(probe.handle);
    mapProbes.clear();
    mapCacheReady = false;
}

static bool MapVisible(const MapProbe &probe) {
    Object component = api.gchandle_get_target(probe.handle);
    if (!Alive(component) ||
        !*static_cast<bool *>(api.object_unbox(Invoke(api.isActiveAndEnabled, component)))) return false;
    return !probe.visible || *static_cast<bool *>(api.object_unbox(Invoke(probe.visible, component)));
}

static bool MapOpen(DWORD now) {
    // Discover inactive map objects infrequently, then read only their cached
    // visibility on each window tick. Do not scan all Unity objects per frame.
    if (!mapCacheReady || now - lastMapScan >= 500) {
        std::vector<MapProbe> fresh;
        try {
            CollectMaps(api.mapComponentType, api.mapIsVisible, api.mapComponentUiField, fresh);
            CollectMaps(api.mapViewType, nullptr, api.mapViewUiField, fresh);
        } catch (...) {
            for (auto &probe : fresh) if (probe.handle) api.gchandle_free(probe.handle);
            throw;
        }
        ClearMapCache();
        mapProbes.swap(fresh);
        mapCacheReady = true;
        lastMapScan = now;
    }
    for (auto &probe : mapProbes) {
        // The quick map uses canvas visibility, while the menu MapView
        // uses its component lifecycle. Neither depends on a key binding.
        if (MapVisible(probe)) return true;
    }
    return false;
}

static void RestoreMapInputs(bool all = true) {
    for (auto item = mapInputs.begin(); item != mapInputs.end();) {
        if (all || !item->second.seen) {
            Object ui = api.gchandle_get_target(item->second.handle);
            if (Alive(ui)) api.field_set_value(ui, api.mapResolutionField, &item->second.resolution);
            api.gchandle_free(item->second.handle);
            item = mapInputs.erase(item);
        } else ++item;
    }
}

static void SyncMapInputs() {
    if (!api.mapResolutionField || !api.mapRectField) return;
    for (auto &entry : mapInputs) entry.second.seen = false;
    for (auto &probe : mapProbes) {
        if (!probe.mapUiField || !MapVisible(probe)) continue;
        Object ui = nullptr, rect = nullptr;
        api.field_get_value(api.gchandle_get_target(probe.handle), probe.mapUiField, &ui);
        if (!Alive(ui)) continue;
        api.field_get_value(ui, api.mapRectField, &rect);
        if (!Alive(rect)) continue;
        const Rect bounds = *static_cast<Rect *>(api.object_unbox(Invoke(api.getRect, rect)));
        if (!std::isfinite(bounds.width) || !std::isfinite(bounds.height) ||
            bounds.width <= 0 || bounds.height <= 0) continue;
        Vec2 current{};
        api.field_get_value(ui, api.mapResolutionField, &current);
        auto item = mapInputs.find(ui);
        if (item == mapInputs.end()) {
            mapInputs.emplace(ui, SavedMapInput{api.gchandle_new(ui, false), current, true});
            char line[160];
            sprintf_s(line, "Map input frame synchronized from %.1fx%.1f to %.1fx%.1f UI units.",
                current.x, current.y, bounds.width, bounds.height);
            Log(line);
        } else item->second.seen = true;
        // MapUI's screen-to-map conversion uses this stored size. Restoring
        // full-width anchors alone leaves clicks compressed into the middle
        // third. Use local UI units, which also respect the canvas scale.
        if (std::fabs(current.x - bounds.width) > 0.001f ||
            std::fabs(current.y - bounds.height) > 0.001f) {
            Vec2 target{bounds.width, bounds.height};
            api.field_set_value(ui, api.mapResolutionField, &target);
        }
    }
    RestoreMapInputs(false);
}

static void Adjust() {
    int width = *static_cast<int *>(api.object_unbox(Invoke(api.getWidth, nullptr)));
    int height = *static_cast<int *>(api.object_unbox(Invoke(api.getHeight, nullptr)));
    const DWORD now = GetTickCount();
    const bool mapOpen = MapOpen(now);
    const bool mapChanged = mapOpen != mapWasOpen;
    if (mapChanged) {
        Log(mapOpen ? "Map opened; restoring the full-width UI."
            : "Map closed; resuming the saved UI toggle setting.");
        mapWasOpen = mapOpen;
    }
    // Automatic map bypass never writes or overrides the user's F10 choice.
    if (mapOpen) {
        Restore();
        SyncMapInputs();
        layoutScanned = false;
        return;
    }
    RestoreMapInputs();
    if (!enabled || height <= 0 || width <= height * 2.4f) {
        Restore(); layoutScanned = false; return;
    }
    // Map transitions and F10 apply immediately; ordinary discovery of new
    // HUD/menu roots retains the original half-second interval.
    if (!mapChanged && layoutScanned && now - lastLayoutScan < 500) return;
    lastLayoutScan = now;
    layoutScanned = true;
    const float margin = std::max(0.0f, (1.0f - height * (16.0f / 9.0f) / width) * 0.5f);
    const Vec2 minimum{margin, 0}, maximum{1.0f - margin, 1};
    bool includeInactive = true;
    void *arguments[] = { api.scalerType, &includeInactive };
    Object array = Invoke(api.findObjects, nullptr, arguments);
    if (!array) return;
    void *arrayHandle = api.gchandle_new(array, false);
    for (auto &entry : saved) entry.second.seen = false;
    try {
        // Il2CppArray's object header, bounds and length precede its elements.
        const size_t count = static_cast<size_t>(api.array_length(array));
        if (count > 4096) throw "Unexpected UI scaler count; layout adjustment stopped.";
        std::set<Object> roots;
        for (size_t i = 0; i < count; ++i) {
            array = api.gchandle_get_target(arrayHandle);
            if (!array) throw "Unity released the temporary UI list unexpectedly.";
            Object scaler = reinterpret_cast<Object *>(array)[4 + i];
            Object list = nullptr;
            api.field_get_value(scaler, api.scalingTransformsField, &list);
            if (!list) continue;
            if (!api.listItemsField) {
                void *listClass = api.object_get_class(list);
                api.listItemsField = api.class_get_field_from_name(listClass, "_items");
                api.listSizeField = api.class_get_field_from_name(listClass, "_size");
                if (!api.listItemsField || !api.listSizeField)
                    throw "The Unity UI transform list layout was unavailable.";
            }
            Object items = nullptr;
            int size = 0;
            api.field_get_value(list, api.listItemsField, &items);
            api.field_get_value(list, api.listSizeField, &size);
            if (!items || size < 0 || size > 1024 ||
                size > static_cast<int>(api.array_length(items))) continue;
            const std::wstring owner = Name(scaler);
            for (int k = 0; k < size; ++k) {
                Object rect = reinterpret_cast<Object *>(items)[4 + k];
                if (Alive(rect) && CentreRoot(rect, owner)) roots.insert(rect);
            }
        }
        for (Object rect : roots) {
            // A nested canvas inherits its ancestor's central frame. Applying
            // the margin twice would squeeze its UI into one ninth of the span.
            bool nested = false;
            Object parent = Invoke(api.getParent, rect);
            for (int depth = 0; parent && depth < 128; ++depth) {
                if (roots.count(parent)) { nested = true; break; }
                parent = Invoke(api.getParent, parent);
            }
            if (nested) continue;
            Vec2 oldMin = GetVec(api.getMin, rect), oldMax = GetVec(api.getMax, rect);
            auto item = saved.find(rect);
            if (item == saved.end()) {
                saved.emplace(rect, SavedRect{api.gchandle_new(rect, false), oldMin, oldMax, true});
                char line[160];
                sprintf_s(line, "Centred UI root at %dx%d; world camera and resolution unchanged.", width, height);
                Log(line);
            } else item->second.seen = true;
            if (std::fabs(oldMin.x - minimum.x) > 0.0001f ||
                std::fabs(oldMax.x - maximum.x) > 0.0001f) {
                SetVec(api.setMin, rect, minimum);
                SetVec(api.setMax, rect, maximum);
            }
        }
    } catch (...) { api.gchandle_free(arrayHandle); throw; }
    api.gchandle_free(arrayHandle);
    for (auto item = saved.begin(); item != saved.end();) {
        if (!item->second.seen) {
            Object rect = api.gchandle_get_target(item->second.handle);
            if (Alive(rect)) {
                SetVec(api.setMin, rect, item->second.minimum);
                SetVec(api.setMax, rect, item->second.maximum);
            }
            api.gchandle_free(item->second.handle);
            item = saved.erase(item);
        } else ++item;
    }
}

static void CALLBACK Tick(HWND, UINT, UINT_PTR, DWORD) {
    if (stopped) return;
    try {
        if (!ready) ready = ResolveApi();
        if (ready) Adjust();
    } catch (const char *message) {
        Log(message); enabled = false;
        try { if (ready) { Restore(); RestoreMapInputs(); ClearMapCache(); } } catch (...) {}
        stopped = true;
    } catch (...) { Log("Unexpected exception; UI adjustment stopped."); stopped = true; }
}

static LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM w, LPARAM l) {
    if (message == controlMessage) {
        if (w == 1) {
            if (!timerId) timerId = SetTimer(window, 0, 16, Tick);
            Log(timerId ? "Main-thread UI timer started. F10 toggles the layout." : "Could not create UI timer.");
        } else {
            if (timerId) { KillTimer(window, timerId); timerId = 0; }
            stopped = true;
            try { if (ready) { Restore(); RestoreMapInputs(); ClearMapCache(); } } catch (...) {}
            if (reinterpret_cast<WNDPROC>(GetWindowLongPtrW(window, GWLP_WNDPROC)) == WindowProc)
                SetWindowLongPtrW(window, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(originalWindowProc));
        }
        return 0;
    }
    if (!stopped && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN) &&
        w == VK_F10 && !(l & (1LL << 30))) {
        enabled = !enabled;
        WritePrivateProfileStringW(L"Inventory", L"Enabled", enabled ? L"1" : L"0", configPath);
        Tick(window, 0, 0, 0);
        return 0;
    }
    if (!stopped && (message == WM_KEYUP || message == WM_SYSKEYUP) && w == VK_F10) return 0;
    return CallWindowProcW(originalWindowProc, window, message, w, l);
}

static BOOL CALLBACK FindWindow(HWND window, LPARAM) {
    DWORD process = 0; GetWindowThreadProcessId(window, &process);
    wchar_t name[128]{}; GetClassNameW(window, name, 128);
    if (process == GetCurrentProcessId() && IsWindowVisible(window) &&
        wcscmp(name, L"UnityWndClass") == 0 && GetWindow(window, GW_OWNER) == nullptr) {
        gameWindow = window; return FALSE;
    }
    return TRUE;
}

static DWORD WINAPI Start(void *) {
    // Wait for Unity to create its normal game window, outside loader lock.
    for (int i = 0; i < 240 && !stopped; ++i) {
        EnumWindows(FindWindow, 0);
        if (gameWindow) {
            HMODULE pinned;
            GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
                reinterpret_cast<LPCWSTR>(&WindowProc), &pinned);
            originalWindowProc = reinterpret_cast<WNDPROC>(GetWindowLongPtrW(gameWindow, GWLP_WNDPROC));
            SetLastError(0);
            LONG_PTR previous = SetWindowLongPtrW(gameWindow, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(WindowProc));
            if (!previous && GetLastError()) { Log("Could not attach to Unity window."); return 0; }
            if (previous) originalWindowProc = reinterpret_cast<WNDPROC>(previous);
            PostMessageW(gameWindow, controlMessage, 1, 0);
            return 0;
        }
        Sleep(500);
    }
    Log("No normal Unity game window appeared; no changes applied.");
    return 0;
}

extern "C" __declspec(dllexport) bool AddonInit(HMODULE module, HMODULE reshadeModule) {
    addonModule = module;
    stopped = false;
    ready = false;
    mapWasOpen = false;
    mapCacheReady = layoutScanned = false;
    wchar_t executable[MAX_PATH]{}; GetModuleFileNameW(nullptr, executable, MAX_PATH);
    const wchar_t *base = wcsrchr(executable, L'\\');
    if (_wcsicmp(base ? base + 1 : executable, L"Trailmakers.exe") != 0) return false;
    auto registerAddon = reinterpret_cast<bool (*)(void *, uint32_t)>(GetProcAddress(reshadeModule, "ReShadeRegisterAddon"));
    if (!registerAddon || !registerAddon(module, 20)) return false;
    // Timer/window callbacks must remain executable even if another add-on
    // subclasses this window after us and ReShade later disables this add-on.
    HMODULE pinned;
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
        reinterpret_cast<LPCWSTR>(&WindowProc), &pinned);
    GetModuleFileNameW(module, configPath, MAX_PATH);
    wchar_t *extension = wcsrchr(configPath, L'.');
    if (extension) *extension = 0;
    wcscpy_s(logPath, configPath); wcscat_s(logPath, L".log"); wcscat_s(configPath, L".ini");
    enabled = GetPrivateProfileIntW(L"Inventory", L"Enabled", 1, configPath) != 0;
    controlMessage = RegisterWindowMessageW(L"TrailmakersCentreScreenUI-v1-92A7");
    Log("Centre-screen UI add-on loaded. No game DLL or settings modification.");
    HANDLE worker = CreateThread(nullptr, 0, Start, nullptr, 0, nullptr);
    if (!worker) return false;
    CloseHandle(worker);
    return true;
}

extern "C" __declspec(dllexport) void AddonUninit(HMODULE, HMODULE reshadeModule) {
    if (gameWindow && IsWindow(gameWindow)) SendMessageW(gameWindow, controlMessage, 0, 0);
    stopped = true;
    auto unregisterAddon = reinterpret_cast<void (*)(void *)>(GetProcAddress(reshadeModule, "ReShadeUnregisterAddon"));
    if (unregisterAddon) unregisterAddon(addonModule);
}

BOOL WINAPI DllMain(HMODULE module, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) DisableThreadLibraryCalls(module);
    return TRUE;
}
