# AsarLib
Lightweight C# library for manipulating Electron ASAR archives.

# 📖 Usage

### Create a New ASAR
```C#
using (var fs = new Filesystem())
{
    // Add directory  
    fs.Files.Add("docs", new Filesystem.FileEntry
    {
        Type = Filesystem.FileType.Directory,
        Files = new Dictionary<string, Filesystem.FileEntry>()
    });

    // Add file with string content  
    fs.Files.Add("app.js", new Filesystem.FileEntry
    {
        Type = Filesystem.FileType.File,
        Data = new Filesystem.FileData("console.log('Hello World!');")
    });

    // Add file with byte content  
    fs.Files.Add("app.png", new Filesystem.FileEntry
    {
        Type = Filesystem.FileType.File,
        Data = new Filesystem.FileData(File.ReadAllBytes("app.png"))
    });


    fs.Save(File.Create("new.asar"));
}
```

### Modify Existing ASAR
```C#
using (var fs = new Filesystem(File.OpenRead("app.asar")))
{
    // Override a file's content with string 
    var mainScript = fs.Files["src"].Files
        .FirstOrDefault(name => name.Key.StartsWith("main-") && name.Key.EndsWith(".js"));
    mainScript.Value.Data.Override("console.log('Patched!');");

    // Override a file's content with bytes
    fs.Files["logo.png"].Data.Override(File.ReadAllBytes("custom-logo.png"));

    // Delete a file  
    fs.Files["temp"].Files.Remove("deprecated.log");

    fs.Save(File.Create("modified.asar"));
}
```

### Read/Modify Fuse
```C#
using (var stream = File.Open("app.exe", FileMode.Open, FileAccess.ReadWrite))
{
    var electron = new ElectronExecutable(stream);
    Console.WriteLine(electron.OnlyLoadAppFromAsar);
    electron.EnableEmbeddedAsarIntegrityValidation = ElectronExecutable.FuseState.Removed;
    electron.Save();
}
```

# 🤝 Contributing
Pull requests welcome!

# 📜 License
MIT
