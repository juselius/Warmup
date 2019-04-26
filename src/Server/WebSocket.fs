module Warmup.WebSocket

open System
open System.Text
open System.Net.WebSockets
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open System.Threading

type private ByteSeg = ArraySegment<byte>

type ConnectionId = string

type ConnectionsAgentMessage =
    | ClientConnected of ConnectionId * WebSocket
    | ClientDisconnected of ConnectionId
    | Broadcast of string
    | Send of ConnectionId * string

type Agent = MailboxProcessor<ConnectionsAgentMessage>

let private sendMessage (ws : WebSocket) (message : string) =
    let buffer = Encoding.UTF8.GetBytes message
    let segment = ArraySegment<byte> buffer
    match ws.State with
    | WebSocketState.Open ->
        ws.SendAsync (
               segment,
               WebSocketMessageType.Text,
               true,
               CancellationToken.None
        )
        |> Async.AwaitTask
        |> ignore
    | _ -> ()
    ws.State

let WsAgent =
    Agent.Start(fun inbox ->
        let rec loop (connections: Map<ConnectionId, WebSocket>) =
            let send msg (KeyValue (cid, ws)) =
                match sendMessage ws msg with
                | WebSocketState.Open -> ()
                | _ -> inbox.Post (ClientDisconnected cid)
            async {
                match! inbox.Receive() with
                | Broadcast msg ->
                    connections |> Seq.iter (send msg)
                    do! loop connections
                | Send (cid, msg) ->
                    match connections.TryFind cid with
                    | Some ws -> send msg (KeyValuePair.Create(cid, ws))
                    | None -> ()
                    do! loop connections
                | ClientConnected (id, ws) ->
                    let now = System.DateTime.Now
                    printfn "Connected %A at %A" id now
                    do! loop ( Map.add id ws connections )
                | ClientDisconnected id ->
                    let now = System.DateTime.Now
                    printfn "Disconnected %A at %A" id now
                    do! loop (Map.remove id connections)
        }
        loop Map.empty
    )

let private newConnection (ctx : HttpContext) =
    printfn "%A" ctx.WebSockets.IsWebSocketRequest
    async {
        match ctx.WebSockets.IsWebSocketRequest with
        | true ->
            let! webSocket =
                ctx.WebSockets.AcceptWebSocketAsync()
                |> Async.AwaitTask
            return Some webSocket
        | false ->
            ctx.Response.StatusCode <- 400
            return None
    }

(* Create a new websocket connection of the specified type *)
let private newConnectionHandler (ctx : HttpContext) msgHandler =
    async {
        let cid = ctx.Connection.Id
        let recv : byte [] = Array.zeroCreate 4096
        let! ct = Async.CancellationToken
        let handle (webSocket : WebSocket) =
            let mutable cycle = true
            async {
                while cycle do
                    let! result =
                        webSocket.ReceiveAsync (ByteSeg(recv), ct)
                        |> Async.AwaitTask
                    if result.CloseStatus.HasValue then
                        cycle <- false
                    else
                        recv
                        |> Encoding.UTF8.GetString
                        |> function
                        | "Close" ->
                            WsAgent.Post (ClientDisconnected cid)
                            cycle <- false
                        | msg ->
                            msgHandler cid ctx msg
            }
        match! newConnection ctx with
        | Some ws ->
            WsAgent.Post (ClientConnected (cid, ws))
            do! handle ws
        | None -> ()
    }

type WebSocketHandler = ConnectionId -> HttpContext -> string -> unit

let voidHandler : WebSocketHandler = fun _ _ msg -> 
    try
        printfn "voidHandler: %s" msg
    with ex -> printfn "ws error: %s" ex.Message

type WebSocketMiddleware(next : RequestDelegate, path : PathString, handler : WebSocketHandler) =
    member __.Invoke(ctx : HttpContext) =
        async {
            if ctx.Request.Path = path then
                do! newConnectionHandler ctx handler
            else
                return! next.Invoke ctx |> Async.AwaitTask
        } |> Async.StartAsTask

