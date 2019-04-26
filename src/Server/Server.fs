module Warmup.Main

open System
open System.IO
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2
open Giraffe
open Warmup.Settings
open Warmup.Server.Model
open Warmup.Warmup
open Shared

let tryGetEnv =
    Environment.GetEnvironmentVariable
    >> function null | "" -> None | x -> Some x

let publicPath =
    let devel = Path.GetFullPath "../Client/public"
    let prod _ = Path.GetFullPath "./public"
    "PRODUCTION" |> tryGetEnv |> Option.map prod |> Option.defaultValue devel

let port =
    "SERVER_PORT"
    |> tryGetEnv
    |> Option.map uint16
    |> Option.defaultValue 8085us

let start next (ctx : Http.HttpContext) =
    let t =
        task {
            match! ctx.BindJsonAsync<Result<MailConfig, string>>() with
            | Ok cfg ->
                let model = {
                    Default.defaultModel with
                        mailConfig = cfg
                }
                // metronome model |> ignore
                mvu.Post (Start model)
                return! Successful.OK 1 next ctx
            | Error _  ->
                return! Successful.NO_CONTENT next ctx
    }
    let state = getState ()
    match state.runningState with
    | Initializing | Cancelled | Completed -> t
    | _ -> Successful.OK "" next ctx

let defaults =
    let model =
        { Default.defaultModel with
            mailConfig = {
                server = defaultSmtpServer
                mail = defaultMailMsg
                hourlyRates = hourlyRates
            }
        }
    json model

let pause () =
    mvu.Post Pause
    Successful.OK 0

let cont () =
    mvu.Post Continue
    Successful.OK 0

let stats () =
    let state = getState ()
    json state

let stop () =
    mvu.Post Stop
    stats ()

let webApp =
    choose [
        POST >=> route "/api/start" >=> warbler (fun _ -> start)
        choose [
            route "/api/stop" >=> warbler (fun _ -> stop ())
            route "/api/pause" >=> warbler (fun _ -> pause ())
            route "/api/continue" >=> warbler (fun _ -> cont ())
            route "/api/state" >=> warbler (fun _ -> stats ())
            route "/api/defaults" >=> defaults
        ]
        RequestErrors.notFound (text "not found")
    ]

type WS = WebSocket.WebSocketMiddleware

let configureApp (app : IApplicationBuilder) =
    app.UseDefaultFiles()
       .UseStaticFiles()
       .UseWebSockets()
       .UseMiddleware<WS>(PathString "/ws", WebSocket.voidHandler)
       .UseGiraffe webApp

type Json = Giraffe.Serialization.Json.IJsonSerializer

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore
    services.AddSingleton<Json>(Thoth.Json.Giraffe.ThothSerializer()) |> ignore

let webHost () =
    WebHost
        .CreateDefaultBuilder()
        .UseWebRoot(publicPath)
        .UseContentRoot(publicPath)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .UseUrls("http://0.0.0.0:" + port.ToString() + "/")
        .Build()
        .Run()

[<EntryPoint>]
let main argv =
    webHost ()
    0