# Plutana-DMP

Windows WPF utility for dumping mounted PS4 game content from a PS4 running GoldHEN FTP.

## Features

- Connect to the PS4 FTP server and scan `/mnt/sandbox/pfsmnt/` for mounted game directories.
- Filter the library to base-game mounts, update mounts, or both.
- Identify Title IDs from mount names (for example, `CUSAxxxxx`) and show the selected mount type.
- Choose an output mode:
  - **Raw game dump** — keep the extracted game files in the `ext` folder.
  - **Build FPKG package** — run the existing GP4/FPKG build path after extraction.
- Write a `PLUTANA-DMP.xml` and `PLUTANA-DMP.txt` provenance watermark into each dump folder. These are sidecar metadata files; the game files themselves are not modified by the watermark.

## Requirements

- Windows with the .NET Framework 4.8.1 runtime.
- A PS4 with GoldHEN running and its FTP server enabled.
- Network connectivity between the PC and PS4.

## Use

1. Start GoldHEN and enable FTP on the PS4.
2. Start the game so its content is mounted.
3. Launch PS4Dumper on Windows.
4. Enter the PS4 IP address and FTP port (normally `2121`).
5. Click **Connect** to detect the mounted game folders.
6. Choose **Base Game**, **Game Patch**, or **All**, then select the mount to dump.
7. Choose **Raw game dump** or **Build FPKG package** in the output selector.
8. Click **Download** and wait for the operation to finish.

The app stores extracted files under `ext\\` and generated packages under `FPKG\\`.

## Build

Open `PS4wpfDumper.sln` in Visual Studio with the .NET Framework 4.8.1 targeting pack, or run the repository's **Build** GitHub Actions workflow. The workflow builds the x64 Release configuration and uploads a ZIP artifact.

## Notes

- The FTP mount list contains content currently mounted by the console; start the game and reconnect if the list is empty.
- The package-building option uses the repository's existing GP4/FPKG implementation and its existing requirements.
- The Plutana-DMP watermark is written as metadata beside the dump content, not embedded into executable or game data.
