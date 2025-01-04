import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

let loaderProgress = document.getElementById("progress");

dotnet.withModuleConfig({
    onDownloadResourceProgress: (loaded, total) => {
        var progress = Math.round((loaded / total) * 100);
        loaderProgress.innerText = "Downloading resources (" + progress + "%)";
        if (loaded === total && loaded !== 0) {
            loaderProgress.innerText = "Launching...";
            console.log("DownloadResourceProgress: Finished");
        }
    }
});

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

dotnetRuntime.setModuleImports("main.js", {
    window: {
        location: {
            hostname: () => globalThis.location.hostname
        }
    }
});

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [window.location.search]);