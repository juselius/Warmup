module Warmup.Settings

open System.IO
open Microsoft.Extensions.Configuration
open Shared

let private config =
    ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory ())
        .AddJsonFile("appsettings.json")
        .Build()

let private orderSection (x : IConfigurationSection) =
    x.AsEnumerable true 
    |> Seq.sortBy (fun x -> x.Key |> int)
    |> Seq.map (fun x -> x.Value)

let inline optionOfNull x = if not (isNull x) then Some x else None 

let private from = 
    config.GetSection "from" 
    |> fun x -> (x.["email"], x.["name"])

let private rcpt = 
    config.GetSection "rcpt" 
    |> orderSection
    |> List.ofSeq


let defaultMailMsg = {
    from = from
    rcpt = rcpt
    subject = config.["subject"]
    body = config.["body"]
}

let defaultSmtpServer = {
    server = config.["server"]
    port = config.["port"] |> optionOfNull |> Option.map int |> Option.defaultValue 25
    user = config.["user"] |> optionOfNull
    password = config.["password"] |> optionOfNull
}

let hourlyRates = 
    config.GetSection "hourlyRates" 
    |> orderSection
    |> Array.ofSeq
    |> Array.map int
