/*
 * RgbFx Virtual HID LampArray — KMDF + VHF skeleton
 *
 * Goal: enumerate a Chassis LampArray in Windows 11 Dynamic Lighting, then
 * forward host output reports to user-mode via \\.\pipe\RgbFxLampArray.
 *
 * Build requires Visual Studio + Windows Driver Kit (WDK).
 * See docs/INSTALL_TESTSIGN.md and docs/LAMPARRAY_SPEC.md.
 *
 * References:
 *  - https://learn.microsoft.com/windows-hardware/design/component-guidelines/dynamic-lighting-devices
 *  - https://learn.microsoft.com/windows-hardware/drivers/hid/virtual-hid-framework--vhf-
 *  - https://github.com/microsoft/ArduinoHidForWindows
 */

#include <ntddk.h>
#include <wdf.h>
#include <vhf.h>
#include "lamp_array_descriptor.h"

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD RgbFxEvtDeviceAdd;

/* TODO: VHF_CONFIG + VhfCreate; implement ReadyForNextReadReport / process WriteReport
 * callbacks to push Multi/Range/Control payloads to the user-mode pipe server.
 *
 * Until the full VHF path is completed, RgbFx.Service SourceMode=simulator validates
 * the MSI /api/v1/frame integration without the driver.
 */

NTSTATUS
DriverEntry(
    _In_ PDRIVER_OBJECT DriverObject,
    _In_ PUNICODE_STRING RegistryPath
)
{
    WDF_DRIVER_CONFIG config;
    WDF_DRIVER_CONFIG_INIT(&config, RgbFxEvtDeviceAdd);
    return WdfDriverCreate(DriverObject, RegistryPath, WDF_NO_OBJECT_ATTRIBUTES, &config, WDF_NO_HANDLE);
}

NTSTATUS
RgbFxEvtDeviceAdd(
    _In_ WDFDRIVER Driver,
    _Inout_ PWDFDEVICE_INIT DeviceInit
)
{
    UNREFERENCED_PARAMETER(Driver);
    UNREFERENCED_PARAMETER(DeviceInit);

    /* Scaffold: return success so INF install can proceed once WDK project is wired.
     * Real implementation creates WDFDEVICE, VHFHANDLE, and report descriptor from
     * RGBFX_LAMP_ARRAY_REPORT_DESCRIPTOR.
     */
    KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL,
        "RgbFx.Driver: skeleton DeviceAdd (descriptor %u bytes)\n",
        RGBFX_LAMP_ARRAY_REPORT_DESCRIPTOR_LENGTH));

    return STATUS_SUCCESS;
}
