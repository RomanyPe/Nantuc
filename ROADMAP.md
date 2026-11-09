# ROADMAP

## Vision
Where Nantuc is heading and who it is for.

## v1.0.0 — Must
- API freeze: public API, deprecation policy, tests.
- Document all ways to obtain Kernel components without reflection via VmApplication.
- VM: audit unsafe places, fuzz/edge-case tests, basic benchmarks.
- Automation: GitHub Actions workflows, changelog script.
- Documentation: system design rules.

## v1.0.0 — Should
- Improve class names/object model.
- Useful preprocessor directives.
- Extension points for the API.

## v1.0.0 — Stretch
- `typedef`.
- Replaceable parser.
- Modular/extensible builder.
- Compiler refactoring.

## After v1.0
- `VmApplication.Extension.Vm`:
  - DynamicMethod implementation;
  - fast unsafe interpreter;
  - handwritten JIT in C++ via P/Invoke — if we do it at all.

## Non-goals
- adding major new functionality to the project
- changing method semantics