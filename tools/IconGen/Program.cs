using ReIcon.Services;

// Копирует готовый .ico в ReIcon\app.ico: IconGen <путь\к\файлу.ico>
var pathArgs = args.Where(a => !string.IsNullOrWhiteSpace(a) && !a.StartsWith('-')).ToArray();
if (pathArgs.Length < 1)
{
    Console.Error.WriteLine("Usage: IconGen <source.ico>");
    Console.Error.WriteLine("Copies the file to ReIcon\\app.ico");
    return 1;
}

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var dst = Path.Combine(repoRoot, "ReIcon", "app.ico");
var src = Path.GetFullPath(pathArgs[0]);

AppIconFactory.CopyIconFile(src, dst);
Console.WriteLine($"OK: {dst}");
return 0;
