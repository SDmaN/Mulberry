open Mulberry
open Mulberry.TestCollect

[<EntryPoint>]
let main argv =
    "My test" &&&& fun () -> printfn "Hello world 1"
    "My test" &&& fun () -> printfn "Hello world 2"

    Runner.run (RunConfiguration(workersAmount = 2, failIfAnyWip = true))
    |> printfn "%A"

    0 // return an integer exit code
