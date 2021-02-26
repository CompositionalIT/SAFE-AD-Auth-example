#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"
#r "netstandard"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Farmer
open Farmer.Builders

Target.initEnvironment ()

let sharedPath = Path.getFullName "./src/Shared"
let serverPath = Path.getFullName "./src/Server"
let deployDir = Path.getFullName "./deploy"
let sharedTestsPath = Path.getFullName "./tests/Shared"
let serverTestsPath = Path.getFullName "./tests/Server"

let serverPublicPath = Path.getFullName "./src/Server/public"
let clientPath = Path.getFullName "./src/Client"
let clientPublicPath = Path.combine clientPath "public"

let npm args workingDir =
    let npmPath =
        match ProcessUtils.tryFindFileOnPath "npm" with
        | Some path -> path
        | None ->
            "npm was not found in path. Please install it and make sure it's available from your path. " +
            "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
            |> failwith

    let arguments = args |> String.split ' ' |> Arguments.OfArgs

    Command.RawCommand (npmPath, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let dotnet cmd workingDir =
    let result = DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

Target.create "Clean" (fun _ ->
    Shell.cleanDir deployDir
    Shell.cleanDir serverPublicPath)

Target.create "InstallClient" (fun _ -> npm "install" ".")

Target.create "Bundle" (fun _ ->
    dotnet (sprintf "publish -c Release -o \"%s\"" deployDir) serverPath
    npm "run build" "."
)

Target.create "Azure" (fun _ ->
    let web = webApp {
        name "safe-azure-ad"
        zip_deploy "deploy"
    }
    let deployment = arm {
        location Location.WestEurope
        add_resource web
    }

    deployment
    |> Deploy.execute "safe-azure-ad" Deploy.NoParameters
    |> ignore
)

Target.create "Run" (fun _ ->
    Shell.copyDir serverPublicPath clientPublicPath FileFilter.allFiles
    dotnet "build" sharedPath
    [ async { dotnet "watch run" serverPath }
      async { npm "run start" "." } ]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
)

Target.create "RunTests" (fun _ ->
    dotnet "build" sharedTestsPath
    [ async { dotnet "watch run" serverTestsPath }
      async { npm "run test:live" "." } ]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
)

open Fake.Core.TargetOperators

"Clean"
    ==> "InstallClient"
    ==> "Bundle"
    ==> "Azure"

"Clean"
    ==> "InstallClient"
    ==> "Run"

"Clean"
    ==> "InstallClient"
    ==> "RunTests"

Target.runOrDefaultWithArguments "Bundle"
