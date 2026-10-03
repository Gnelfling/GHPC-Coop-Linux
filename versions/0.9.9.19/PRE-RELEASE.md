# GHPC Coop 0.9.9.19 Pre-release

This is the first pre-release for the 0.9.9.19 fixed version in the `versions/0.9.9.19` tree.

## Purpose

This build is intended for testing the transport and network diagnostics around the reported Linux/Proton multiplayer stall and queue overflow behavior.

## Included changes

- Diagnostic logging added to the TCP transport path in `versions/0.9.9.19/src/Transport.cs`.
- No transport logic, queue limits, retry behavior, packet formats, or network design were changed.
- The logging is intentionally minimal and scoped to the send/receive path to identify whether the queue is filling due to blocked writes, stalled reads, or a specific message type.

## Release status

- Status: pre-release
- Target: testing / validation
- Scope: static diagnostic instrumentation only

## Notes

- This is not a gameplay or networking redesign.
- The goal is to confirm the runtime path that leads to `Send queue overflow` and the associated snapshot stall.
- Best used in combination with the native logs emitted by the game while reproducing the direct-IP / Hamachi failure.
