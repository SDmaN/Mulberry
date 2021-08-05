namespace Mulberry

open System
open TestCollect

/// <summary>
/// Parameters of testing process.
/// </summary>
/// <param name="workersAmount">Amount of test workers (browsers) that run in parallel. Defaults to 1.</param>
/// <param name="failIfAnyWip">Run will fail if there is at least one wip test. Defaults to false.</param>
type RunConfiguration(?workersAmount: int, ?failIfAnyWip: bool) =
    let workersAmount = defaultArg workersAmount 1
    let failIfAnyWip = defaultArg failIfAnyWip false

    /// <summary>
    /// Returns amount of test workers (browsers) that run in parallel.
    /// </summary>
    member _.WorkersAmount = workersAmount

    /// <summary>
    /// Returns value indicating run will fail if there is at least one wip test..
    /// </summary>
    member _.FailIfAnyWip = failIfAnyWip


/// <summary>
/// Contains functions for tests execution.
/// </summary>
module Runner =
    type private RunnerMsg =
        | Start of
            RunConfiguration *
            TestContext *  // Global context
            TestContext list *  // Non global contexts
            AsyncReplyChannel<int>
        | TestDone of Test

    let private calcWorkersNeeded (cfg: RunConfiguration) (contexts: TestContext list) globalContext =
        Math.Min(cfg.WorkersAmount, contexts.Length + globalContext.Tests.Length)

    let private calcTotalTests contexts globalContext =
        (contexts |> List.sumBy (fun x -> x.Tests.Length))
        + globalContext.Tests.Length

    let private create () =
        MailboxProcessor<RunnerMsg>.Start
            (fun self ->
                let rec loop workers totalTestsAmount doneTestsAmount replyChannel =
                    async {
                        let! msg = self.Receive()

                        match msg with
                        | Start (cfg, globalContext, contexts, rc) ->
                            let workersNeeded =
                                calcWorkersNeeded cfg contexts globalContext

                            if workersNeeded < 1 then
                                rc.Reply 0
                                return ()

                            let testDone t = self.Post(TestDone t)
                            let testWorkers = Worker.createMany workersNeeded testDone
                            let cyclicTestWorkers = Utils.makeCycle testWorkers
                            let workerEnumerator = cyclicTestWorkers.GetEnumerator()

                            let nextWorker () =
                                workerEnumerator.MoveNext() |> ignore
                                workerEnumerator.Current

                            contexts
                            |> List.iter (fun c -> Worker.runContext c (nextWorker ()))

                            globalContext.Tests
                            |> List.iter (fun t -> Worker.runTest t (nextWorker ()))

                            let total = calcTotalTests contexts globalContext

                            return! loop testWorkers total 0 (Some rc)

                        | TestDone _ ->
                            let doneTestsAmount = doneTestsAmount + 1

                            if doneTestsAmount >= totalTestsAmount then
                                workers |> List.iter Worker.stop
                                replyChannel.Value.Reply 0
                                return ()

                            return! loop workers totalTestsAmount doneTestsAmount replyChannel
                    }

                loop [] 0 0 None)

    let private failIfWipsIfNeeded (cfg: RunConfiguration) contexts globalContext =
        if cfg.FailIfAnyWip then
            let wipTests =
                contexts
                |> List.collect (fun x -> x.Tests)
                |> List.append globalContext.Tests
                |> List.filter
                    (function
                    | Wip _ -> true
                    | _ -> false)

            if wipTests.Length > 0 then
                let testNames =
                    wipTests
                    |> List.map
                        (fun t ->
                            match t with
                            | Wip (n, _) -> Some n
                            | _ -> None)
                    |> List.choose id
                    |> String.concat "\n"

                failwith (
                    sprintf
                        "Flag %s is %b and there is wip tests:\n%s"
                        (nameof cfg.FailIfAnyWip)
                        cfg.FailIfAnyWip
                        testNames
                )

    let private runInternal cfg postGetter =
        endContext ()

        failIfWipsIfNeeded cfg collectedContexts globalContext

        let runner = create ()
        let post = postGetter (runner)

        post (fun rc -> Start(cfg, globalContext, collectedContexts, rc))

    /// <summary>
    /// Runs collected tests.
    /// </summary>
    /// <param name="cfg">Configuration to run tests with</param>
    let run cfg =
        runInternal cfg (fun r -> r.PostAndReply)

    /// <summary>
    /// Runs collected tests asynchronously.
    /// </summary>
    /// <param name="cfg">Configuration to run tests with</param>
    let runAsync cfg =
        runInternal cfg (fun r -> r.PostAndAsyncReply)
