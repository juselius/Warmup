module Warmup.Form

open System
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Thoth.Elmish.FormBuilder
open Thoth.Elmish.FormBuilder.Types
open Thoth.Elmish.FormBuilder.BasicFields
open Thoth.Json
open Fulma
open Warmup.Model
open Shared

module SenderField =
    type State = {
        Label : string
        Value : string * string
        Type : string
        Placeholder : string option
        Validators : Validator list
        ValidationState : ValidationState
        Name : string
    }
    and Validator = State -> ValidationState

    type Msg =
        | ChangeValue of string
        interface IFieldMsg

    let private init (state : FieldState) =
        state, FormCmd.none

    let private validate (state : FieldState) =
        let state : State = state :?> State
        let rec applyValidators (validators : Validator list) (state : State) =
            match validators with
            | validator :: rest ->
                match validator state with
                | Valid -> applyValidators rest state
                | Invalid msg -> { state with ValidationState = Invalid msg }
            | [] -> state
        applyValidators state.Validators { state with ValidationState = Valid } |> box

    let private isValid (state : FieldState) =
        let state : State = state :?> State
        state.ValidationState = Valid

    let private setError (state : FieldState) (message : string)=
        let state : State = state :?> State
        { state with ValidationState = Invalid message } |> box

    let private toJson (state : FieldState) =
        let state : State = state :?> State
        state.Name, (Encode.tuple2 Encode.string Encode.string) state.Value

    let private update (msg : FieldMsg) (state : FieldState) =
        // Cast the received message into it's real type
        let msg = msg :?> Msg
        // Cast the received state into it's real type
        let state = state :?> State

        match msg with
        | ChangeValue newValue ->
            let v = newValue.Split [| ',' |]
            if v.Length <> 2 then
                {state with ValidationState = Invalid "wrong format"}
                |> box, FormCmd.none
            else
                { state with Value = v.[0], v.[1] }
                |> validate
                // We need to box the returned state
                |> box, FormCmd.none

    let private view (state : FieldState) (dispatch : IFieldMsg -> unit) =
        let state : State = state :?> State
        let className =
            if isValid state then
                "input"
            else
                "input is-danger"

        div [ Class "field" ]
            [ label [ Class "label"
                      HtmlFor state.Name ]
                [ str state.Label ]
              div [ Class "control" ]
                [ input [ Value state.Value
                          Placeholder (state.Placeholder |> Option.defaultValue "")
                          Id state.Name
                          Class className
                          OnChange (fun ev ->
                            ChangeValue ev.Value |> dispatch
                          ) ] ]
              span [ Class "help is-danger" ]
                [ str state.ValidationState.Text ] ]

    let config : FieldConfig = {
        View = view
        Update = update
        Init = init
        Validate = validate
        IsValid = isValid
        ToJson = toJson
        SetError = setError
    }

    type Sender private (state : Input.State) =
        static member Create(name : string) =
            Sender
                { Label = ""
                  Value = ""
                  Type = "from"
                  Placeholder = None
                  Validators = [ ]
                  ValidationState = Valid
                  Name = name }
        member __.WithDefaultView () : FieldBuilder = {
            Type = "from"
            State = state
            Name = state.Name
            Config = Input.config
        }

        member __.WithLabel (label : string) =
            Sender { state with Label = label }

        member __.WithPlaceholder (placeholder : string) =
            Sender { state with Placeholder = Some placeholder }

        member __.IsRequired (?msg : String) =
            let msg = defaultArg msg "This field is required"
            let validator (state : Input.State) =
                if String.IsNullOrWhiteSpace state.Value then
                    Invalid msg
                else
                    Valid
            Sender { state with Validators = state.Validators @ [ validator ] }

        member __.AddValidator (validator) =
            Sender { state with Validators = state.Validators @ [ validator ] }

// let (formState, formConfig) =
let initForm (settings : MailConfig) =
    let rates =
        let s1 = Array.head settings.hourlyRates |> string
        let s2 =
            Array.tail settings.hourlyRates
            |> Array.fold (fun a x -> a + ", " + string x) ""
        s1 + s2
    let sender = sprintf "%s, %s" (snd settings.mail.from) (fst settings.mail.from)
    let rcpt =
        let s1 = List.head settings.mail.rcpt
        let s2 =
            List.tail settings.mail.rcpt
            |> List.fold (fun a x -> a + "\n" + string x) ""
        s1 + s2
    Form<Msg>
        .Create(OnFormMsg)
        .AddField(
            BasicInput
                .Create("server")
                .WithLabel("SMTP gatewey")
                .WithPlaceholder("smtp.server.com")
                .WithValue(settings.server.server)
                .IsRequired()
                .WithDefaultView()
        )
        .AddField(
            BasicInput
                .Create("port")
                .WithLabel("SMTP port")
                .WithPlaceholder("25")
                .WithValue(settings.server.port |> string)
                .IsRequired()
                .WithDefaultView()
        )
        .AddField(
            BasicInput
                .Create("user")
                .WithLabel("SMTP user")
                .WithPlaceholder("warmup")
                .WithValue(Option.defaultValue "" settings.server.user)
                .WithDefaultView()
        )
        .AddField(
            BasicInput
                .Create("password")
                .WithLabel("SMTP password")
                .WithPlaceholder("secret")
                .WithValue(Option.defaultValue "" settings.server.password)
                .WithDefaultView()
        )
        .AddField(
            BasicInput
                .Create("hourlyRates")
                .WithLabel("Hourly rates")
                // .WithValue("20, 28, 39, 55, 77, 108, 151, 211, 295, 413, 579, 810, 1000, 1587, 2222, 3111, 4356, 6098, 8583, 11953, 16734")
                .WithValue(rates)
                .WithDefaultView()
        )
        .AddField(
            BasicInput
                .Create("from")
                .WithLabel("Sender address")
                .WithPlaceholder("Warmup SMTP <noreply@server.com>")
                .IsRequired()
                .WithValue(sender)
                .WithDefaultView()
        )
        .AddField(
            BasicTextarea
                .Create("rcpt")
                .WithLabel("Recipient addresses, one per line")
                .WithPlaceholder("someone@somewhere.com")
                .IsRequired()
                .WithValue(rcpt)
                .WithDefaultView()
        )
        .AddField(
            BasicInput
                .Create("subject")
                .WithLabel("Subject")
                .WithValue(settings.mail.subject)
                .IsRequired()
                .WithDefaultView()
        )
        .AddField(
            BasicTextarea
                .Create("body")
                .WithLabel("Mail body")
                .IsRequired()
                .WithValue(settings.mail.body)
                .WithDefaultView()
        )
        .Build()

let formDecoder body =
    let decoder =
        let dSender (x : string) =
            let s =x.Split [| ',' |] |> Array.map (fun x -> x.Trim())
            if s.Length <> 2 then
                Decode.fail (sprintf "invalid email format %s" x)
            else
                Decode.succeed (s.[1], s.[0])
        Decode.object (fun get ->
            let from =
                get.Required.Field "from" (Decode.andThen dSender Decode.string)
            let rcpt =
                get.Required.Field "rcpt" Decode.string
                |> fun x -> x.Split [| '\n' |]
                |> Array.filter (fun x -> x.Trim () |> fun y -> y.Length > 0)
                |> List.ofArray
            let rates =
                get.Required.Field "hourlyRates" Decode.string
                |> fun x -> x.Split [| ',' |]
                |> Array.map int
            {
                server = {
                   server = get.Required.Field "server" Decode.string
                   port = get.Required.Field "port" Decode.string |> int
                   user = get.Optional.Field "user" Decode.string
                   password = get.Optional.Field "password" Decode.string
                }
                mail = {
                   from = from
                   rcpt = rcpt
                   subject = get.Required.Field "subject" Decode.string
                   body = get.Required.Field "body" Decode.string
                }
                hourlyRates = rates
            }
        )
    Decode.fromString decoder body

