using System;

namespace Vivace.Models;

// metadata and summary info for a MIDI file.
// name, path, tracks, events, etc.
public class MidiFileInfo
{
    // NOTE: I left these as strings for now
    // we should change this later
    public string? FileName { get; set; }
    public string? FilePath { get; set; }

    // total number of tracks, 0 is usually tempo
    public int TrackCount { get; set; }
    // number of tempo events specifically (tempo changes)
    public int TempoEventCount { get; set; }
    // total of all events (notes, control changes, program changes, etc.)
    public int EventCount { get; set; }

    public TimeSpan Duration { get; set; }

    // we need to know the format, it can be a value from 0-2
    // TODO: maybe make this an enum later
    public string? Format { get; set; }

    // it might be worth detecting specification standards
    // non-GM/GM2 specifications (proprietary) can break some players, controllers, and soundfonts
    public string? MidiSpecification { get; set; }

    // flag to indicate if the MIDI file is empty (no events)
    public bool IsEmpty { get; set; }

    public MidiFileInfo()
    {
        // leaving defaults, strings are null for now
        TrackCount = 0;
        EventCount = 0;
        Duration = TimeSpan.Zero;
        TempoEventCount = 0;
        // format is null until we parse it
    }

    // summary string for debugging
    public override string ToString()
    {
        var specInfo = !string.IsNullOrEmpty(MidiSpecification) ? $", Spec={MidiSpecification}" : "";
        var emptyInfo = IsEmpty ? ", EMPTY" : "";
        return $"MIDI: {FileName ?? "(no name)"} | Tracks={TrackCount}, Events={EventCount}, Duration={Duration}, Format={Format ?? "unknown"}{specInfo}{emptyInfo}";
    }
}