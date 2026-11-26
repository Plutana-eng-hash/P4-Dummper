# Plutana-DMP

Windows WPF utility for dumping mounted PS4 game content from a PS4 running GoldHEN FTP.

## Features

- Connect to the PS4 FTP server and scan `/mnt/sandbox/pfsmnt/` for mounted game directories.
- Filter the library to base-game mounts, update mounts, or both.
- Identify Title IDs from mount names and show the selected mount type.
- Choose raw game dump or FPKG build output.
- Show game names and icons from PS4 app metadata when available.
- Right-click a detected mount for dump, copy-title-ID, open-output-folder, and copy-mount-path actions.

## Requirements

- Windows with the .NET Framework 4.8.1 runtime.
- A PS4 with GoldHEN running and its FTP server enabled.
- Network connectivity between the PC and PS4.

## Build the Windows app

Open `PS4wpfDumper.sln` in Visual Studio with the .NET Framework 4.8.1 targeting pack, or run the repository's **Build** GitHub Actions workflow. The workflow builds the x64 Release configuration and uploads a ZIP artifact.

## TrampWare plugin build

TrampWare is a GoldHEN game plugin, so its correct output format is **PRX/ SPRX**, not a generic `.bin` payload. The main **Build** GitHub Actions workflow now builds TrampWare with the GoldHEN plugin SDK and OpenOrbis toolchain as a prerequisite to the Windows app build. The resulting Windows ZIP includes `plugins/TrampWare.prx` and `plugins/TrampWare.sprx`, and the workflow also uploads a standalone `TrampWare-PS4-plugin` artifact.

To install it:

1. Download and extract the `TrampWare-PS4-plugin` artifact from the workflow run.
2. Copy `TrampWare.prx` to `/data/GoldHEN/plugins/trampware.prx` on the PS4.
3. Add `/data/GoldHEN/plugins/trampware.prx` as a plugin entry in `/data/GoldHEN/plugins.ini`.
4. Restart the game (or reboot the console) so GoldHEN loads the plugin.

The in-game menu opens with **L3 + R3** by default. Use D-pad navigation, Cross to select, Circle to close, L1/R1 to change tabs, and Triangle to return to categories. TrampWare settings are stored in `/data/GoldHEN/trampware.ini`; its log is at `/data/trampware/trampware.log`.

**Important:** FTP copying the plugin does not inject it into a game that is already running. GoldHEN loads configured plugins at the supported game/plugin startup point. The PC app must not claim that the menu is live until the plugin is actually loaded.

## Goto Home

A Goto Home action must use the PS4 system's normal application-exit request (`sceSystemServiceLoadExec("exit", NULL)`). It must not kill arbitrary processes or reboot the console. This repository does not yet ship a verified Goto Home payload binary; the binary needs to be built and validated against the PS4 payload SDK and supported firmware before it is offered as a runnable `.bin`. A source placeholder or renamed PRX would not be a working payload.

## Existing payloads

The `PS4wpfDumper/bins` directory contains the existing PS4 payload binaries. Do not rename PRX plugins to `.bin`: GoldHEN plugin loading and BinLoader payload execution are different interfaces.
