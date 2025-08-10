# 📂 File Renamer Pro — Batch File Rename Tool (C# .NET 8)

**File Renamer Pro** is a lightweight and fast batch file renaming tool built in **C# (.NET 8)**.  
It lets you rename hundreds or thousands of files in seconds using flexible templates, sequence numbers, date tokens, find/replace, and regex.  
By default, it runs in **dry-run mode** to preview changes before making them, and it can generate an **undo log** for safety.

---

## ✨ Features
- **Flexible templates** — Use `{name}`, `{ext}`, `{n}`, `{date}` in your patterns.
- **Date formatting** — Fully customizable date/time formats (`yyyyMMdd`, `yyyy-MM-dd_HHmm`, etc.).
- **Numbering** — Sequential numbers with configurable start and padding (`001`, `002`...).
- **Search & Replace** — Simple or regex-based replacements.
- **Extension change** — Force or change file extensions in bulk.
- **Case formatting** — Lowercase, uppercase, title case.
- **Conflict handling** — Skip, overwrite, or auto-increment file names.
- **Dry-run mode** — Preview renames before applying.
- **Undo log** — Export before/after file names to CSV.

---

## 📦 Installation

### Option 1 — Download & Run
1. Go to the **[Releases](../../releases)** section.
2. Download the latest `FileRenamerPro.zip`.
3. Extract it to any folder.
4. Open **Command Prompt** or **PowerShell** in that folder.
5. Run `Renamer.exe` with your desired options.

### Option 2 — Install to PATH (optional)
Run the included `install.bat` to add File Renamer Pro to your system PATH so you can run it from anywhere:
```bash
install.bat

## If you ever want to remove it completely from your system, use the included uninstall

