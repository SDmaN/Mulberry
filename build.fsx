#r "paket: groupref Fake //"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.DotNet

let repositoryUrl = "https://github.com/SDmaN/Mulberry"

let licenseFile = "LICENSE"

let licenseUrl =
    sprintf "https://github.com/SDmaN/Mulberry/blob/main/%s" licenseFile

let changeLogUrl =
    "https://github.com/SDmaN/Mulberry/blob/main/CHANGELOG.md"

let configuration = DotNet.BuildConfiguration.Release

let projectName = "Mulberry"
let slnName = sprintf "%s.sln" projectName
let slnPath = slnName
let fsProjName = sprintf "%s.fsproj" projectName

let fsProjPath =
    fsProjName
    |> Path.combine projectName
    |> Path.combine "src"

let summary = "Lightweight E2E testing library"
let author = "Serdyukov Dmitrii (SDmaN)"

let release = ReleaseNotes.load "CHANGELOG.md"

Target.create "Clean" (fun _ -> !! "src/**/bin" ++ "src/**/obj" |> Shell.cleanDirs)

Target.create
    "AssemblyInfo"
    (fun _ ->
        let changeFsProj proj property value =
            Xml.poke proj (sprintf "Project/PropertyGroup/%s/text()" property) value

        let rootBuildProps = "Directory.Build.props"

        [ ("Version", release.NugetVersion)
          ("Authors", author)
          ("RepositoryUrl", repositoryUrl)
          ("PackageProjectUrl", repositoryUrl)
          ("PackageLicenseFile", licenseFile)
          ("FsDocsLicenseLink", licenseUrl)
          ("FsDocsReleaseNotesLink", changeLogUrl) ]
        |> List.iter (fun (p, v) -> changeFsProj rootBuildProps p v)

        changeFsProj fsProjPath "Description" summary)

Target.create
    "Format"
    (fun _ ->
        let sourceFiles =
            !! "src/**/*.fs" ++ "src/**/*.fsi" ++ "build.fsx"
            -- "src/**/obj/**/*.fs"

        let res =
            sourceFiles
            |> String.concat " "
            |> DotNet.exec id "fantomas"

        if not res.OK then
            failwith "Errors while formatting files")

Target.create "Build" (fun _ -> DotNet.build (fun p -> { p with Configuration = configuration }) slnPath)

let processDocs cmd =
    let res =
        [ cmd
          "--strict"
          "--clean"
          "--output .fsdocs/output"
          sprintf "--properties Configuration=%s" (configuration.ToString()) ]
        |> String.concat " "
        |> DotNet.exec id "fsdocs"

    if not res.OK then
        failwith "Errors while buildng docs"

Target.create "BuildDocs" (fun _ -> processDocs "build")
Target.create "WatchDocs" (fun _ -> processDocs "watch")

Target.create "Default" ignore

"Build" ==> "WatchDocs"

"Clean"
==> "AssemblyInfo"
==> "Format"
==> "Build"
==> "BuildDocs"
==> "Default"

Target.runOrDefault "Default"
