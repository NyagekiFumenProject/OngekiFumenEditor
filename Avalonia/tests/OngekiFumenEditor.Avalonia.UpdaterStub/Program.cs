var markerFilePath = Environment.GetEnvironmentVariable("ONGEKI_UPDATER_STUB_MARKER");
if (string.IsNullOrWhiteSpace(markerFilePath))
    return 2;

await File.WriteAllLinesAsync(markerFilePath, args);
return 0;
