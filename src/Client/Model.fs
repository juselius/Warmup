module Warmup.Model

open Thoth.Elmish
open FormBuilder.Types

type Msg =
    | Submit
    | InitializeForm of Shared.RunningState * Shared.ServerModel
    | OnFormMsg of FormBuilder.Types.Msg
    | OnSubmit of int
    | Ignore
    | OnError of System.Exception
    | OnUpdate
    | OnStats of Shared.ServerModel
    | Pause
    | Continue
    | Stop
    | Done
    | Reset

type FormState = State

type FormConfig = Config<Msg>

type CreationResponse =
    | Ok
    | Errors of ErrorDef list

type State =
    | State of Shared.RunningState
    | Editing of FormState * FormConfig

type Model = {
    State : State
    ServerStats : Shared.ServerModel
    }
