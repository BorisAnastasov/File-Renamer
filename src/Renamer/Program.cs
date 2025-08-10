using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var opts = Options.Parse(args);
            if (opts.ShowHelp)
            {
                Options.PrintHelp();
                return 0;
            }
            //
            Validate(opts);
            Run(opts);
            return 0;
        }
        catch (OptionException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}\n");
            Options.PrintHelp();
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nFatal: {ex.Message}\n{ex}\n");
            return 1;
        }
    }

    //Normalize and sanity-check all options before doing any work
    private static void Validate(Options o)
    {
        if (string.IsNullOrWhiteSpace(o.Folder) || !Directory.Exists(o.Folder))
            throw new OptionException("--folder is required and must exist");

        if (!string.IsNullOrEmpty(o.Template))
        {
            // check tokens
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name", "ext", "n", "date" };
            foreach (Match m in Regex.Matches(o.Template, "\\{(.*?)\\}"))
            {
                var token = m.Groups[1].Value.Trim();
                if (!allowed.Contains(token))
                    throw new OptionException($"Unknown token '{{token}}' in --template. Allowed: {{name}}, {{ext}}, {{n}}, {{date}}");
            }
        }

        if (!string.IsNullOrEmpty(o.ChangeExt))
        {
            o.ChangeExt = NormalizeExt(o.ChangeExt);
        }

        if (!string.IsNullOrEmpty(o.FilterExt))
        {
            var list = new List<string>();
            foreach (var part in o.FilterExt.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                list.Add(NormalizeExt(part));
            o.FilterExtList = list;
        }

        if (!string.IsNullOrEmpty(o.UndoLog))
        {
            var dir = Path.GetDirectoryName(o.UndoLog);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        if (o.SequenceDigits < 1 || o.SequenceDigits > 8)
            throw new OptionException("--sequence-digits must be 1..8");

        if (!string.IsNullOrEmpty(o.NameCase))
        {
            var v = o.NameCase.ToLowerInvariant();
            if (v != "lower" && v != "upper" && v != "title" && v != "none")
                throw new OptionException("--name-case must be lower|upper|title|none");
        }

        if (!string.IsNullOrEmpty(o.Conflict))
        {
            var v = o.Conflict.ToLowerInvariant();
            if (v != "skip" && v != "overwrite" && v != "increment")
                throw new OptionException("--conflict must be skip|overwrite|increment");
        }
    }

    private static string NormalizeExt(string ext)
    {
        ext = ext.Trim();
        if (!ext.StartsWith('.')) ext = "." + ext;
        return ext.ToLowerInvariant();
    }

    private static void Run(Options o)
    {
        var searchOpt = o.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(o.Folder, "*", searchOpt);

        var list = new List<(string from, string to)>();
        var seq = o.SequenceStart;
        var dtNow = DateTime.Now;

        foreach (var path in files)
        {
            var fi = new FileInfo(path);
            if (!o.IncludeHidden && (fi.Attributes & FileAttributes.Hidden) != 0) continue;

            var ext = fi.Extension;
            if (o.FilterExtList != null && o.FilterExtList.Count > 0 && !o.FilterExtList.Contains(ext.ToLowerInvariant()))
                continue;

            var nameNoExt = Path.GetFileNameWithoutExtension(fi.Name);

            // 1) preprocess name
            var processed = nameNoExt;
            if (!string.IsNullOrEmpty(o.ReplaceOld))
                processed = processed.Replace(o.ReplaceOld, o.ReplaceNew ?? string.Empty);
            if (!string.IsNullOrEmpty(o.RegexPattern))
                processed = Regex.Replace(processed, o.RegexPattern, o.RegexReplacement ?? string.Empty);
            if (!string.IsNullOrEmpty(o.NameCase))
                processed = ApplyCase(processed, o.NameCase!);

            // 2) extension change
            var finalExt = !string.IsNullOrEmpty(o.ChangeExt) ? o.ChangeExt! : ext;

            // 3) template or prefix/suffix
            var nStr = seq.ToString(new string('0', o.SequenceDigits));
            var dateStr = dtNow.ToString(string.IsNullOrEmpty(o.DateFormat) ? "yyyyMMdd" : o.DateFormat);

            string finalName;
            if (!string.IsNullOrEmpty(o.Template))
            {
                finalName = o.Template!;
                finalName = finalName.Replace("{name}", processed)
                                     .Replace("{ext}", finalExt.TrimStart('.'))
                                     .Replace("{n}", nStr)
                                     .Replace("{date}", dateStr);
            }
            else
            {
                finalName = $"{o.Prefix}{processed}{o.Suffix}";
            }

            finalName = SanitizeFileName(finalName);

            // enforce max length (basic)
            var max = o.MaxLength <= 0 ? 255 : o.MaxLength;
            if (finalName.Length > max) finalName = finalName.Substring(0, max);

            var targetPath = Path.Combine(fi.DirectoryName!, finalName + finalExt);

            // handle conflicts
            if (File.Exists(targetPath))
            {
                var mode = (o.Conflict ?? "increment").ToLowerInvariant();
                if (mode == "skip")
                {
                    continue; // skip this file
                }
                else if (mode == "overwrite")
                {
                    // allowed, will overwrite below
                }
                else // increment
                {
                    int inc = 1;
                    string candidate;
                    do
                    {
                        candidate = Path.Combine(fi.DirectoryName!, $"{finalName}_{inc}{finalExt}");
                        inc++;
                    } while (File.Exists(candidate));
                    targetPath = candidate;
                }
            }

            list.Add((fi.FullName, targetPath));
            seq++;
        }

        // Preview table
        Console.WriteLine($"Found {list.Count} file(s). Mode: {(o.Apply ? "APPLY" : "DRY-RUN")}");
        foreach (var (from, to) in list)
        {
            Console.WriteLine($"{from} -> {to}");
        }

        if (!o.Apply)
        {
            Console.WriteLine("\nDry run only. Use --apply to perform rename.");
            return;
        }

        // Apply and write undo
        using var undo = string.IsNullOrEmpty(o.UndoLog) ? null : new StreamWriter(o.UndoLog!, append: true, Encoding.UTF8);
        if (undo != null)
        {
            undo.WriteLine("timestamp,from,to");
        }

        int renamed = 0, errors = 0;
        foreach (var (from, to) in list)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Move(from, to, overwrite: (o.Conflict ?? "increment").Equals("overwrite", StringComparison.OrdinalIgnoreCase));
                renamed++;
                undo?.WriteLine($"{DateTime.Now:O},\"{from}\",\"{to}\"");
            }
            catch (Exception ex)
            {
                errors++;
                Console.Error.WriteLine($"Failed: {from} -> {to} | {ex.Message}");
            }
        }

        Console.WriteLine($"\nDone. Renamed: {renamed}, Errors: {errors}");
        if (!string.IsNullOrEmpty(o.UndoLog))
            Console.WriteLine($"Undo log: {o.UndoLog}");
    }

    //Change the case of the base filename (no extension)
    private static string ApplyCase(string input, string mode)
    {
        switch (mode.ToLowerInvariant())
        {
            case "lower": return input.ToLowerInvariant();
            case "upper": return input.ToUpperInvariant();
            case "title":
                TextInfo ti = CultureInfo.InvariantCulture.TextInfo;
                return ti.ToTitleCase(input.Replace('_', ' ')).Replace(' ', '_');
            default: return input;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (Array.IndexOf(invalid, ch) >= 0)
                sb.Append('_');
            else
                sb.Append(ch);
        }
        return sb.ToString().Trim();
    }

    private sealed class Options
    {
        public string? Folder { get; set; }
        public bool Recursive { get; set; }
        public bool IncludeHidden { get; set; }
        public string? FilterExt { get; set; }
        public List<string>? FilterExtList { get; set; }

        public string? Template { get; set; }
        public string Prefix { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;

        public string? ReplaceOld { get; set; }
        public string? ReplaceNew { get; set; }
        public string? RegexPattern { get; set; }
        public string? RegexReplacement { get; set; }

        public string? ChangeExt { get; set; }
        public string? DateFormat { get; set; }
        public int SequenceStart { get; set; } = 1;
        public int SequenceDigits { get; set; } = 3;
        public int MaxLength { get; set; } = 255;

        public string? NameCase { get; set; } // lower|upper|title|none
        public string? Conflict { get; set; } // skip|overwrite|increment

        public bool Apply { get; set; } = false;
        public string? UndoLog { get; set; }

        public bool ShowHelp { get; set; } = false;

        public static Options Parse(string[] args)
        {
            var option = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string? Next() => (++i < args.Length) ? args[i] : throw new OptionException($"Missing value for {a}");

                switch (a)
                {
                    case "--help": case "-h": option.ShowHelp = true; break;
                    case "--folder": option.Folder = Next(); break;
                    case "--recursive": option.Recursive = true; break;
                    case "--include-hidden": option.IncludeHidden = true; break;
                    case "--filter-ext": option.FilterExt = Next(); break;

                    case "--template": option.Template = Next(); break;
                    case "--prefix": option.Prefix = Next(); break;
                    case "--suffix": option.Suffix = Next(); break;

                    case "--replace": option.ReplaceOld = Next(); option.ReplaceNew = Next(); break;
                    case "--regex": option.RegexPattern = Next(); option.RegexReplacement = Next(); break;

                    case "--change-ext": option.ChangeExt = Next(); break;
                    case "--date-format": option.DateFormat = Next(); break;
                    case "--sequence-start": option.SequenceStart = int.Parse(Next()); break;
                    case "--sequence-digits": option.SequenceDigits = int.Parse(Next()); break;
                    case "--max-length": option.MaxLength = int.Parse(Next()); break;

                    case "--name-case": option.NameCase = Next(); break;
                    case "--conflict": option.Conflict = Next(); break;

                    case "--apply": option.Apply = true; break;
                    case "--undo-log": option.UndoLog = Next(); break;

                    default:
                        throw new OptionException($"Unknown argument: {a}");
                }
            }

            if (string.IsNullOrWhiteSpace(option.Folder))
                option.Folder = Directory.GetCurrentDirectory();


            option.Folder = Path.GetFullPath(option.Folder);

            return option;
        }

        public static void PrintHelp()
        {
            Console.WriteLine("""
    ======================================================
          C# File Renamer - Command Line Tool
    ======================================================

    Rename files in bulk with flexible templates, prefixes,
    suffixes, and case changes. Supports dry-run previews
    before making any changes.

    Usage:
      Renamer [--folder <path>] [options]

    If --folder is omitted, the current directory is used.

    Options:
      --folder <path>              Root folder to process (optional)
      --recursive                  Include subfolders
      --include-hidden             Include hidden files
      --filter-ext ".jpg,.png,.txt"     Comma-separated list of extensions to include

    Naming:
      --template "{date}_{n}_{name}"  Filename template
                                     Tokens:
                                       {name}  - original base name
                                       {ext}   - extension (no dot)
                                       {n}     - sequence number
                                       {date}  - date stamp

      --prefix <text>              Prefix (ignored if --template is set)
      --suffix <text>              Suffix (ignored if --template is set)

      --replace <old> <new>        Simple string replacement in base name
      --regex <pattern> <repl>     Regex replacement in base name
      --name-case <mode>           lower | upper | title | none
      --date-format <fmt>          .NET date format (default: yyyyMMdd)
      --sequence-start <n>         Starting sequence number (default: 1)
      --sequence-digits <n>        Zero-padding width (default: 3)
      --change-ext <.ext>          Force a new extension
      --max-length <n>             Max filename length (default: 255)

    Conflict Handling:
      --conflict <mode>            skip | overwrite | increment (default: increment)

    Execution:
      --apply                      Actually rename files (default: dry-run)
      --undo-log <path>            Write CSV log for undoing changes

    Help:
      -h, --help                   Show this help message

    Examples:
      Dry-run preview with template:
        Renamer --template "{date}_{n}_{name}"

      Apply renames with prefix, change extension:
        Renamer --prefix "IMG_" --change-ext .jpg --apply

      Regex cleanup and title case:
        Renamer --regex "[ _]+" "_" --name-case title --apply
    ======================================================
    """);
        }
    }

    private sealed class OptionException : Exception
    {
        public OptionException(string message) : base(message) { }
    }
}