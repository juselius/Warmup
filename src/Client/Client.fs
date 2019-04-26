module Client

open Elmish
open Elmish.React
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch
open Fable.Core.JsInterop
open Fable.PowerPack
open Fable.Import
open Fulma
open Thoth.Json
open Thoth.Elmish
open Thoth.Elmish.FormBuilder

open Shared
open Warmup.Model
open Warmup.Form
open Warmup.View

let getState api =
    let decoder = Decode.Auto.generateDecoder<ServerModel>()
    promise {
        let! s = fetchAs<ServerModel> api decoder []
        return (s.runningState, s)
    }

let initialModel = {
    State = State Initializing
    ServerStats = Default.defaultModel
}
let reset () : Model * Cmd<Msg> =
    initialModel, Cmd.ofPromise getState "/api/defaults" InitializeForm OnError

type R = RunningState

let formInit runningState (stats : ServerModel) model =
    match runningState with
    | R.Initializing | R.Cancelled | R.Completed ->
        let (formState', formConfig) = initForm stats.mailConfig
        let (formState, formCmds) = Form.init formConfig formState'
        let model' = {
            model with
                State = Editing (formState, formConfig)
        }
        model', Cmd.map OnFormMsg formCmds
    | _ ->
        let model' = {
            model with
                State = State runningState
                ServerStats = stats
        }
        model', Cmd.none

let inline applyIfEditing (model : Model) (f : FormState -> Model * Cmd<Msg>) =
    match model.State with
    | Editing (formState, _) -> f formState
    | State _ -> model, Cmd.none

let submitForm body =
    let bdy =
        formDecoder body
        |> fun x -> Encode.Auto.toString (4, x)
    Browser.console.log bdy
    let props = [
        RequestProperties.Method HttpMethod.POST
        RequestProperties.Body !^bdy
    ]
    let requestPath = "/api/start"
    let decoder = Decode.Auto.generateDecoder<int>()
    promise {
        return! fetchAs<int> requestPath decoder props
    }

// this is a bit hackish, and works only while developing locally
#if DEBUG
let WsUrl = sprintf "ws://%s:8085/ws" Browser.window.location.hostname
#else
let WsUrl = sprintf "ws://%s/ws" Browser.window.location.host
#endif

let updateStats (_ : Model) =
    let socket = Browser.WebSocket.Create WsUrl
    let sub dispatch =
        socket.addEventListener_message (fun _ -> dispatch OnUpdate)
    Cmd.ofSub sub

let onFormMsg msg (model : Model) =
    applyIfEditing
        model
        (fun formState ->
            match model.State with
            | Editing (_, formConfig) ->
                let (newFormState, formCmd) =
                   Form.update formConfig msg formState
                { model with
                    State = Editing (newFormState, formConfig)
                }, Cmd.map OnFormMsg formCmd
            | _ -> model, Cmd.none
        )

let submit (model : Model) =
    let f formState formConfig =
        let (newFormState, isValid) = Form.validate formConfig formState
        if isValid then
            let body = Form.toJson formConfig newFormState
            let model' =
                { model with
                    State = Editing (
                                Form.setLoading false formState,
                                formConfig
                    )
            }
            model', Cmd.ofPromise submitForm body OnSubmit OnError
        else
            { model with State = Editing (newFormState, formConfig) }, Cmd.none
    applyIfEditing
        model
        (fun formState ->
            match model.State with
            | Editing (_, formConfig) -> f formState formConfig
            | _ -> model, Cmd.none
        )

let onSubmit model =
    match model.State with
    | Editing _ ->
        { model with State = State Running}, Cmd.none
    | _ -> model, Cmd.none

let doIgnore _ = Ignore

let pause model =
    let p () = promise { return! fetchAs<int> "/api/pause" Decode.int [] }
    { model with State = State Paused }, Cmd.ofPromise p () doIgnore OnError

let doContinue model =
    let p () = promise { return! fetchAs<int> "/api/continue" Decode.int [] }
    { model with State = State Running }, Cmd.ofPromise p () doIgnore OnError

let stop model =
    let decoder  = Decode.Auto.generateDecoder<ServerModel>()
    let p () =
        promise {
            return! fetch "/api/stop" []
        }
    { model with State = State Cancelled }, Cmd.ofPromise p () doIgnore OnError

let onUpdate model =
    let d = Decode.Auto.generateDecoder<Shared.ServerModel>()
    let p () = promise { return! fetchAs<Shared.ServerModel> "/api/state" d [] }
    model, Cmd.ofPromise p () OnStats OnError

let onStats m model =
    let model' =
        { model with
            State =
                match model.State with
                | Editing _ as x -> x
                | _ -> State m.runningState
            ServerStats = m
        }
    model', Cmd.none

let onError error model =
    Browser.console.error error
    model, Cmd.none

let init () : Model * Cmd<Msg> =
    initialModel, Cmd.ofPromise getState "/api/state" InitializeForm OnError

let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
    match msg with
    | InitializeForm (stats, settings) -> formInit stats settings model
    | OnFormMsg msg -> onFormMsg msg model
    | Submit -> submit model
    | Ignore -> model, Cmd.none
    | OnSubmit _ -> onSubmit model
    | OnError e -> onError e model
    | Pause -> pause model
    | Continue -> doContinue model
    | Stop -> stop model
    | OnUpdate -> onUpdate model
    | OnStats m -> onStats m model
    | Done -> init ()
    | Reset -> reset ()


let view (model : Model) (dispatch : Msg -> unit) =
    let content =
        match model.State with
        | Editing (formState, formConfig) ->
            viewFormEditing formState formConfig dispatch
        | State Initializing ->
            div [] [
                Heading.h6 [] [ str "The server is initializing, please reload." ]
                p [] [
                    str (sprintf "Server: %A" model.ServerStats.runningState)
                    br []
                    str (sprintf "Client: %A" model.State)
                ]
            ]
        | _ -> viewRunning model dispatch
    div [] [
        navbar
        Container.container [] [
            Content.content [] [
                div [
                    Style [
                        MaxWidth "800px"
                        PaddingTop "50px"
                    ]
                ] [ content ]
            ]
        ]
        footer
    ]

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram init update view
|> Program.withSubscription updateStats
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR
#endif
|> Program.withReact "elmish-app"
#if DEBUG
// |> Program.withDebugger
#endif
|> Program.run
