using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vivace.Models;
using Vivace.Services;

namespace Vivace.ViewModels;

// this is the main view model
// handles the logic of opening and analyzing midi files for now
public partial class MainWindowViewModel : ViewModelBase
{
    // load MIDI import service
    private readonly MidiFileService _midiFileService;

    // reference to the window (can be null)
    private readonly Window? _window;

    // midi file info can be displayed
    [ObservableProperty]
    private MidiFileInfo? _currentMidiFile;

    [ObservableProperty]
    private bool _isFileLoaded;

    // stores any error message
    [ObservableProperty]
    private string? _errorMessage;

    // constructor
    // the window parameter is optional
    public MainWindowViewModel(Window? window = null)
    {
        // store the window reference
        _window = window;

        // create instance of midi file service
        _midiFileService = new MidiFileService();
    }

    // if called "open midi file" has been clicked
    // [RelayCommand] exposes this to UI
    // async to avoid hanging UI
    [RelayCommand]
    private async Task OpenMidiFileAsync()
    {
        // first, check if we have a window reference
        // if not, we can't show the file picker dialog
        if (_window == null)
        {
            ErrorMessage = "Cannot open file picker: Window reference not available.";
            return;
        }

        try
        {
            // clear any previous error messages
            ErrorMessage = null;

            // options for the picker (probably enough)
            var filePickerOptions = new FilePickerOpenOptions
            {
                Title = "Open MIDI File",
                AllowMultiple = false, // one file at a time for now
                FileTypeFilter = new[]
                {
                    // filter 1: only show midi files
                    new FilePickerFileType("MIDI Files")
                    {
                        Patterns = new[] { "*.mid", "*.midi" },
                        MimeTypes = new[] { "audio/midi", "audio/x-midi" }
                    },
                    // filter 2: show all files
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" } // show everything
                    }
                }
            };

            // show picker and wait for a choice
            var selectedFiles = await _window.StorageProvider.OpenFilePickerAsync(filePickerOptions);

            // no selection means user probably canceled
            if (selectedFiles.Count == 0)
            {
                return;
            }

            // take first file (only one anyways)
            var selectedFile = selectedFiles[0];
            // grab path as string to pass to the service
            var filePath = selectedFile.Path.LocalPath;

            // load and try to parse the MIDI
            // could throw if the file is bad
            var midiFileInfo = _midiFileService.LoadMidiFile(filePath);

            // store what we got so the UI can show it
            CurrentMidiFile = midiFileInfo;
            // set flag to file loaded
            IsFileLoaded = true;
        }
        catch (Exception ex)
        {
            // catch exception here if any
            ErrorMessage = $"Failed to load MIDI file: {ex.Message}";

            // reset state
            IsFileLoaded = false;
            CurrentMidiFile = null;
        }
    }
}