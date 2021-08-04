/// <summary>
/// Contains functions to create and run tests.
/// </summary>
module Mulberry.TestCollect

open System

type TestFunc = unit -> unit

type internal Test =
    | Test of Name: string * Invoke: TestFunc
    | Wip of Name: string * Invoke: TestFunc
    | Skip of Name: string * Invoke: TestFunc
    | Many of Count: int * Name: string * Invoke: TestFunc

type internal TestContext = { Name: string; Tests: Test list }

let mutable internal globalContext = { Name = "Global context"; Tests = [] }
let mutable internal collectedContexts = []
let mutable private currentContext = None

/// <summary>
/// Ends current context if it was created with <see cref="M:Mulberry.TestCollect.context" /> function.
/// All tests created after this function call will be added to global context.
/// </summary>
let endContext () =
    match currentContext with
    | Some ctx ->
        collectedContexts <- ctx :: collectedContexts |> List.rev
        currentContext <- None
    | None -> ()

/// <summary>
/// Creates new context.
/// Tests added after new context created will be a part of this context.
/// Otherwise a part of global context.
/// </summary>
/// <param name="name">Name of new context</param>
let context name =
    endContext ()
    currentContext <- Some { Name = name; Tests = [] }

let private addTest t =
    let addTestToContext ctx =
        { ctx with
              Tests = t :: ctx.Tests |> List.rev }

    match currentContext with
    | Some ctx -> currentContext <- Some(addTestToContext ctx)
    | None -> globalContext <- addTestToContext globalContext

let private checkName n =
    if String.IsNullOrWhiteSpace n then
        failwith "Test name must be not empty."

/// <summary>
/// Adds a new named test.
/// </summary>
/// <param name="name">Name of test</param>
/// <param name="body">Body of test</param>
let ntest name body =
    checkName name
    Test(name, body) |> addTest

/// <summary>
/// Adds a new named test.
/// Same as <see cref="M:Mulberry.TestCollect.ntest" />.
/// </summary>
/// <param name="name">Name of test</param>
/// <param name="body">Body of test</param>
let (&&&) name body = ntest name body

/// <summary>
/// Adds a new named test marked as WIP (work in progress).
/// Tests marked as WIP runs slowly and highlights elements that it is interacting with.
/// </summary>
/// <param name="name">Name of test</param>
/// <param name="body">Body of test</param>
let nwip name body =
    checkName name
    Wip(name, body) |> addTest

/// <summary>
/// Adds a new named test marked as WIP (work in progress).
/// Tests marked as WIP runs slowly and highlights elements that it is interacting with.
/// Same as <see cref="M:Mulberry.TestCollect.nwip" />
/// </summary>
/// <param name="name">Name of test</param>
/// <param name="body">Body of test</param>
let (&&&&) name body = nwip name body

/// <summary>
/// Skips the test.
/// </summary>
/// <param name="name">Name of test</param>
/// <param name="body">Body of test</param>
let skip name body =
    checkName name
    Skip(name, body) |> addTest

/// <summary>
/// Skips the test.
/// Same as <see cref="M:Mulberry.TestCollect.skip" />
/// </summary>
/// <param name="name">Name of test</param>
/// <param name="body">Body of test</param>
let (!&&) name body = skip name body

/// <summary>
/// Adds a test that will be run few times.
/// </summary>
/// <param name="times">Value showing how many times test will be run</param>
/// <param name="name">Name of test</param>
/// <param name="body">Body of test</param>
let nmany times name body =
    if times < 0 then
        failwith $"Parameter {nameof times} must be bigger or equal to zero."

    checkName name

    Many(times, name, body) |> addTest

/// <summary>
/// Marks test as to be done.
/// Tests marked with this function will be marked by reporter too.
/// </summary>
let todo () = ()
