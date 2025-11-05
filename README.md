# Vivace MIDI

A fast, modern, and fully open-source MIDI sequencer and editor designed for Linux users. Not a DAW!

Current Status: ALPHA v0.0.0 - Early development

## What is Vivace?

Vivace is a dedicated MIDI sequencing/editing program built specifically for Linux users. The goal is to provide an open-source, responsive, and fully-featured MIDI composing experience. No additional complexity, or any of the unnecessary features of a full Digital Audio Workstation (DAW).

### State of Development (v0.0.0)

- Midi File Import: Load .mid and .midi files with metadata analysis
- Format Detection: Automatic identification of MIDI file formats (Format 0, 1, or 2)
- Standard Detection: Advanced detection of sound standard specifications:
  - General MIDI (GM)
  - General MIDI Level 2 (GM2)
  - Roland GS
  - Yamaha XG

### Roadmap

This is the earliest alpha build. Current "features" are a foundation to expand into a highly capable MIDI sequencer/editor for Linux users.

Future planned features include:
- MIDI playback with FluidSynth integration
- SoundFont2 support
- Piano roll editor for note editing on a grid
- MIDI + audio export functionality
- Track management
- Tempo and time signature editing
- Velocity and controller editing
- Full event handling

## Technology Stack

- Framework: .NET 9.0
- UI: Avalonia 11.3.8
- Architecture: MVVM with CommunityToolkit.Mvvm
- MIDI Library: Melanchall DryWetMIDI 7.2.0
- Target Platform: Linux

## Contributing

Contribution guidelines will be established as the project matures.
