# Runtime Inspector protocol

The Windows application now includes a **Runtime Inspector** receiver. Open it from the main window, choose a TCP port (default `9147`), and start listening. It accepts one UTF-8 JSON object per TCP line and exports received records to `PLUTANA-DMP-runtime.json`.

## Record format

Required fields:

- `moduleName`: module name, such as `eboot.bin`
- `moduleBase`: module base address (hex string such as `0x400000`, or decimal)
- `address`: address belonging to that module (hex string or decimal)

Optional fields: `gameTitle`, `titleId`, `gameVersion`, `processId`, and `label`.

Example line:

```json
{"gameTitle":"Offline Test Build","titleId":"CUSA00000","gameVersion":"1.00","processId":123,"moduleName":"eboot.bin","moduleBase":"0x400000","address":"0x412A80","label":"test_marker"}
```

The receiver calculates `offset = address - moduleBase`, rejects malformed records and addresses below the supplied module base, and records the receive time. The JSON export is watermarked `PLUTANA-DMP`.

## Important implementation boundary

This is the **Windows-side receiver and offset calculator**, not a PS4 process-memory reader. It does not read PS4 memory, enumerate processes, or discover module bases by itself. A compatible PS4-side inspector must be built against the exact PS4 SDK/GoldHEN plugin API and send the records above. The existing `bins/module-dumper.bin` is a USB module dumper (its binary strings identify a USB-device wait flow); it is not a live process-memory reader and is not silently repurposed by this feature.

The receiver is read-only and only processes metadata/address records supplied by the connected inspector. It does not accept memory-write commands or arbitrary payload execution.
