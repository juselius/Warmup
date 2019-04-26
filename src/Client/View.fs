module Warmup.View

open Fable.Helpers.React
open Fulma
open Thoth.Elmish
open Thoth.Elmish.FormBuilder

open Shared
open Warmup.Model

let formActions (formState : FormBuilder.Types.State) dispatch =
    Field.div [
        Field.IsGrouped
        Field.IsGroupedCentered
    ] [
        Control.div [] [
            Button.button [
                Button.Color IsWarning
                Button.OnClick (fun _ -> dispatch Reset)
            ] [ str "Reset" ]
        ]
        Control.div [] [
            Button.button [
                Button.Color IsPrimary
                Button.IsLoading (Form.isLoading formState)
                Button.OnClick (fun _ -> dispatch Submit)
            ] [ str "Submit" ]
        ]
    ]

let viewFormEditing (formState : FormState) (formConfig : FormConfig) dispatch =
    Form.render {
        Config = formConfig
        State = formState
        Dispatch = dispatch
        ActionsArea = (formActions formState dispatch)
        Loader = Form.DefaultLoader
    }

let navbar =
    Navbar.navbar [ Navbar.Color IsBlack ] [
        Navbar.Item.div [] [
            Heading.h2 [ Heading.Modifiers [
                Modifier.TextColor Color.IsGreyLight ] ] [
                    str "Warmup Mail Exchange"
                ]
        ]
    ]
let footer =
    Footer.footer [] [
        Content.content [
            Content.Modifiers [
                Modifier.TextAlignment (Screen.All, TextAlignment.Centered)
            ]
        ] [ str "Copyright (c) 2019, Serit IT Partner Tromsø AS" ]
    ]

let controlButtons (model : Model) (dispatch : Msg -> unit) =
    let pauseButton =
        match model.State with
        | State Running ->
            Button.button [
                Button.Color IsSuccess
                Button.OnClick (fun _ -> dispatch Pause)
            ] [ str "Pause" ]
        | State Paused ->
            Button.button [
                Button.Color IsSuccess
                Button.OnClick (fun _ -> dispatch Continue)
            ] [ str "Continue" ]
        | _ -> div [] []
    let stopButton =
        match model.State with
        | State Cancelled | State Completed ->
            Button.button [
                Button.Color IsWarning
                Button.OnClick (fun _ -> dispatch Done)
            ] [ str "Done" ]
        | _ ->
            Button.button [
                Button.Color IsDanger
                Button.OnClick (fun _ -> dispatch Stop)
            ] [ str "Stop" ]
    Columns.columns [] [
        Column.column [] [ pauseButton ]
        Column.column [] [ stopButton ]
    ]

let statsTable (model : Model) =
    Table.table [ Table.IsHoverable] [
        thead [] [
            tr [] [
                th [] [ str "Decription"]
                th [] [ str "Sent"]
                th [] [ str "Failed"]
            ]
        ]
        tbody [] [
            tr [] [
                td [] [ str "Last hour" ]
                td [] [ str (string model.ServerStats.messagesSent) ]
                td [] [ str (string model.ServerStats.messagesFailed) ]
            ]
            tr [] [
                td [] [ str "Last day" ]
                td [] [ str (string model.ServerStats.sentToday) ]
                td [] [ str (string model.ServerStats.failedToday) ]
            ]
            tr [] [
                td [] [ str "Total" ]
                td [] [ str (string model.ServerStats.totalSent) ]
                td [] [ str (string model.ServerStats.totalFailed) ]
            ]
        ]
    ]

let viewRunning (model : Model) (dispatch : Msg -> unit) =
    div [] [
        Heading.h2 [] [ str "Running stats" ]
        Heading.h4 [] [ str (sprintf "Running %d hours" model.ServerStats.hours)]
        Box.box' [] [
            statsTable model
            controlButtons model dispatch
        ]
        p [] [
            str (sprintf "Server: %A" model.ServerStats.runningState)
            br []
            str (sprintf "Client: %A" model.State)
        ]
    ]