module Warmup.MVU

open Warmup.Server.Model
open Shared

[<RequireQualifiedAccess>]
module Cmd =

    let none : Cmd = []

    let ofMsg msg : Cmd = [ fun dispatch -> dispatch msg ]

    let inline ofAsyc f args ok err : Cmd =
        let exec dispatch =
            let r = f args
            let ok' = ok >> dispatch
            let err' = err >> dispatch
            Async.StartWithContinuations (r, ok', err', ignore)
        [ exec ]

    let batch (cmds : Cmd list) : Cmd = List.concat cmds

let mkProgram update view =
    MailboxProcessor.Start (fun inbox ->
        let rec loop (model : ServerModel) =
            async {
                let! msg = inbox.Receive()
                let model', cmd' = update msg model
                view model' inbox.Post
                cmd' |> List.iter (fun sub -> sub inbox.Post)
                do! loop model'
            }
        loop Default.defaultModel
    )
