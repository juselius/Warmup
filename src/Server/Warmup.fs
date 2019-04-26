module Warmup.Warmup

open System
open System.Threading
open System.Net.Mail
open Warmup.Server.Model
open Warmup.MVU
open Shared
open System.Net
open Warmup
open System.Net.WebSockets

let mutable private serverState = {
    Default.defaultModel with
        runningState = Initializing
        mailConfig = {
            server = Settings.defaultSmtpServer
            mail = Settings.defaultMailMsg
            hourlyRates = Settings.hourlyRates
    }
}

let private sendMail (server : SmtpServer) (mail : MailMsg) =
    let msg = new MailMessage()
    msg.From <- mail.from |> MailAddress
    mail.rcpt |> List.iter (MailAddress >> msg.To.Add)
    msg.Subject <- mail.subject
    msg.Body <- mail.body
    let smtp = new SmtpClient(server.server)
    smtp.Port <- server.port
    if server.user.IsSome then
        let user = server.user.Value
        let pw = Option.defaultValue "" server.password
        let cred = NetworkCredential(user, pw)
        let cache = CredentialCache()
        cache.Add (server.server, server.port, "Basic", cred)
        smtp.UseDefaultCredentials <- true
        if server.port = 465  then
            smtp.EnableSsl <- true
        else
            smtp.EnableSsl <- false
        smtp.Credentials <- cache
    else ()
    try
        smtp.Send msg
    with ex ->
        printfn "%A" ex
        raise ex

let private sendMailAsync (server : SmtpServer) m =
    async {
        return sendMail server m
        // return! Async.Sleep 300 // DEBUG
    }

let private sendFreq n =
    // let msHour = 1 * 10 * 1000 // DEBUG
    let msHour = 60 * 60 * 1000
    let margin = 0.01 * float msHour |> int
    if n > 0 then
        (msHour + margin)/n |> int
    else
        1000

let private sendMessage (model : ServerModel) =
    let mail = model.mailConfig.mail
    let mailer = sendMailAsync model.mailConfig.server mail
    let timeout = 5000
    let task () =
        async {
            let! send = Async.StartChild (mailer, timeout)
            try
                do! send
                return sprintf "%d" (model.messagesSent + 1)
            with ex ->
                return raise ex
        }
    model, Cmd.ofAsyc task () MessageSent MessageFailed

let private step model =
    // let day = model.hours / 2 // DEBUG
    // let sent, failed =
    //     if model.hours % 2 = 0 then 0, 0
    //     else model.sentToday, model.failedToday
    let day = model.hours / 24
    let sent, failed =
        if model.hours % 24 = 0 then 0, 0
        else model.sentToday, model.failedToday
    if model.mailConfig.hourlyRates.Length > day then
        Some {
            model with
                hours = model.hours + 1
                freq = sendFreq (model.mailConfig.hourlyRates.[day])
                remaining = model.mailConfig.hourlyRates.[day]
                messagesSent = 0
                messagesFailed = 0
                sentToday = sent
                failedToday = failed
            }
    else None

let private updateStart model =
    printfn "* Start"
    let model' =
        { model with
            runningState = Running
            hours = 0 //model.hours
        }
    model', Cmd.ofMsg QueueNext

let private updateStop model =
    printfn "* Stop"
    { model with
        freq = 1000
        remaining = 0
        runningState = Cancelled
    }, Cmd.none

let private updatePause model =
    printfn "* Pause"
    { model with runningState = Paused }, Cmd.none

let private updateContinue model =
    printfn "* Continue"
    { model with runningState = Running } , Cmd.none

let private updateStep (model : ServerModel) =
    match step model with
    | Some m -> m, Cmd.ofMsg QueueNext
    | None -> model, Cmd.ofMsg Done

let private updateMessageSent model x =
    printfn "* Message sent: %s" x
    let model' =
        { model with
            messagesSent = model.messagesSent + 1
            sentToday = model.sentToday + 1
            totalSent = model.totalSent + 1
        }
    model', Cmd.none

let private updateMessageFailed model (x : Exception) =
    printfn "* Message failed: %s" x.Message
    let model' =
        { model with
            messagesFailed = model.messagesFailed + 1
            failedToday = model.failedToday + 1
            totalFailed = model.totalFailed + 1
        }
    model', Cmd.none

let private updateDone model =
    printfn "* Warmup done!"
    let model' = { model with runningState = Completed }
    model', Cmd.none

let private queueNext (model : ServerModel) =
    match model.runningState with
    | Running ->
        if model.remaining > 0 then
            let _, mail = sendMessage model
            let next = Cmd.ofAsyc Async.Sleep model.freq (fun _ -> QueueNext) Failure
            let model' = { model with remaining = model.remaining - 1 }
            model', Cmd.batch [ mail; next ]
        else
            model, Cmd.ofMsg Step
    | Paused ->  model, Cmd.ofAsyc Async.Sleep 1000 (fun _ -> QueueNext) Failure
    | Cancelled | Initializing | Completed -> model, Cmd.none

let private update (msg : Msg) (model : ServerModel) =
    match msg with
    | Start m -> updateStart m
    | QueueNext -> queueNext model
    | Failure ex -> printfn "ERROR: %s" ex.Message; model, Cmd.ofMsg QueueNext
    | Stop -> updateStop model
    | Pause -> updatePause model
    | Continue -> updateContinue model
    | Done -> updateDone model
    | SendMessage -> sendMessage model
    | Step -> updateStep model
    | MessageSent x -> updateMessageSent model x
    | MessageFailed x -> updateMessageFailed model x

type private WsMsg = WebSocket.ConnectionsAgentMessage

let private view (model : ServerModel) (dispatch : Msg -> unit) =
    if serverState <> model then
        serverState <- model
        WebSocket.WsAgent.Post (WsMsg.Broadcast "")
    else ()

let getState () = serverState

let mvu = mkProgram update view
