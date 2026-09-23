# Use Avalonia for the desktop application

HockeySim targets Windows, macOS, and Linux with a shared Avalonia UI, using
CommunityToolkit.Mvvm for presentation state and commands. This supports the
three-platform product scope with one UI codebase, while still requiring native
smoke checks and packaging validation on each OS. The UI consumes the headless
Management interface; the existing console entry point remains scaffolding
until the desktop setup work is undertaken.
