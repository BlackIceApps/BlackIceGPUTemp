# BlackIceGPUTemp - Decision Log and License References

## 0. Technical Context & Decisions

- **Target Framework:** `net48` (.NET Framework 4.8), `PlatformTarget = x64`, `Prefer32Bit = false`.
- **UI:** Windows Forms (`NotifyIcon`), WinExe (no console).
- **Architecture:** Zero dependencies in production app (`BlackIceGPUTemp.exe` is a single portable executable). Modular GPU provider architecture isolating native interop (`Native/`) behind robust abstractions (`IGpuProvider`, `IGpuDevice`).

## 1. AMD Libraries & Licensing Sources

- **ADL (Display Library):**
  - Document/Source: AMD Display Library (ADL) SDK License Agreement (included in official GPUOpen ADL SDK).
  - Terms Summary: Permission to use SDK headers and documentation for developing tools for AMD graphics hardware; requires preserving copyright notices in documentation/notices where applicable. Application loads system `atiadlxx.dll` dynamically (`LoadLibraryEx` with `LOAD_LIBRARY_SEARCH_SYSTEM32`) and does not redistribute ADL SDK binaries or headers.
- **ADLX (AMD Device Library):**
  - Document/Source: ADLX SDK License Agreement.pdf (official GPUOpen ADLX SDK).
  - Terms Summary: Similar terms regarding SDK header/documentation usage. Application loads system `amdadlx64.dll` dynamically from `System32`.
- **LibreHardwareMonitorLib (Diagnostics Only):**
  - License: MPL-2.0 (Mozilla Public License 2.0).
  - Usage scope: Strictly limited to `BlackIceGPUTemp.Diagnostics` console utility as a reference comparison during Stage 1 and future vendor analysis. Never included in the production `BlackIceGPUTemp.exe` release.

## 2. Vendor Selection & Roadmap
- V1.0 candidate APIs: ADL (`atiadlxx.dll`) and ADLX (`amdadlx64.dll`). Stage 1 diagnostics concluded:
  - **AMD ADLX** is selected as the primary API for GPU temperature reading in BlackIceGPUTemp.
  - ADLX successfully detected the GPU and read `IADLXGPUMetrics::GPUTemperature` on all tested machines (Windows 10 with Radeon RX 580 at 74°C, and Windows 11 with Radeon RX 9070 GRE at 27°C, both with Edge Temperature semantics).
  - In both cases, `IsSupportedGPUTemperature` returned `Supported: 1`, and metric reading finished with `Result: 0`.
  - Results matched LHM GPU Core readings (73°C and 27°C respectively), with ADLX confirming Edge Temperature semantics.
  - ADL worked on RX 580, but checked ADL temperature methods (OD5, OD6, OverdriveN) failed to read temperature on RX 9070 GRE. ADL will not be the primary API, but may be considered as a future alternative if needed.
  - Tests were executed without administrator privileges in an x64 process.
  - CI after changing ADLX `IADLXGPUList::At_GPUList` to vtable index 11 completed successfully.
