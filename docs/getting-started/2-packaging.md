| [docs](..) / [getting-started](.) / 2-packaging.md |
|:---|

# Step 2. Packaging

Packaging is the process of building, packing, and preparing MyApp release packages for distribution.

## Building

The first step in preparing the application for distribution is to build the application. 

1. **Set MyApp Version** - set the initial application version.
 
   	**`Properties\AssemblyInfo.cs`**
   
   	~~~cs
  	[assembly: AssemblyVersion("1.0.0")]
	[assembly: AssemblyFileVersion("1.0.0")]
   	~~~
2. **Switch to Release** - switch your build configuration to `Release`.
3. **Build MyApp** - build your application to ensure the latest changes are included in the package we will be creating.

## Packing

Squirrel uses [NuGet](https://www.NuGet.org/) for bundling application files and various application properties (e.g., application name, version, description) in a single release package.

Section [NuGet Package Metadata](../using/nuget-package-metadata.md) provides additional details on using NuGet and `.nuspec` files to automate the packing of your application. We will be going through the process using the [NuGet Package Explorer](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer) to manually create a NuGet package.

1. **Creating a New NuGet Package** - the first step is to create a new NuGet package.
2. **Edit Metadata** - update package metadata for MyApp.
   * **Id** - name of the application (no spaces)
   * **Version** - version specified in `Properties\Assembly.cs`
   * **Dependencies** - Squirrel expects no dependencies in the package (all files should be explicitly added to the package)
3. **Add lib & net45** - add the `lib` folder and the `net45` folder to the project. Squirrel is expecting a single `lib / net45` directory provided regardless of whether your app is a `net45` application.
4. **Add Release Files** - add all the files from `bin\Release` needed by MyApp to execute (including the various files required by Squirrel).
   * **Include MyApp Files:** MyApp.exe, MyApp.exe.config, any non-standard .NET dll's needed by MyApp.exe.
   * **Include Squirrel Files:** Squirrel.dll, Splat.dll, NuGet.Squirrel.dll, Mono.Cecil.\*, DeltaCompressionDotNet.\*,
   * **Exclude:** *.vshost.\*, *.pdb files 
5. **Save the NuGet Package File** - save the NuGet package file to where you can easily access later (e.g., `MyApp.sln` directory). Follow the given naming format (e.g., `MyApp.1.0.0.nupkg`).
 
![](images/1.2-nuget-package-explorer.png)

## Releasifying

Releasifying is the process of preparing the `MyApp.1.0.0.nupkg` for distribution. 

### Using Releasify

You use the `Squirrel.exe` tool that was included in the Squirrel.Windows package you installed in the `MyApp.sln` previously. 

Use the [Package Manager Console](https://docs.NuGet.org/consume/package-manager-console) to execute `Squirrel.exe --releasify` command.

~~~powershell
PM> Squirrel --releasify MyApp.1.0.0.nupkg
~~~ 

**Tip:** If you get an error stating that `...'Squirrel' is not recognized...` then you may simply need to restart Visual Studio so the `Package Manager Console` will have loaded all the package tools.

### Releasify Output

The `Squirrel --releasify` command completes the following:

* **Create `Releases` Directory** - creates a Releases directory (in the `MyApp.sln` directory by default). 
* **Create `Setup.exe`** - creates a `Setup.exe` file which includes the latest version of the application to be installed.
* **Create JSON `RELEASES` File** - creates a versioned JSON file that provides a list of all release files for MyApp to be used during the update process. The filename remains exactly `RELEASES`.
* **Create `MyApp.1.0.0-full.nupkg`** - copies the package you created to the `Releases` directory.
* **Create `MyApp.*.*.*-delta.nupkg`** - if you are releasing an update, releasify creates a delta file package to reduce the update package size (see [Updating](5-updating.md) for details).

**`C:\Projects\MyApp\Releases`**

![](images/1.2-releases-directory.png)

## Testing your update logic with FakeUpdateManager

Test update orchestration in your own application test project. Reference the `burrow.windows` package, inject `IUpdateManager` into the consumer's updater service, and use `Squirrel.Testing.FakeUpdateManager`; do not use repository test helpers or `SquirrelAwareApp.exe` for this unit test. Production and test code use the same interface:

~~~cs
using System;
using System.Threading.Tasks;
using NuGet;
using Squirrel;
using Squirrel.Testing;
using Xunit;

public sealed class AppUpdater
{
    readonly IUpdateManager manager;

    public AppUpdater(IUpdateManager manager)
    {
        this.manager = manager;
    }

    public async Task<ReleaseEntry> UpdateAsync()
    {
        return await manager.UpdateApp();
    }
}

var fake = new FakeUpdateManager("MyApp", initialVersion: new SemanticVersion("1.0.0"));
fake.PublishRelease(new SemanticVersion("1.1.0"));

var release = await new AppUpdater(fake).UpdateAsync();

Assert.Equal("1.1.0", release.Version.ToString());
Assert.Equal("1.1.0", fake.CurrentVersion.ToString());
Assert.True(fake.IsUninstallerRegistered);
Assert.Equal(FakeUpdateOperation.ApplyReleases, fake.Calls[2].Operation);
~~~

`FakeUpdateManager` keeps releases and installation state in memory. It never creates package files, writes the registry, starts child processes, or downloads from a feed. Assert the orchestration through `CurrentVersion`, `IsInstalled`, `IsUninstallerRegistered`, `Shortcuts`, and `Calls`.

Test the consumer's error path with a persistent failure. `UpdateApp` retries once with delta updates disabled, so a persistent failure is delivered after both attempts:

~~~cs
var fake = new FakeUpdateManager("MyApp", initialVersion: new SemanticVersion("1.0.0"));
fake.PublishRelease(new SemanticVersion("1.1.0"));
fake.Fail(FakeUpdateOperation.DownloadReleases, new InvalidOperationException("offline"));

try
{
    var error = await Assert.ThrowsAsync<InvalidOperationException>(
        () => new AppUpdater(fake).UpdateAsync());
    Assert.Equal("offline", error.Message);
}
finally
{
    fake.ClearFailure(FakeUpdateOperation.DownloadReleases);
}
~~~

Use `FailNext` separately for a direct `IUpdateManager` method test when only one attempt should fail; the queued exception is consumed by the next invocation.

## Testing a real packaged application update

Keep one consumer-owned smoke test for packaging and process behavior. Create two of your own `MyApp` packages, `0.1.0` and `0.2.0`, and put a version-specific `lib\\net45\\version.txt` in each package. In the app startup code, register the update callback with `SquirrelAwareApp.HandleEvents(onAppUpdate: ...)` and write an `updated|<version>` marker from that callback. Generate `RELEASES` for the feed containing both packages.

Run a real `UpdateManager` with a disposable `rootDirectory`, call `FullInstall(silentInstall: true)` for `0.1.0`, then call `UpdateApp()` after publishing `0.2.0`. Assert `app-0.1.0\\version.txt`, `app-0.2.0\\version.txt`, and the `updated|0.2.0` marker from the application process. Use temporary feed and install paths and clean up the disposable root directory.

This smoke test is separate from the `FakeUpdateManager` unit test: the fake validates consumer orchestration and error handling, while the real manager validates package layout, `RELEASES`, extraction, process startup, and the Squirrel-aware application callback.

## See Also

* [Visual Studio Build Packaging](../using/visual-studio-packaging.md) - integrating NuGet packaging into your visual studio build process to include packing and releasifying.


---
| Previous: [1. Integrating](1-integrating.md) | Next: [3. Distributing](3-distributing.md)|
|:---|:---|
