# QUADRO protocol notes

These layouts are independently implemented encoders/decoders. No Aquasuite executable, decompiled source, UI assets or captured private packets are included. Development used public monitoring behavior, local USB observations and read-back experiments. This is not an official Aqua Computer specification.

Write support is limited to firmware 1033, settings structure 3. Protocol assumptions for other firmware must be tested before extending the allow-list.

- USB vendor 0x0C70, product 0xF00D.
- HID status report ID 1, 220 bytes. Firmware at 0x0D; two serial number components at 3 and 5.
- Four signed centi-degree physical temperatures at 0x34; software fields at 0x3C. 0x7FFF means unavailable.
- Flow at 0x6E, decilitres/hour; 0x7FFF unavailable, zero is a measured zero.
- Four fan records at 0x70, 0x7D, 0x8A, 0x97: duty, voltage, current, power, RPM.
- Settings report ID 3, 961 bytes, structure at 1. Flow calibration at 6, correction at 8.
- Channel limit records at 0x12 + 9 * index; controller records at 0x36, 0x8B, 0xE0, 0x135.
- Native curve mode 2. Physical temperature sources 0–3; first software source 5. Lutra allocates up to eight software slots, although four output channels bound its current need.
- Startup at controller + 0x13; 16 temperature points at +0x15 and 16 response points at +0x35.
- Settings and software sensor reports use reflected CRC-16/USB over bytes excluding report ID and trailing CRC, with initial/final xor 0xFFFF.
- Settings commit output: 02 00 00 00 02 00 00 00 00 34 C6.
- WinUSB interface GUID 99c4bbb0-e925-4397-afee-981cd0702163, interface 0, bulk OUT endpoint 0x02.
- Software sensor report ID 4, 67 bytes; 16 temperature fields, unit 3 for active Celsius inputs, access bytes 100, unavailable temperature 0x7FFF.

Only slot 0 and the two original cooling channels were commissioned in the earlier private controller. Additional slot/channel combinations are encoded and unit-tested, but remain in the preview's hardware validation backlog. Do not equate a successful encoder test with physical commissioning.

QUADRO keeps a startup latch: a zero-origin curve starts at the chosen startup temperature and stops at the curve's last zero response. Response scales between output limits; hold-minimum determines whether a zero response becomes zero duty or minimum duty.

