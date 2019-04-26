namespace Shared

open System.Threading

type EmailAddress = string * string

type RunningState =
    | Initializing
    | Running
    | Paused
    | Cancelled
    | Completed

type MailMsg = {
    from : EmailAddress
    rcpt : string list
    subject : string
    body : string
}

type SmtpServer = {
    server : string
    port : int
    user : string option
    password : string option
}

type MailConfig = {
    server : SmtpServer
    mail : MailMsg
    hourlyRates: int array
}

type ServerModel = {
    hours : int
    freq : int
    remaining : int
    messagesSent: int
    messagesFailed: int
    sentToday: int
    failedToday: int
    totalSent: int
    totalFailed: int
    runningState : RunningState
    mailConfig : MailConfig
    // warmupSequence : int list
}

module Default =

    let defaultMailMsg = {
        from = "",""
        rcpt = []
        subject = ""
        body = ""
    }

    let defaultSmtpServer = {
        server = ""
        port = 25
        user = None
        password = None
    }

    let hourlyRates = [| 0 |]

    let defaultModel = {
        hours = 0
        freq = 1000
        remaining = 0
        messagesSent = 0
        messagesFailed = 0
        sentToday = 0
        failedToday = 0
        totalSent = 0
        totalFailed = 0
        runningState = Initializing
        mailConfig = {
            server = defaultSmtpServer
            mail = defaultMailMsg
            hourlyRates = hourlyRates
        }
        // warmupSequence = []
    }