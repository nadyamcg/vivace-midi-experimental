using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Vivace.Models;

namespace Vivace.Services;
// service class responsible for MIDI file operations using DryWetMIDI
public class MidiFileService
{
    public MidiFileInfo? LoadMidiFile(string filePath)
    // let's not mark this one as static, might need to inject dependencies later
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"MIDI file not found: {filePath}", filePath);
        }

        try
        {
            // DryWetMIDI parses the binary MIDI file format into an object we can work with
            var midiFile = MidiFile.Read(filePath);

            // extract file name from full path
            var fileName = Path.GetFileName(filePath);

            // count all MIDI events across all tracks
            // sift through all track chunks and count their events
            var eventCount = midiFile.GetTrackChunks().Sum(chunk => chunk.Events.Count);

            // detect if MIDI file is empty (no events at all)
            var isEmpty = eventCount == 0;

            // grab tempo map, tells us how tempo changes throughout the piece
            var tempoMap = midiFile.GetTempoMap();

            // calculate the total duration of the MIDI file
            // just find last event's time and convert it to a TimeSpan
            var duration = TimeSpan.Zero;
            if (eventCount > 0)
            {
                // get the time of the last event in metric time
                var lastEventTime = midiFile.GetTimedEvents()
                    .LastOrDefault()?.TimeAs<MetricTimeSpan>(tempoMap);
                if (lastEventTime != null)
                {
                    duration = (TimeSpan)lastEventTime;
                }
            }

            // determine the MIDI file format (0, 1, or 2)
            var formatString = midiFile.OriginalFormat switch
            {
                MidiFileFormat.SingleTrack => "Format 0 (Single Track)",
                MidiFileFormat.MultiTrack => "Format 1 (Multi Track)",
                MidiFileFormat.MultiSequence => "Format 2 (Multi Sequence)",
                _ => "Unknown Format"
            };

            // detect MIDI specification standard (GM, GM2, XG, GS)
            var midiSpecification = DetectMidiSpecification(midiFile);

            // count tempo change events
            var tempoEventCount = midiFile.GetTrackChunks()
                .SelectMany(chunk => chunk.Events)
                .OfType<SetTempoEvent>()
                .Count();

            // create and return our domain model
            return new MidiFileInfo
            {
                FileName = fileName,
                FilePath = filePath,
                TrackCount = midiFile.GetTrackChunks().Count(),
                EventCount = eventCount,
                Duration = duration,
                Format = formatString,
                TempoEventCount = tempoEventCount,
                MidiSpecification = midiSpecification,
                IsEmpty = isEmpty
            };
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not FileNotFoundException)
        {
            // wrap any DryWetMIDI-specific exceptions in a more generic exception
            // this should prevent implementation details from leaking to the ViewModel
            throw new InvalidOperationException($"Failed to read MIDI file: {ex.Message}", ex);
        }
    }

    // WIP
    // unrepetant MIDI specification format detector
    // still not happy with it
    private string DetectMidiSpecification(MidiFile midiFile)
    {
        // flags for detected standards
        bool hasGM1 = false;
        bool hasGM2 = false;
        bool hasXG = false;
        bool hasGS = false;

        // reassemble and analyze all SysEx messages in raw data
        foreach (var sysExData in EnumerateFullSysExMessages(midiFile))
        {
            // checking for explicit markers and usage patterns
            // specifically looking for any attempt from the MIDI file to communicate with a MIDI device
            // these attempts should contain manufacturer-specific data indicating GM/GM2 or XG/GS modes
            // if nothing is found, attempt to find any parameter change that is exclusive to proprietary formats
            // if proprietary signatures are detected it will be evidence for XG or GS
            if (IsGM1SystemOn(sysExData))
            {
                hasGM1 = true;
            }

            if (IsGM2SystemOn(sysExData))
            {
                hasGM2 = true; // GM2 takes precedence over GM1
            }

            if (IsXGSystemOnOrReset(sysExData))
            {
                hasXG = true;
            }

            if (IsXGParameterChange(sysExData))
            {
                hasXG = true; // usage-based detection
            }

            if (IsGSReset(sysExData))
            {
                hasGS = true;
            }

            if (IsGSDT1Message(sysExData))
            {
                hasGS = true; // usage-based detection
            }
        }

        // apply precedence rules
        // GM2 > GM1, vendor formats (XG/GS) noted with GM compatibility
        if (hasXG && hasGS)
        {
            return "XG/GS Mixed (GM-compatible)"; // rare but possible
                                                  // probably should not bet on this now that I think abt it
        }
        if (hasXG)
        {
            return hasGM2 ? "Yamaha XG (GM2-compatible)" : "Yamaha XG (GM-compatible)";
        }
        if (hasGS)
        {
            return hasGM2 ? "Roland GS (GM2-compatible)" : "Roland GS (GM-compatible)";
        }
        if (hasGM2)
        {
            return "General MIDI Level 2 (GM2)";
        }
        if (hasGM1)
        {
            return "General MIDI (GM)";
        }

        return "Unknown Format MIDI";
    }

    // enumerate all SysEx messages from a MIDI file, properly reassembling
    // split packets (NormalSysExEvent, then EscapeSysExEvent)
    // DryWetMIDI exposes the Completed flag to indicate if a packet has hit the terminating F7
    private IEnumerable<byte[]> EnumerateFullSysExMessages(MidiFile midiFile)
    {
        var buffer = new List<byte>();
        bool assembling = false;

        foreach (var evt in midiFile.GetTrackChunks().SelectMany(t => t.Events))
        {
            if (evt is NormalSysExEvent normalSysEx)
            {
                // if previously assembling a previous message, yield it first
                if (assembling && buffer.Count > 0)
                {
                    yield return buffer.ToArray();
                    buffer.Clear();
                }

                // start of new SysEx message (F0)
                buffer.AddRange(normalSysEx.Data);

                // check if message is complete
                // note: Completed flag may not always be set correctly, so we check
                // if the next event is NOT an EscapeSysExEvent or if Completed is true
                if (normalSysEx.Completed)
                {
                    // single-packet complete message
                    yield return buffer.ToArray();
                    buffer.Clear();
                    assembling = false;
                }
                else
                {
                    // treat as potentially complete but check for continuation
                    // in practice, most single-packet SysEx messages have Completed=false
                    // so we wait to see if next event is a continuation
                    assembling = true;
                }
            }
            else if (evt is EscapeSysExEvent escapeSysEx)
            {
                // continuation of a split SysEx message (F7)
                if (assembling)
                {
                    buffer.AddRange(escapeSysEx.Data);

                    if (escapeSysEx.Completed)
                    {
                        // message complete
                        yield return buffer.ToArray();
                        buffer.Clear();
                        assembling = false;
                    }
                }
                // if not assembling, ignore stray escape events
            }
            else if (assembling)
            {
                // hit a non-SysEx event while assembling, so previous message is complete
                yield return buffer.ToArray();
                buffer.Clear();
                assembling = false;
            }
        }

        // if we're still assembling at the end, yield the buffer
        if (assembling && buffer.Count > 0)
        {
            yield return buffer.ToArray();
        }
    }

    // references:
    // https://midi.org/community/midi-specifications/system-exclusive-events-gm-reset
    // http://www.studio4all.de/htmle/frameset090.html
    // http://studio4all.de/htmle/main91.html
    // http://www.jososoft.dk/yamaha/articles/midi_10.htm
    // https://web.archive.org/web/20060926124939/http://www.yamaha.co.uk/xg/reading/pdf/xg_spec.pdf
    // https://usa.yamaha.com/files/download/other_assets/8/320948/MU80E1.pdf
    // https://usa.yamaha.com/files/download/other_assets/9/320949/MU80E2.pdf
    // https://www.soundonsound.com/techniques/demystifying-yamahas-xg-soundcards
    // https://metacpan.org/pod/Win32API::MIDI::SysEX::Yamaha#Parameter-Change
    // 

    // check if SysEx data contains GM System On message: 7E 7F 09 01
    private bool IsGM1SystemOn(byte[] data)
    {
        return data.Length >= 4 &&
               data[0] == 0x7E && // Universal Non-Real Time SysEx
               data[1] == 0x7F && // Target all devices
               data[2] == 0x09 && // General MIDI message
               data[3] == 0x01;   // GM System On
    }

    // check if SysEx data contains GM2 System On message: 7E 7F 09 03
    private bool IsGM2SystemOn(byte[] data)
    {
        return data.Length >= 4 &&
               data[0] == 0x7E &&
               data[1] == 0x7F &&
               data[2] == 0x09 &&
               data[3] == 0x03; // GM2 System On
    }

    // check if SysEx data contains Yamaha XG System On or Reset: 43 1n 4C 00 00 7E/7F 00
    private bool IsXGSystemOnOrReset(byte[] data)
    {
        if (data.Length < 7)
            return false;

        return data[0] == 0x43 &&               // Yamaha ID
               (data[1] & 0xF0) == 0x10 &&      // Device number (0x10-0x1F)
               data[2] == 0x4C &&               // Model ID (XG)
               data[3] == 0x00 &&
               data[4] == 0x00 &&
               (data[5] == 0x7E || data[5] == 0x7F) && // System On (7E) or Reset (7F)
               data[6] == 0x00;
    }

    // check if SysEx data contains Yamaha XG Parameter Change: 43 1n 4C ...
    // this indicates XG usage even without explicit System On
    // any XG parameter change is a clear sign of XG format usage
    private bool IsXGParameterChange(byte[] data)
    {
        return data.Length >= 3 &&
               data[0] == 0x43 &&          // Yamaha ID
               (data[1] & 0xF0) == 0x10 && // Device number (0x10-0x1F)
               data[2] == 0x4C;            // Model ID (XG)
    }

    // check SysEx data for Roland GS Reset message against checksum verification
    // format: 41 dd 42 12 40 00 7F 00 cc
    // where cc = (128 - ((sum of addr+data) & 0x7F)) & 0x7F
    private bool IsGSReset(byte[] data)
    {
        if (data.Length < 9)
            return false;

        // check Roland GS header for GS Reset
        if (data[0] != 0x41 ||                           // Roland ID
            data[2] != 0x42 ||                           // Model ID (GS)
            data[3] != 0x12 ||                           // Command (DT1/Data Set)
            data[4] != 0x40 ||                           // Address MSB
            data[5] != 0x00 ||                           // Address
            data[6] != 0x7F ||                           // Address LSB (GS Reset)
            data[7] != 0x00)                             // Data
            return false;

        // verify device ID is valid (0x10-0x1F or 0x7F)
        byte deviceId = data[1];
        if (deviceId != 0x7F && (deviceId < 0x10 || deviceId > 0x1F))
            return false;

        // verify checksum
        int sum = data[4] + data[5] + data[6] + data[7]; // addr + data
        int expectedChecksum = (128 - (sum & 0x7F)) & 0x7F;
        return data[8] == expectedChecksum;
    }

    // check if SysEx data contains any Roland GS DT1 (Data Set) message
    // format: 41 dd 42 12 ...
    // any GS DT1 message is a clear sign of GS format usage
    private bool IsGSDT1Message(byte[] data)
    {
        if (data.Length < 4)
            return false;

        // verify device ID is valid (0x10-0x1F or 0x7F)
        byte deviceId = data[1];
        bool validDeviceId = deviceId == 0x7F || (deviceId >= 0x10 && deviceId <= 0x1F);

        return data[0] == 0x41 &&     // Roland ID
               validDeviceId &&       // Device ID
               data[2] == 0x42 &&     // Model ID (GS)
               data[3] == 0x12;       // Command (DT1/Data Set)
    }
}