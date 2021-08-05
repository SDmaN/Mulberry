module internal Mulberry.Utils

#nowarn "40"

let makeCycle s =
    let rec cycle =
        seq {
            yield! s
            yield! cycle
        }

    cycle
