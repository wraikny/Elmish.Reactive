module Elmish.Reactive

open System
open System.Threading
open System.Collections.Generic
open Elmish

type RxProgram<'arg, 'model, 'msg, 'view>(program: Program<'arg, 'model, 'msg, 'view>, arg) =

  let ctx = SynchronizationContext.Current
  do
    if isNull ctx then invalidOp "Call from UI thread"

  let onErrorEvent = Event<string * exn>()
  let viewEvent = Event<'view>()

  let mutable lastModel = Unchecked.defaultof<_>
  let mutable dispatch = Unchecked.defaultof<_>

  do
    let view = Program.view program

    program
    |> Program.withSetState (fun model dispatch ->
      (Program.setState program model dispatch)

      lastModel <- model
      viewEvent.Trigger(view model dispatch)
    )
    |> Program.withSubscription (fun _ ->
      [ fun f ->
        dispatch <- f
      ]
    )
    |> Program.mapErrorHandler(fun handler x ->
      handler x
      onErrorEvent.Trigger(x)
    )
    |> Program.runWith arg

  member private __.View with get() = viewEvent.Publish
  member __.OnError with get() = onErrorEvent.Publish

  member __.LastModel with get() = lastModel

  member __.Dispatch(msg) =
    if isNull SynchronizationContext.Current then
      ctx.Post((fun _ -> dispatch msg), null)
    else
      dispatch msg

  interface IObservable<'view> with
    member this.Subscribe(observer) =
      this.View.Subscribe(observer)


[<RequireQualifiedAccess>]
module RxProgram =
  let inline start arg program: RxProgram<'arg, 'model, 'msg, 'view> =
    new RxProgram<_, _, _, _>(program, arg)

  let inline startProgram (initModel: 'model * Cmd<'msg>) update =
    Program.mkProgram (fun() -> initModel) update (fun m _ -> m)
    |> start ()

  let inline startSimple (initModel: 'model) update =
    Program.mkSimple (fun () -> initModel) update (fun m _ -> m)
    |> start ()
