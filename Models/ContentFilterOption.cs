namespace Remnant2UnlockerApp.Models;

// One entry of the "All content / Base game / <DLC>" filter; Key is a DlcCatalog filter key.
public sealed record ContentFilterOption(string Key, string DisplayName);
