module Warmup.Server.Model

open System
open Shared

type Msg =
    | Start of ServerModel
    | QueueNext
    | Failure of Exception
    | Stop
    | Pause
    | Continue
    | Done
    | Step
    | SendMessage
    | MessageSent of string
    | MessageFailed of Exception

type Dispatch = Msg -> unit

type Sub = Dispatch -> unit

type Cmd = Sub list
