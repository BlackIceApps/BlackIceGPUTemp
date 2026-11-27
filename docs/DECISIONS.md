# BlackIceGPUTemp - Decision Log and License References

## 0. Technical Context & Decisions

- **Target Framework:** `net48` (.NET Framework 4.8), `PlatformTarget = x64`, `Prefer32Bit = false`.
- **UI:** Windows Forms (`NotifyIcon`), WinExe (no console).
- **Architecture:** Zero dependencies in production app (`BlackIceGPUTemp.exe` is a single portable executable). Modular GPU provider architecture isolating native interop (`Native/`) behind robust abstractions (`IGpuProvider`, `IGpuDevice`).

## 1. AMD Libraries & Licensing Sources

- **ADL (Display Library):**
  - Document/Source: AMD Display Library (ADL) SDK License Agreement (included in official GPUOpen ADL SDK).
  - Terms Summary: Permission to use SDK headers and documentation for developing tools for AMD graphics hardware; requires preserving copyright notices in documentation/notices where applicable. Used by `BlackIceGPUTemp.Diagnostics` console utility for diagnostic and comparative purposes only; dynamically loads system `atiadlxx.dll` (`LoadLibraryEx` with `LOAD_LIBRARY_SEARCH_SYSTEM32`). Not used by the production `BlackIceGPUTemp.exe` application. Does not redistribute ADL SDK binaries or headers.
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
- **V1.0 vendor support scope:** The initial release supports AMD GPUs only, using AMD ADLX as the primary temperature-reading API. NVIDIA and Intel GPUs are explicitly out of scope for V1.0. The provider architecture is designed to allow additional vendors to be supported in future releases.

## 3. GPU Device Selection Policy

- **Intended scope:** BlackIceGPUTemp is designed primarily for systems with a single GPU. The application targets typical users who have one graphics card.
- **Selection mechanism:** The application selects the first device returned by the GPU provider (`devices[0]`). No preference is applied for discrete vs. integrated GPUs (`GpuKind.Discrete` is not preferred).
- **User interface:** No UI is provided for manual GPU selection. The user cannot override the automatic choice.
- **Rationale:** Simplicity of operation and avoidance of unnecessary configuration. The product is a lightweight utility, not a multi-GPU management tool.
- **Multi-GPU behavior:** When multiple GPUs are present, the selected device depends on the enumeration order returned by the underlying provider (ADLX). This order is not guaranteed to be stable across driver updates or hardware changes.
- **Future consideration:** Manual GPU selection may be revisited if the product scope changes to explicitly support multi-GPU configurations. This is not a commitment to implement.

(End of file - total 44 lines)