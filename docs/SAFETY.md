# Safety notes

Open Window Utility changes the operating system. Most tweaks are reversible through the undo journal, but some are not.

## Always

- Run on a machine (or VM) you can rebuild.
- Keep **Create a restore point before applying tweaks** enabled unless System Restore is unavailable.
- Prefer **Select Essential Tweaks** over ticking Advanced items blindly.
- Export a profile only after you have tested the selection.
- The EXE is portable. Settings and the undo journal follow the `data` folder next to the EXE (or AppData if that folder is not writable). Restore points stay on the Windows install.

## High impact (extra confirmation)

| Action | Risk |
| --- | --- |
| BitLocker - Disable | Decrypts the system drive. Undo does not re-encrypt. |
| OneDrive sync - Disable | Policy blocks sync; files already local stay local. |
| Disable All Updates | No security patches until you apply Default. Type `DISABLE` to confirm. |
| Copilot - Disable | Removes the Copilot AppX package for all users. |
| Recall - Disable | Policy blocks Windows Recall snapshots. Confirm first. |
| Fast startup - Disable | Full shutdown instead of hybrid boot; laptops may take longer to start. |
| LLMNR - Disable | Can break name lookup on some local networks. |
| IPv6 disable | Can break some VPN / modern networks. Prefer "IPv4 preferred" first. |
| Hibernation disable | Deletes/stops hiberfil.sys; laptops lose hibernate. |
| Deep Cleanup | Deletes selected junk (temp, caches, Recycle Bin). Scan first; Deep items (browser, AI app/model caches, package caches, Prefetch, Windows.old) stay unchecked. Windows.old and AI download caches each need a second confirm. Does not delete Ollama models, Cursor projects, System32, or Program Files. |
| App self-update | Replaces the running EXE after SHA-256 check. Confirm first; the app restarts. Only HTTPS feeds and allowlisted hosts. |
| Hyper-V / Sandbox / WSL / Containers / Application Guard | Needs a compatible Windows edition; may require reboot. |
| Network reset | Resets TCP/IP and Winsock. A reboot is often required. |
| System scan / Component cleanup | SFC and DISM can take a long time and should not be cancelled mid-run. |

## What "Default" Windows Update does

Default only removes registry values this app journaled (or the known Security/Disable set). It does **not** wipe enterprise Group Policy you did not create with Open Window Utility.

## Antivirus

Registry, service, and DISM tools are often flagged heuristically. Prefer building from source. Code signing is planned after v1.

## Attribution

Behavior is inspired by Chris Titus Tech WinUtil. Catalogs, UI copy, and engines in this repository are original. Do not copy CTT assets into this project.
