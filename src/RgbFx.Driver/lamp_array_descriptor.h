/* Shared with managed RgbFx.LampArray.Protocol.ReportDescriptorBytes — keep in sync. */
#pragma once

#ifdef __cplusplus
extern "C" {
#endif

/* HID Lighting and Illumination page (0x59) LampArray application collection.
 * Align with Microsoft ArduinoHidForWindows / Dynamic Lighting device docs during bring-up.
 */
static const unsigned char RGBFX_LAMP_ARRAY_REPORT_DESCRIPTOR[] = {
    0x05, 0x59,       /* Usage Page (Lighting And Illumination) */
    0x09, 0x01,       /* Usage (LampArray) */
    0xA1, 0x01,       /* Collection (Application) */

    0x85, 0x01,       /*   Report ID 1 — Attributes */
    0x09, 0x02,
    0xA1, 0x02,
    0x75, 0x08,
    0x95, 0x13,
    0xB1, 0x03,
    0xC0,

    0x85, 0x04,       /*   Report ID 4 — Multi update */
    0x09, 0x05,
    0xA1, 0x02,
    0x75, 0x08,
    0x95, 0x40,
    0x91, 0x02,
    0xC0,

    0x85, 0x05,       /*   Report ID 5 — Range update */
    0x09, 0x06,
    0xA1, 0x02,
    0x75, 0x08,
    0x95, 0x08,
    0x91, 0x02,
    0xC0,

    0x85, 0x06,       /*   Report ID 6 — Control / Autonomous */
    0x09, 0x07,
    0xA1, 0x02,
    0x75, 0x08,
    0x95, 0x01,
    0xB1, 0x02,
    0xC0,

    0xC0
};

static const unsigned int RGBFX_LAMP_ARRAY_REPORT_DESCRIPTOR_LENGTH =
    sizeof(RGBFX_LAMP_ARRAY_REPORT_DESCRIPTOR);

/* Named pipe / symbolic link contract with user-mode service */
#define RGBFX_USERMODE_PIPE_NAME L"\\\\.\\pipe\\RgbFxLampArray"
#define RGBFX_DEVICE_DESCRIPTION L"RgbFx Virtual Chassis LampArray"

#ifdef __cplusplus
}
#endif
