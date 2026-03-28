# ADR-0005: Editor Bridge Policy

- Status: accepted
- Date: 2026-03-29

## Context

The project may later benefit from a Unity MCP or similar editor bridge for inspection and controlled automation.
However, editor bridges increase the risk of hidden state changes, large unintended edits, and boundary violations that are harder to review than normal file-based changes.

## Decision

Adopt a policy-first approach:

- do not install or connect any editor bridge by default
- define allowed, blocked, and review-required operation categories first
- prefer read-oriented and narrowly scoped write operations if a bridge is later adopted
- keep architecture boundaries intact, especially `Assets/ThirdParty` versus `Assets/_Game`
- require sandbox-first validation and explicit rollback paths for risky bridge use

## Consequences

### Positive

- lower chance of premature unsafe tooling adoption
- clearer operating rules before automation expands
- stronger alignment with data-first and boundary-first project architecture
- easier human oversight of future bridge trials

### Negative

- some editor-assisted workflows will remain slower until a bridge is formally evaluated
- future adoption will require extra governance and trial effort
- contributors may need to tolerate more manual or file-based workflows in the short term

## Follow-Up

- maintain `docs/05_setup/unity-mcp.md` as the operational policy reference
- evaluate candidate bridges only against documented criteria
- document exceptions explicitly if bridge installation is ever approved
