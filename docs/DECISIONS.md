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
- **Enumeration failure policy:** Production GPU enumeration is fail-fast. If retrieving or validating any enumerated GPU fails (including handle, device identity, name, or temperature support checks), the enumeration fails as a whole and does not return a partial device list. This is intentional for the current single-GPU-focused scope; partial enumeration and per-device error recovery are not supported.
- **Probe behavior:** GPU probing may tolerate individual device errors while checking for a supported GPU. This does not change the stricter failure policy used by production enumeration.
- **User interface:** No UI is provided for manual GPU selection. The user cannot override the automatic choice.
- **Rationale:** Simplicity of operation and avoidance of unnecessary configuration. The product is a lightweight utility, not a multi-GPU management tool.
- **Multi-GPU behavior:** When multiple GPUs are present, the selected device depends on the enumeration order returned by the underlying provider (ADLX). This order is not guaranteed to be stable across driver updates or hardware changes.
- **Future consideration:** Manual GPU selection may be revisited if the product scope changes to explicitly support multi-GPU configurations. This is not a commitment to implement.

## 4. Runtime Behavior & Limitations

### GPU Hot-Plug Is Not Supported

The application does not support dynamically detecting GPU devices being connected or disconnected while it is running.

GPU enumeration and initialization are performed as part of the provider lifecycle. Monitoring changes to the available GPU devices and reinitializing the provider at runtime are outside the scope of the current version.

This is a deliberate scope limitation, not an accidental omission. Supporting GPU hot-plug would require a separate device-change detection and lifecycle management mechanism.

### Native ADLX Calls Are Not Cancellable
**Decision:** Accept that synchronous native ADLX calls cannot be interrupted by `CancellationToken` or application-level timeouts.

**Context:**
The application checks `CancellationToken` between native ADLX calls. However, cancellation cannot interrupt a native call that is already executing. If ADLX hangs, the background monitoring operation may remain blocked. Since provider operations use synchronization, subsequent operations may also wait for the blocked call.

**Rationale:**
- A timeout applied to the managed task would only stop waiting for the result; it would not terminate the native call.
- Attempting to release ADLX interfaces while a native call is still executing could introduce resource-lifetime and memory-safety issues.
- Proper isolation would require moving ADLX operations into a separate process, which is not justified by the current application requirements.

**Consequences:**
- Keep cancellation checks between native calls.
- Do not introduce misleading task-level timeouts as a substitute for native cancellation.
- Accept that an unresponsive ADLX call may block GPU monitoring until the call returns or the application exits.
- Reconsider this decision if monitoring reliability requirements change.

### Single-Instance Application Behavior

BlackIceGPUTemp enforces a single running instance per Windows user session using a named local mutex.

If another instance is already running, the newly launched instance exits silently before initializing the GPU provider or creating the tray application context.

No notification is displayed, and the existing instance is not activated or brought to the foreground. This is intentional behavior for the background tray application. Forwarding activation requests to an existing instance is outside the scope of the current version.

### Manual GPU Initialization Retry

If the initial GPU monitoring initialization fails, the tray menu exposes a manual Retry option. Retry is available only while initialization has failed; no automatic retry or periodic reinitialization is performed. A retry uses the same initialization flow as application startup.

### Log File Rotation Is Not Required

The application uses append-only logging without automatic log rotation.

This is an intentional decision based on the expected low logging volume during normal operation. Repeated warning and error messages are deduplicated to avoid unnecessarily growing the log file during a long-running session.

Automatic log rotation, size limits, and log retention management are not required for the current application scope.

Reconsider this decision only if real-world usage shows sustained log growth or log files becoming impractically large.