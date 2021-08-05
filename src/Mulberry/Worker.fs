module internal Mulberry.Worker

open TestCollect

type WorkerMsg =
    private
    | RunContext of TestContext
    | RunTest of Test
    | Stop

let create testDone =
    let runTest t =
        try
            match t with
            | Test (n, f) -> f ()
            | Wip (n, f) -> f ()
            | Skip (n, f) -> ()
            | Many (c, n, f) ->
                for _ = 1 to c do
                    f ()
        finally
            testDone t

    MailboxProcessor<WorkerMsg>.Start
        (fun self ->
            let rec loop () =
                async {
                    let! msg = self.Receive()

                    match msg with
                    | RunContext ctx ->
                        ctx.Tests |> List.iter runTest
                        return! loop ()
                    | RunTest t ->
                        runTest t
                        return! loop ()
                    | Stop -> return ()
                }

            loop ())

let createMany n testDone =
    [ for _ = 1 to n do
          create testDone ]

let runContext ctx (worker: MailboxProcessor<WorkerMsg>) = worker.Post(RunContext ctx)
let runTest test (worker: MailboxProcessor<WorkerMsg>) = worker.Post(RunTest test)
let stop (worker: MailboxProcessor<WorkerMsg>) = worker.Post Stop
