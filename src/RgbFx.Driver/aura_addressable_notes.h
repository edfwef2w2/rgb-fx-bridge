/*
 * L3 notes: virtual Aura Addressable HID (feature/aura-addressable-sim)
 *
 * - Present a HID device that accepts 65-byte host reports (magic 0xEC).
 * - Forward each OUT/Feature write to user-mode pipe: \\.\pipe\RgbFxAuraAddressable
 * - Protocol parsing lives in managed RgbFx.AuraAddressable.Protocol (do not reimplement
 *   color logic in kernel beyond passthrough).
 * - VID 0x0B05 (ASUS); PID must be configurable — avoid colliding with a real board
 *   controller already present on the machine.
 *
 * Reference (public docs, not GPL source):
 *   https://openrgb-wiki.readthedocs.io/en/latest/asus/ASUS-Aura-USB/
 *
 * Existing vhf_lamparray.c remains the LampArray (Windows Dynamic Lighting) skeleton.
 * Aura Addressable is a separate HID collection / device instance when implemented.
 */
#pragma once
