# Safety notes

Open Window Utility changes the operating system. Most tweaks are reversible through the undo journal, but some are not.

## Always

- Run on a machine (or VM) you can rebuild.
- Keep **Create a restore point before applying tweaks** enabled unless System Restore is unavailable.
- Prefer **Select Essential Tweaks** over ticking Advanced items blindly.
- Export a profile only after you have tested the selection.

## High impact (extra confirmation)

| Action | Risk |
| --- | --- |
| BitLocker - Disable | Decrypts the system drive. Undo does not re-encrypt. |
| OneDrive sync - Disable | Policy blocks sync; files already local stay local. |
| Disable All Updates | No security patches until you apply Default. Type `DISABLE` to confirm. |
| Copilot - Disable | Removes the Copilot AppX package for all users. |
| IPv6 disable | Can break some VPN / modern networks. Prefer "IPv4 preferred" first. |
| Hibernation disable | Deletes/stops hiberfil.sys; laptops lose hibernate. |
| Deep Cleanup | Deletes selected junk (temp, caches, Recycle Bin). Scan first; Deep items (browser cache, Prefetch, Windows.old) stay unchecked. Windows.old needs a second confirm and removes upgrade rollback. |
| App self-update | Replaces the running EXE after SHA-256 check. Confirm first; the app restarts. Only HTTPS feeds and allowlisted hosts. |
| Hyper-V / Sandbox / WSL | Needs a compatible Windows edition; may require reboot. |

## What "Default" Windows Update does

Default only removes registry values this app journaled (or the known Security/Disable set). It does **not** wipe enterprise Group Policy you did not create with Open Window Utility.

## Antivirus

Registry, service, and DISM tools are often flagged heuristically. Prefer building from source. Code signing is planned after v1.

## Attribution

Behavior is inspired by Chris Titus Tech WinUtil. Catalogs, UI copy, and engines in this repository are original. Do not copy CTT assets into this project.
