# ADR-0003: Match HWiNFO sensors on their original names

- Status: accepted
- Date: 2026-10-24

## Context
HWiNFO's shared memory gives each sensor and reading two names: the original one HWiNFO generates
(`CPU [#0]: Intel Core i5-14600K: Enhanced`, `CPU Package`) and a user name that can be renamed in HWiNFO's UI.
Original names embed the hardware model, so they can't be bound to directly by a shareable dashboard, but their *shape*
(`CPU [#N]: <model>`, `dGPU [#N]: <model>`, reading labels such as `GPU Hot Spot Temperature`) is stable across models.
Reading indices and IDs are not documented as stable across HWiNFO versions.

## Decision
- Built-in rules match the original sensor name by pattern and the original reading label, with ordered fallbacks per
  vendor (Intel / AMD CPU, AMD / NVIDIA GPU).
- User overrides, set from the plugin's settings page, are stored by original sensor name + instance + reading label.
- Resolved positions (reading index + reading ID) are cached only for the running HWiNFO session and re-resolved when the
  layout changes.

## Consequences
- Renaming sensors in HWiNFO never changes what the plugin reports.
- A new HWiNFO label for an existing reading needs a rule update; fixture tests from real dumps catch this.
