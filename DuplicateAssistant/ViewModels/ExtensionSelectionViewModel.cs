using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class ExtensionOption : ViewModelBase
{
    private readonly Action? _changed;
    private bool _isSelected;

    public ExtensionOption(string extension, Action? changed = null)
    {
        Extension = extension;
        _changed = changed;
    }

    public string Extension { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isSelected, value))
            {
                _changed?.Invoke();
            }
        }
    }
}

/// <summary>
/// Multi-select extension filter used by the duplicate tabs. An empty selection
/// means all files, matching the existing filter syntax.
/// </summary>
public class ExtensionSelectionViewModel : ViewModelBase
{
    private static readonly string[] CommonExtensions =
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".tif", ".tiff",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".md", ".csv", ".json", ".xml", ".html", ".css",
        ".zip", ".rar", ".7z", ".tar", ".gz",
        ".mp3", ".flac", ".wav", ".ogg", ".m4a",
        ".mp4", ".mkv", ".avi", ".mov", ".webm",
        ".iso", ".exe", ".dll", ".cs", ".js", ".ts", ".py", ".java", ".go", ".rs"
    };

    private bool _suppressChanged;
    private bool _isOpen;
    private string _newExtension = string.Empty;

    public ExtensionSelectionViewModel()
    {
        Options = new ObservableCollection<ExtensionOption>(
            CommonExtensions.Select(extension => new ExtensionOption(extension, OnOptionChanged)));

        ClearCommand = ReactiveCommand.Create(Clear);
        AddExtensionCommand = ReactiveCommand.Create(AddCustomExtension);
    }

    public event EventHandler? SelectionChanged;

    public ObservableCollection<ExtensionOption> Options { get; }

    public bool IsOpen
    {
        get => _isOpen;
        set => this.RaiseAndSetIfChanged(ref _isOpen, value);
    }

    public ReactiveCommand<Unit, Unit> ClearCommand { get; }

    public ReactiveCommand<Unit, Unit> AddExtensionCommand { get; }

    public string NewExtension
    {
        get => _newExtension;
        set => this.RaiseAndSetIfChanged(ref _newExtension, value);
    }

    public string Summary
    {
        get
        {
            int selected = Options.Count(option => option.IsSelected);

            return selected switch
            {
                0 => "All files",
                1 => "1 extension",
                _ => $"{selected} extensions"
            };
        }
    }

    public string ToFilterString()
    {
        return string.Join(
            ",",
            Options.Where(option => option.IsSelected).Select(option => option.Extension));
    }

    public void SetFromFilterString(string? value)
    {
        _suppressChanged = true;

        try
        {
            foreach (ExtensionOption option in Options)
            {
                option.IsSelected = false;
            }

            foreach (string rawExtension in (value ?? string.Empty).Split(
                         new[] { ',', ';' },
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string extension = Normalize(rawExtension);
                EnsureOption(extension).IsSelected = true;
            }
        }
        finally
        {
            _suppressChanged = false;
        }

        NotifyChanged();
    }

    public void Clear()
    {
        _suppressChanged = true;

        try
        {
            foreach (ExtensionOption option in Options)
            {
                option.IsSelected = false;
            }
        }
        finally
        {
            _suppressChanged = false;
        }

        NotifyChanged();
    }

    public void AddExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return;
        }

        string normalized = Normalize(extension);
        EnsureOption(normalized).IsSelected = true;
        NotifyChanged();
    }

    private void AddCustomExtension()
    {
        if (string.IsNullOrWhiteSpace(NewExtension))
        {
            return;
        }

        AddExtension(NewExtension);
        NewExtension = string.Empty;
    }

    private void OnOptionChanged()
    {
        if (!_suppressChanged)
        {
            NotifyChanged();
        }
    }

    private void NotifyChanged()
    {
        this.RaisePropertyChanged(nameof(Summary));
        this.RaisePropertyChanged(nameof(FilterString));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // Convenience for bindings that want the same string as the text filter.
    public string FilterString => ToFilterString();

    private ExtensionOption EnsureOption(string extension)
    {
        ExtensionOption? option = Options.FirstOrDefault(candidate =>
            string.Equals(candidate.Extension, extension, StringComparison.OrdinalIgnoreCase));

        if (option is not null)
        {
            return option;
        }

        option = new ExtensionOption(extension, OnOptionChanged);
        Options.Add(option);
        return option;
    }

    private static string Normalize(string extension)
    {
        string value = extension.Trim();

        if (value.StartsWith("*.", StringComparison.Ordinal))
        {
            value = value[1..];
        }
        else if (!value.StartsWith('.'))
        {
            value = "." + value;
        }

        return value.ToLowerInvariant();
    }
}
