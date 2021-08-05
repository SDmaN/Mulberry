namespace Mulberry

open System
open TestCollect

/// <summary>
/// Parameters of testing process.
/// </summary>
/// <param name="workersAmount">Amount of test workers (browsers) that run in parallel</param>
type RunConfiguration(?workersAmount: int) =
    let workersAmount = defaultArg workersAmount 1

    /// <summary>
    /// Returns amount of test workers (browsers) that run in parallel.
    /// </summary>
    member _.WorkersAmount = workersAmount

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


    let private runInternal cfg postGetter =
        endContext ()
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
