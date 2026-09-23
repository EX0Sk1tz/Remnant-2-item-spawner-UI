using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Remnant2UnlockerApp.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly string[] SupportedLanguages = { "en", "de" };

    private readonly Dictionary<string, string> _fallback;
    private Dictionary<string, string> _current;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationService(string language)
    {
        _fallback = LoadEmbedded("en");
        Language = SupportedLanguages.Contains(language) ? language : "en";
        _current = Language == "en" ? _fallback : LoadEmbedded(Language);
    }

    public string Language { get; private set; }

    public string this[string key] =>
        _current.TryGetValue(key, out var value) ? value :
        _fallback.TryGetValue(key, out var fallbackValue) ? fallbackValue :
        key;

    public void SetLanguage(string language)
    {
        var code = SupportedLanguages.Contains(language) ? language : "en";

        if (code == Language)
            return;

        Language = code;
        _current = code == "en" ? _fallback : LoadEmbedded(code);

        // Empty property name is the INotifyPropertyChanged convention for "everything
        // changed" -- WPF re-evaluates every binding on this object, including the
        // indexer bindings ({Binding Loc[Key]}) that a specific key name wouldn't reach.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private static Dictionary<string, string> LoadEmbedded(string code)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{assembly.GetName().Name}.Localization.{code}.json";

        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
            return new Dictionary<string, string>();

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>();
    }
}
