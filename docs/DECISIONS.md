# TinyTemp - Decision Log and License References

## 0. Technical Context & Decisions

- **Target Framework:** `net48` (.NET Framework 4.8), `PlatformTarget = x64`, `Prefer32Bit = false`.
- **UI:** Windows Forms (`NotifyIcon`), WinExe (no console).
- **Architecture:** Zero dependencies in production app (`TinyTemp.exe` is a single portable executable). Modular GPU provider architecture isolating native interop (`Native/`) behind robust abstractions (`IGpuProvider`, `IGpuDevice`).

## 1. AMD Libraries & Licensing Sources

- **ADL (Display Library):**
  - Document/Source: AMD Display Library (ADL) SDK License Agreement (included in official GPUOpen ADL SDK).
  - Terms Summary: Permission to use SDK headers and documentation for developing tools for AMD graphics hardware; requires preserving copyright notices in documentation/notices where applicable. Application loads system `atiadlxx.dll` dynamically (`LoadLibraryEx` with `LOAD_LIBRARY_SEARCH_SYSTEM32`) and does not redistribute ADL SDK binaries or headers.
- **ADLX (AMD Device Library):**
  - Document/Source: ADLX SDK License Agreement.pdf (official GPUOpen ADLX SDK).
  - Terms Summary: Similar terms regarding SDK header/documentation usage. Application loads system `amdadlx64.dll` dynamically from `System32`.
- **LibreHardwareMonitorLib (Diagnostics Only):**
  - License: MPL-2.0 (Mozilla Public License 2.0).
  - Usage scope: Strictly limited to `TinyTemp.Diagnostics` console utility as a reference comparison during Stage 1 and future vendor analysis. Never included in the production `TinyTemp.exe` release.

## 2. Vendor Selection & Roadmap
- V1.0 candidate APIs: ADL (`atiadlxx.dll`) and ADLX (`amdadlx64.dll`). Stage 1 diagnostics will determine the primary provider for v1.0 based on reliability, lack of admin requirement, and exact temperature edge metric verification.
